using CCRP_Plugin.Services;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal class CCRPItemFeaturesCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "itemfeatures";
        public string Help => "Enables or disables features on the currently held item.";
        public string Syntax => "<autoreload|autorepair|all> <on|off>";
        public List<string> Aliases => new List<string> { "if" };
        public List<string> Permissions => new List<string> { "ccrp.itemfeatures" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player) || player.Player == null)
            {
                return;
            }

            bool allFeatures;
            bool autoReload;
            bool autoRepair;
            bool enabled;
            if (!TryParse(command, out allFeatures, out autoReload, out autoRepair, out enabled))
            {
                UnturnedChat.Say(caller, "Syntax: /itemfeatures <autoreload|autorepair|all> <on|off>");
                return;
            }

            ItemJar heldItem = FindHeldItem(player.Player);
            if (heldItem == null)
            {
                UnturnedChat.Say(caller, "You must be holding an item to use this command.");
                return;
            }

            ItemFeaturesService service = player.Player.GetComponent<ItemFeaturesService>();
            if (service == null)
            {
                service = player.Player.gameObject.AddComponent<ItemFeaturesService>();
                service.Initialize(player.Player);
            }

            bool nextAutoReload = autoReload;
            bool nextAutoRepair = autoRepair;
            if (allFeatures)
            {
                nextAutoReload = enabled;
                nextAutoRepair = enabled;
            }
            else
            {
                bool currentAutoReload;
                bool currentAutoRepair;
                service.GetFeatures(heldItem, out currentAutoReload, out currentAutoRepair);
                if (autoReload)
                {
                    nextAutoReload = enabled;
                    nextAutoRepair = currentAutoRepair;
                }
                else
                {
                    nextAutoReload = currentAutoReload;
                    nextAutoRepair = enabled;
                }
            }

            service.SetFeatures(heldItem, nextAutoReload, nextAutoRepair);
            UnturnedChat.Say(caller, "Item features for this item are now " +
                DescribeFeatures(nextAutoReload, nextAutoRepair) + ".");
        }

        private static bool TryParse(string[] command, out bool allFeatures, out bool autoReload, out bool autoRepair, out bool enabled)
        {
            allFeatures = false;
            autoReload = false;
            autoRepair = false;
            enabled = false;

            if (command == null || command.Length != 2)
            {
                return false;
            }

            if (string.Equals(command[0], "all", StringComparison.OrdinalIgnoreCase))
            {
                allFeatures = true;
            }
            else if (string.Equals(command[0], "autoreload", StringComparison.OrdinalIgnoreCase))
            {
                autoReload = true;
            }
            else if (string.Equals(command[0], "autorepair", StringComparison.OrdinalIgnoreCase))
            {
                autoRepair = true;
            }
            else
            {
                return false;
            }

            if (string.Equals(command[1], "on", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                return true;
            }

            return string.Equals(command[1], "off", StringComparison.OrdinalIgnoreCase);
        }

        private static ItemJar FindHeldItem(Player player)
        {
            if (player.equipment == null || player.inventory == null || !player.equipment.isEquipped)
            {
                return null;
            }

            Items[] pages = player.inventory.items;
            byte pageIndex = player.equipment.equippedPage;
            if (pages == null || pageIndex >= pages.Length || pages[pageIndex] == null)
            {
                return null;
            }

            Items page = pages[pageIndex];
            byte count = page.getItemCount();
            for (byte index = 0; index < count; index++)
            {
                ItemJar item = page.getItem(index);
                if (item != null && item.x == player.equipment.equipped_x && item.y == player.equipment.equipped_y)
                {
                    return item;
                }
            }

            return null;
        }

        private static string DescribeFeatures(bool autoReload, bool autoRepair)
        {
            if (autoReload && autoRepair)
            {
                return "autoreload and autorepair enabled";
            }
            if (autoReload)
            {
                return "autoreload enabled";
            }
            if (autoRepair)
            {
                return "autorepair enabled";
            }

            return "disabled";
        }
    }
}
