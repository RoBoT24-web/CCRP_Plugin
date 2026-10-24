using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using System.Collections.Generic;
using System.Globalization;

namespace CCRPPlugin.Commands
{
    internal class CCRPBypassCostCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "bypasscost";
        public string Help => "Check the in-game cost of a bypass shop offer.";
        public string Syntax => "<ID> | <item|vehicle> <ID>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.bypass.shop" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            string commandText = command == null || command.Length == 0
                ? "/bypasscost"
                : "/bypasscost " + string.Join(" ", command);
            string result = "Failed: internal error.";
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
                result = ExecuteCore(caller, command));
            DiscordWebhooks.DiscordBypassWebhook(caller.DisplayName, caller.Id, commandText, result);
        }

        private static string ExecuteCore(IRocketPlayer caller, string[] command)
        {
            string type = null;
            string idText = null;
            if (command != null && command.Length == 1)
            {
                idText = command[0];
            }
            else if (command != null && command.Length == 2 &&
                (command[0].Equals("item", System.StringComparison.OrdinalIgnoreCase) ||
                 command[0].Equals("vehicle", System.StringComparison.OrdinalIgnoreCase)))
            {
                type = command[0];
                idText = command[1];
            }

            ushort assetId;
            if (idText == null ||
                !ushort.TryParse(idText, NumberStyles.None, CultureInfo.InvariantCulture, out assetId) ||
                assetId == 0)
            {
                string syntax = Main.Instance.Translate("BypassCostSyntax");
                UnturnedChat.Say(caller, syntax, Main.Instance.MessageColor);
                return "Failed: " + syntax;
            }

            decimal cost;
            string offerType;
            string errorKey;
            if (!Main.Instance.DonationBypassService.TryGetShopCost(type, assetId, out cost, out offerType, out errorKey))
            {
                string error = errorKey == "BypassShopOfferAmbiguous" && string.IsNullOrWhiteSpace(type)
                    ? Main.Instance.Translate("BypassShopCostAmbiguous")
                    : Main.Instance.Translate(errorKey);
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return "Failed: " + error;
            }

            string message = Main.Instance.Translate(
                "BypassShopCost",
                offerType.ToLowerInvariant(),
                assetId,
                cost.ToString("0.##", CultureInfo.InvariantCulture));
            UnturnedChat.Say(caller, message, Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) checked the {offerType.ToLowerInvariant()} bypass cost for ID {assetId}: {cost.ToString(CultureInfo.InvariantCulture)}.");
            return "Succeeded: " + message;
        }
    }
}
