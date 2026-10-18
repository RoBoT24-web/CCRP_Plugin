using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;
using System.Globalization;

namespace CCRPPlugin.Commands
{
    internal class CCRPBypassItemCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "bypassitem";
        public string Help => "Add or remove an eligible item bypass in your personal group.";
        public string Syntax => "<item ID> | confirm | cancel";
        public List<string> Aliases => new List<string> { "bi" };
        public List<string> Permissions => new List<string> { "ccrp.bypassitem" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            string commandText = command == null || command.Length == 0
                ? "/bypassitem"
                : "/bypassitem " + string.Join(" ", command);
            string result = "Failed: internal error.";
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => result = ExecuteCore(caller, command));
            DiscordWebhooks.DiscordBypassWebhook(caller.DisplayName, caller.Id, commandText, result);
        }

        private string ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player != null && command != null && command.Length == 1 &&
                command[0].Equals("confirm", System.StringComparison.OrdinalIgnoreCase))
            {
                ushort removedItemId;
                string confirmationError;
                if (!Main.Instance.DonationBypassService.TryConfirmItemRemoval(player, out removedItemId, out confirmationError))
                {
                    string error = Main.Instance.Translate(confirmationError);
                    UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                    return "Failed: " + error;
                }

                string removedMessage = Main.Instance.Translate("BypassRemoved", "item", removedItemId);
                UnturnedChat.Say(caller, removedMessage, Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) removed item bypass for {removedItemId}.");
                return "Succeeded: " + removedMessage;
            }

            if (player != null && command != null && command.Length == 1 &&
                command[0].Equals("cancel", System.StringComparison.OrdinalIgnoreCase))
            {
                string cancelMessage = Main.Instance.Translate(
                    Main.Instance.DonationBypassService.TryCancelItemRemoval(player)
                        ? "BypassRemovalCancelled"
                        : "BypassRemovalNoRequest");
                UnturnedChat.Say(caller, cancelMessage, Main.Instance.MessageColor);
                return "Result: " + cancelMessage;
            }

            ushort itemId;
            if (player == null || command == null || command.Length != 1 ||
                !ushort.TryParse(command[0], NumberStyles.None, CultureInfo.InvariantCulture, out itemId) || itemId == 0)
            {
                string error = Main.Instance.Translate("BypassItemSyntax");
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return "Failed: " + error;
            }

            string errorKey;
            if (!Main.Instance.DonationBypassService.TryClaimItem(player, itemId, out errorKey))
            {
                string error = errorKey == "BypassItemRemoveConfirmation"
                    ? Main.Instance.Translate(errorKey, itemId)
                    : Main.Instance.Translate(errorKey);
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return errorKey == "BypassItemRemoveConfirmation"
                    ? "Confirmation requested: " + error
                    : "Failed: " + error;
            }

            string successMessage = Main.Instance.Translate("BypassClaimed", "item", itemId);
            UnturnedChat.Say(caller, successMessage, Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) claimed item bypass for {itemId}.");
            return "Succeeded: " + successMessage;
        }
    }
}
