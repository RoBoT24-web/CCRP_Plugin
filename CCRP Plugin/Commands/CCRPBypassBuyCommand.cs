using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;
using System.Globalization;

namespace CCRPPlugin.Commands
{
    internal class CCRPBypassBuyCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "bypassbuy";
        public string Help => "Buy a listed item or vehicle bypass with in-game money.";
        public string Syntax => "<item|vehicle> <ID>";
        public List<string> Aliases => new List<string> { "bbuy" };
        public List<string> Permissions => new List<string> { "ccrp.bypass.shop" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            string commandText = command == null || command.Length == 0
                ? "/bypassbuy"
                : "/bypassbuy " + string.Join(" ", command);
            string result = "Failed: internal error.";
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
                result = ExecuteCore(caller, command));
            DiscordWebhooks.DiscordBypassWebhook(caller.DisplayName, caller.Id, commandText, result);
        }

        private static string ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            ushort assetId;
            if (player == null || command == null || command.Length != 2 ||
                (!command[0].Equals("item", System.StringComparison.OrdinalIgnoreCase) &&
                 !command[0].Equals("vehicle", System.StringComparison.OrdinalIgnoreCase)) ||
                !ushort.TryParse(command[1], NumberStyles.None, CultureInfo.InvariantCulture, out assetId) ||
                assetId == 0)
            {
                string syntax = Main.Instance.Translate("BypassShopSyntax");
                UnturnedChat.Say(caller, syntax, Main.Instance.MessageColor);
                return "Failed: " + syntax;
            }

            decimal cost;
            bool alreadyOwned;
            string errorKey;
            if (!Main.Instance.DonationBypassService.TryPurchase(
                player,
                command[0],
                assetId,
                out cost,
                out alreadyOwned,
                out errorKey))
            {
                string error = Main.Instance.Translate(errorKey);
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return "Failed: " + error;
            }

            string message = alreadyOwned
                ? Main.Instance.Translate("BypassShopAlreadyPurchased")
                : Main.Instance.Translate(
                    "BypassShopPurchased",
                    command[0].ToLowerInvariant(),
                    assetId,
                    cost.ToString("0.##", CultureInfo.InvariantCulture));
            UnturnedChat.Say(caller, message, Main.Instance.MessageColor);
            Logger.Log(alreadyOwned
                ? $"{caller.DisplayName} ({caller.Id}) already owned {command[0].ToLowerInvariant()} bypass {assetId}; no charge taken."
                : $"{caller.DisplayName} ({caller.Id}) purchased {command[0].ToLowerInvariant()} bypass {assetId} for {cost.ToString(CultureInfo.InvariantCulture)}.");
            return "Succeeded: " + message;
        }
    }
}
