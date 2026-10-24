using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal class CCRPBypassesCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "bypasses";
        public string Help => "Show your bypass usage and claimed item and vehicle bypasses.";
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

            long donorUsed;
            long donorMaximum;
            int paidUsed;
            int paidMaximum;
            if (!Main.Instance.DonationBypassService.TryGetPlayerBypassUsage(
                player,
                out donorUsed,
                out donorMaximum,
                out paidUsed,
                out paidMaximum,
                out errorKey))
            {
                string error = Main.Instance.Translate(errorKey);
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return "Failed: " + error;
            }

            string usageMessage = Main.Instance.Translate("BypassesUsage", donorUsed, donorMaximum, paidUsed, paidMaximum);
            UnturnedChat.Say(caller, usageMessage, Main.Instance.MessageColor);
            if (bypasses.Count == 0)
            {
                string emptyMessage = Main.Instance.Translate("BypassesEmpty");
                UnturnedChat.Say(caller, emptyMessage, Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) checked their bypasses: {donorUsed}/{donorMaximum} donor and {paidUsed}/{paidMaximum} paid used; none owned.");
                return "Succeeded: " + usageMessage + " " + emptyMessage;
            }

            string message = Main.Instance.Translate("BypassesList", string.Join(", ", bypasses));
            UnturnedChat.Say(caller, message, Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) checked their bypasses: {donorUsed}/{donorMaximum} donor and {paidUsed}/{paidMaximum} paid used. {string.Join(", ", bypasses)}.");
            return "Succeeded: " + usageMessage + " " + message;
        }
    }
}