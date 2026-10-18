using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;
using System.Globalization;

namespace CCRPPlugin.Commands
{
    internal class CCRPBypassVehicleCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "bypassvehicle";
        public string Help => "Add or remove an eligible vehicle bypass in your personal group.";
        public string Syntax => "<vehicle ID> | confirm | cancel";
        public List<string> Aliases => new List<string> { "bv" };
        public List<string> Permissions => new List<string> { "ccrp.bypassvehicle" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            string commandText = command == null || command.Length == 0
                ? "/bypassvehicle"
                : "/bypassvehicle " + string.Join(" ", command);
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
                ushort removedVehicleId;
                string confirmationError;
                if (!Main.Instance.DonationBypassService.TryConfirmVehicleRemoval(player, out removedVehicleId, out confirmationError))
                {
                    string error = Main.Instance.Translate(confirmationError);
                    UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                    return "Failed: " + error;
                }

                string removedMessage = Main.Instance.Translate("BypassRemoved", "vehicle", removedVehicleId);
                UnturnedChat.Say(caller, removedMessage, Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) removed vehicle bypass for {removedVehicleId}.");
                return "Succeeded: " + removedMessage;
            }

            if (player != null && command != null && command.Length == 1 &&
                command[0].Equals("cancel", System.StringComparison.OrdinalIgnoreCase))
            {
                string cancelMessage = Main.Instance.Translate(
                    Main.Instance.DonationBypassService.TryCancelVehicleRemoval(player)
                        ? "BypassRemovalCancelled"
                        : "BypassRemovalNoRequest");
                UnturnedChat.Say(caller, cancelMessage, Main.Instance.MessageColor);
                return "Result: " + cancelMessage;
            }

            ushort vehicleId;
            if (player == null || command == null || command.Length != 1 ||
                !ushort.TryParse(command[0], NumberStyles.None, CultureInfo.InvariantCulture, out vehicleId) || vehicleId == 0)
            {
                string error = Main.Instance.Translate("BypassVehicleSyntax");
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return "Failed: " + error;
            }

            string errorKey;
            if (!Main.Instance.DonationBypassService.TryClaimVehicle(player, vehicleId, out errorKey))
            {
                string error = errorKey == "BypassVehicleRemoveConfirmation"
                    ? Main.Instance.Translate(errorKey, vehicleId)
                    : Main.Instance.Translate(errorKey);
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return errorKey == "BypassVehicleRemoveConfirmation"
                    ? "Confirmation requested: " + error
                    : "Failed: " + error;
            }

            string successMessage = Main.Instance.Translate("BypassClaimed", "vehicle", vehicleId);
            UnturnedChat.Say(caller, successMessage, Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) claimed vehicle bypass for {vehicleId}.");
            return "Succeeded: " + successMessage;
        }
    }
}
