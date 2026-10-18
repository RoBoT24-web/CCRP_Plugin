
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CCRPPlugin.Commands
{
    internal class CCRPGiveCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "i";

        public string Help => "";

        public string Syntax => "[id] [amount]";

        public List<string> Aliases => new List<string> { "give" };

        public List<string> Permissions => new List<string> { "ccrp.i" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player))
                return;

            ushort id = 0;
            string itemString = "";
            byte amount = 1;
            switch (command.Length)
            {
                //Case no amount
                case 1:
                    itemString = command[0].ToString();
                    if (!ushort.TryParse(itemString, out id))
                    {
                        List<ItemAsset> sortedAssets = new List<ItemAsset>(SDG.Unturned.Assets.find(EAssetType.ITEM).Cast<ItemAsset>());
                        ItemAsset asset = sortedAssets.Where(i => i.itemName != null).OrderBy(i => i.itemName.Length).Where(i => i.itemName.ToLower().Contains(itemString.ToLower())).FirstOrDefault();
                        if (asset != null) id = asset.id;
                        if (String.IsNullOrEmpty(itemString.Trim()) || id == 0)
                        {
                            UnturnedChat.Say(player, Main.Instance.Translate("GiveItemNotFound"), Main.Instance.MessageColor);
                            Logger.Log($"{caller.DisplayName} ({caller.Id}) item not found! (/give)");
                            return;
                        }
                    }

                    ItemAsset itemAsset = Assets.find(EAssetType.ITEM, id) as ItemAsset;
                    if (itemAsset == null)
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("GiveItemNotFound"), Main.Instance.MessageColor);
                        return;
                    }

                    if (Main.Instance.IsItemBlockedFromGive(id, player))
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("GiveNoBypass"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) doesn't have the bypass for {id}!");
                        return;
                    }
                    player.GiveItem(id, 1);

                    UnturnedChat.Say(caller, Main.Instance.Translate("GiveSuccessWithoutAmount", itemAsset.itemName, id), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) received 1x {itemAsset.itemName} ({id})!");
                    break;
            
                //Case amount
                case 2:
                    itemString = command[0].ToString();
                    if(!byte.TryParse(command[1], out amount))
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("GiveAddValidNumber"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) wrong amount (/give)!");
                        return;
                    }

                    if (!ushort.TryParse(itemString, out id))
                    {
                        List<ItemAsset> sortedAssets = new List<ItemAsset>(SDG.Unturned.Assets.find(EAssetType.ITEM).Cast<ItemAsset>());
                        ItemAsset asset = sortedAssets.Where(i => i.itemName != null).OrderBy(i => i.itemName.Length).Where(i => i.itemName.ToLower().Contains(itemString.ToLower())).FirstOrDefault();
                        if (asset != null) id = asset.id;
                        if (String.IsNullOrEmpty(itemString.Trim()) || id == 0)
                        {
                            UnturnedChat.Say(caller, Main.Instance.Translate("GiveItemNotFound"), Main.Instance.MessageColor);
                            return;
                        }
                    }
                    ItemAsset itemAssetWithAmount = Assets.find(EAssetType.ITEM, id) as ItemAsset;
                    if (itemAssetWithAmount == null)
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("GiveItemNotFound"), Main.Instance.MessageColor);
                        return;
                    }

                    for (int i = 25; i > 0; i--)
                    {
                        if (caller.HasPermission("ccrp.i." + i))
                        {
                            if (amount <= i)
                            {
                                if (Main.Instance.IsItemBlockedFromGive(id, player))
                                {
                                    UnturnedChat.Say(caller, Main.Instance.Translate("GiveNoBypass"), Main.Instance.MessageColor);
                                    Logger.Log($"{caller.DisplayName} ({caller.Id}) doesn't have the bypass for {id}!");
                                    return;
                                }
                                player.GiveItem(id, amount);
                                UnturnedChat.Say(caller, Main.Instance.Translate("GiveSuccessWithAmount", amount, itemAssetWithAmount.itemName, id), Main.Instance.MessageColor);
                                Logger.Log($"{caller.DisplayName} ({caller.Id}) received {amount}x {itemAssetWithAmount.itemName} ({id})");
                                return;
                            }
                        }
                        
                    }
                    
                    UnturnedChat.Say(caller, Main.Instance.Translate("GiveAmountLimit"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) tried to give themselves more items than allowed! (/give)");
                    break;

                //Case default
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("GiveSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) Didn't use /give properly!");
                    return;
            }
        }
    }
}