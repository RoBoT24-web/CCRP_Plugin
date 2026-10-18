using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;
using System.Linq;

namespace CCRPPlugin.Commands
{
    internal class CCRPBypassGroupCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "bypassgroup";
        public string Help => "Create or customize your personal donation bypass group.";
        public string Syntax => "create <display name> <RRGGBB> | name <display name> | color <RRGGBB>";
        public List<string> Aliases => new List<string> { "bg" };
        public List<string> Permissions => new List<string> { "ccrp.bypassgroup" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            string commandText = command == null || command.Length == 0
                ? "/bypassgroup"
                : "/bypassgroup " + string.Join(" ", command);
            string result = "Failed: internal error.";
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => result = ExecuteCore(caller, command));
            DiscordWebhooks.DiscordBypassWebhook(caller.DisplayName, caller.Id, commandText, result);
        }

        private string ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = caller as UnturnedPlayer;
            if (player == null || command == null || command.Length == 0)
            {
                ShowSyntax(caller);
                return "Failed: invalid command syntax.";
            }

            string action = command[0].ToLowerInvariant();
            string errorKey;
            bool success;
            string successKey;
            if (action == "create" && command.Length >= 3)
            {
                string displayName = string.Join(" ", command.Skip(1).Take(command.Length - 2));
                success = Main.Instance.DonationBypassService.TryCreateGroup(player, displayName, command[command.Length - 1], out errorKey);
                successKey = "BypassGroupCreated";
            }
            else if (action == "name" && command.Length >= 2)
            {
                string displayName = string.Join(" ", command.Skip(1));
                success = Main.Instance.DonationBypassService.TrySetGroupName(player, displayName, out errorKey);
                successKey = "BypassGroupNameUpdated";
            }
            else if (action == "color" && command.Length == 2)
            {
                success = Main.Instance.DonationBypassService.TrySetGroupColor(player, command[1], out errorKey);
                successKey = "BypassGroupColorUpdated";
            }
            else
            {
                ShowSyntax(caller);
                return "Failed: invalid command syntax.";
            }

            if (!success)
            {
                string error = Main.Instance.Translate(errorKey);
                UnturnedChat.Say(caller, error, Main.Instance.MessageColor);
                return "Failed: " + error;
            }

            string successMessage = Main.Instance.Translate(successKey);
            UnturnedChat.Say(caller, successMessage, Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) used /{Name} {action}.");
            return "Succeeded: " + successMessage;
        }

        private static void ShowSyntax(IRocketPlayer caller)
        {
            UnturnedChat.Say(caller, Main.Instance.Translate("BypassGroupSyntax"), Main.Instance.MessageColor);
        }
    }
}
