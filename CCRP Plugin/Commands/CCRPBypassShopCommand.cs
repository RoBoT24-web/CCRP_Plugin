using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal class CCRPBypassShopCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "bypassshop";
        public string Help => "List item and vehicle bypasses available for purchase.";
        public string Syntax => string.Empty;
        public List<string> Aliases => new List<string> { "bshop" };
        public List<string> Permissions => new List<string> { "ccrp.bypass.shop" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            string result = "Failed: internal error.";
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
                result = ExecuteCore(caller, command));
            DiscordWebhooks.DiscordBypassWebhook(caller.DisplayName, caller.Id, "/bypassshop", result);
        }

        private static string ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (command != null && command.Length != 0)
            {
                string syntax = Main.Instance.Translate("BypassShopSyntax");
                UnturnedChat.Say(caller, syntax, Main.Instance.MessageColor);
                return "Failed: " + syntax;
            }

            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player == null)
            {
                string error = Main.Instance.Translate("BypassShopPlayerOnly");
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return "Failed: " + error;
            }

            List<string> offers = Main.Instance.DonationBypassService.GetShopOfferLines();
            if (offers.Count == 0)
            {
                string empty = Main.Instance.Translate("BypassShopEmpty");
                UnturnedChat.Say(caller, empty, Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) checked the bypass shop; no offers are currently available.");
                return "Succeeded: " + empty;
            }

            string header = Main.Instance.Translate("BypassShopHeader");
            UnturnedChat.Say(caller, header, Main.Instance.MessageColor);
            foreach (string offer in offers)
            {
                UnturnedChat.Say(caller, offer, Main.Instance.MessageColor);
            }

            Logger.Log($"{caller.DisplayName} ({caller.Id}) viewed the bypass shop with {offers.Count} available offer(s).");
            return "Succeeded: " + header + " " + string.Join("; ", offers);
        }
    }
}
