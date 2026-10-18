using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CCRPPlugin.Commands
{
    internal class CCRPInvCommand : IRocketCommand
    {
        private const byte SnapshotWidth = 10;

        private class View
        {
            public Items Snapshot;
            public ItemRemoved Handler;
        }

        // Viewer Steam64 -> active snapshot, so handlers can be detached when the view ends.
        private static readonly Dictionary<ulong, View> views = new Dictionary<ulong, View>();

        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "inv";

        public string Help => "View a snapshot of an online player's whole inventory.";

        public string Syntax => "<name|Steam64>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.inv" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer viewer = caller as UnturnedPlayer;
            if (viewer == null)
            {
                return;
            }

            if (command.Length != 1)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("InvSyntax"), Main.Instance.MessageColor);
                return;
            }

            UnturnedPlayer target = FindTarget(command[0]);
            if (target == null)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("InvPlayerNotFound"), Main.Instance.MessageColor);
                return;
            }

            if (target.CSteamID == viewer.CSteamID)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("InvSelf"), Main.Instance.MessageColor);
                return;
            }

            ReleaseView(viewer.CSteamID.m_SteamID);

            Items snapshot = BuildSnapshot(target.Player.inventory);
            PlayerInventory inventory = viewer.Player.inventory;
            inventory.isStoring = true;
            inventory.isStorageTrunk = false;
            inventory.storage = null;
            inventory.updateItems(PlayerInventory.STORAGE, snapshot);
            inventory.sendStorage();

            View view = new View { Snapshot = snapshot };
            view.Handler = (page, index, jar) => DiscardTakenCopy(viewer, jar);
            snapshot.onItemRemoved += view.Handler;
            views[viewer.CSteamID.m_SteamID] = view;

            UnturnedChat.Say(caller, Main.Instance.Translate("InvOpened", target.CharacterName), Main.Instance.MessageColor);
            Rocket.Core.Logging.Logger.Log($"{viewer.CharacterName} ({viewer.Id}) opened a snapshot of the inventory of {target.CharacterName} ({target.Id})!");
        }

        private static Items BuildSnapshot(PlayerInventory source)
        {
            List<Item> copies = new List<Item>();
            int area = 0;
            for (byte page = 0; page < PlayerInventory.STORAGE; page++)
            {
                Items items = source.items[page];
                if (items == null)
                {
                    continue;
                }

                for (byte index = 0; index < items.getItemCount(); index++)
                {
                    ItemJar jar = items.getItem(index);
                    if (jar == null || jar.item == null)
                    {
                        continue;
                    }

                    byte[] state = jar.item.state == null ? new byte[0] : (byte[])jar.item.state.Clone();
                    copies.Add(new Item(jar.item.id, jar.item.amount, jar.item.quality, state));
                    area += jar.size_x * jar.size_y;
                }
            }

            Items snapshot = new Items(PlayerInventory.STORAGE);
            int height = System.Math.Min(255, System.Math.Max(4, area * 2 / SnapshotWidth + 4));
            snapshot.resize(SnapshotWidth, (byte)height);

            foreach (Item copy in copies)
            {
                while (!snapshot.tryAddItem(copy) && height < 255)
                {
                    height = System.Math.Min(255, height + 4);
                    snapshot.resize(SnapshotWidth, (byte)height);
                }
            }

            return snapshot;
        }

        // The window shows copies, so an item dragged out would be a duplicate; remove it from the viewer once the move finishes.
        private static void DiscardTakenCopy(UnturnedPlayer viewer, ItemJar removed)
        {
            if (removed == null || removed.item == null || Main.Instance == null)
            {
                return;
            }

            Main.Instance.StartCoroutine(DiscardTakenCopyNextFrame(viewer, removed.item));
        }

        private static IEnumerator DiscardTakenCopyNextFrame(UnturnedPlayer viewer, Item item)
        {
            yield return null;

            if (viewer == null || viewer.Player == null)
            {
                yield break;
            }

            PlayerInventory inventory = viewer.Player.inventory;
            for (byte page = 0; page < PlayerInventory.STORAGE; page++)
            {
                Items items = inventory.items[page];
                if (items == null)
                {
                    continue;
                }

                for (byte index = 0; index < items.getItemCount(); index++)
                {
                    ItemJar jar = items.getItem(index);
                    if (jar != null && ReferenceEquals(jar.item, item))
                    {
                        inventory.removeItem(page, index);
                        UnturnedChat.Say(viewer, Main.Instance.Translate("InvSnapshotOnly"), Main.Instance.MessageColor);
                        yield break;
                    }
                }
            }
        }

        private static UnturnedPlayer FindTarget(string value)
        {
            if (ulong.TryParse(value, out ulong id) && id > 0)
            {
                return UnturnedPlayer.FromCSteamID(new CSteamID(id));
            }

            return UnturnedPlayer.FromName(value);
        }

        private static void ReleaseView(ulong viewerId)
        {
            View view;
            if (views.TryGetValue(viewerId, out view))
            {
                view.Snapshot.onItemRemoved -= view.Handler;
                views.Remove(viewerId);
            }
        }

        internal static void ReleaseViews(UnturnedPlayer player)
        {
            ReleaseView(player.CSteamID.m_SteamID);
        }
    }
}
