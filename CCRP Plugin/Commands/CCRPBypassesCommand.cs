using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;
using System.Linq;

namespace CCRPPlugin.Commands
{
    internal class CCRPBypassesCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "bypasses";
        public string Help => "Show your claimed item and vehicle bypasses.";
        public string Syntax => string.Empty;
        public List<string> Aliases => new List<string> { "bypasslist" };
        public List<string> Permissions => new List<string> { "ccrp.bypass.shop" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            string result = "Failed: internal error.";
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
                result = ExecuteCore(caller));
            DiscordWebhooks.DiscordBypassWebhook(caller.DisplayName, caller.Id, "/bypasses", result);
        }

        private static string ExecuteCore(IRocketPlayer caller)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player == null)
            {
                string error = Main.Instance.Translate("BypassesPlayerOnly");
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return "Failed: " + error;
            }

            string errorKey;
            List<string> bypasses = Main.Instance.DonationBypassService.GetPlayerBypasses(player, out errorKey);
            if (bypasses == null)
            {
                string error = Main.Instance.Translate(errorKey);
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return "Failed: " + error;
            }

            if (bypasses.Count == 0)
            {
                string emptyMessage = Main.Instance.Translate("BypassesEmpty");
                UnturnedChat.Say(caller, emptyMessage, Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) checked their bypasses; none claimed.");
                return "Succeeded: " + emptyMessage;
            }

            string message = Main.Instance.Translate("BypassesList", string.Join(", ", bypasses));
            UnturnedChat.Say(caller, message, Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) checked their bypasses: {string.Join(", ", bypasses)}.");
            return "Succeeded: " + message;
        }
    }
}
