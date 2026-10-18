using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CCRPPlugin.Commands
{
    internal class CCRPBypassAdminCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "bypassadmin";
        public string Help => "Manage a player's personal donation bypass group.";
        public string Syntax => "list <Steam64ID> | add <Steam64ID> <item|vehicle> <ID> | remove <Steam64ID> <item|vehicle> <ID> | delete <Steam64ID> confirm";
        public List<string> Aliases => new List<string> { "badm" };
        public List<string> Permissions => new List<string> { "ccrp.bypasses.admin" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            string commandText = command == null || command.Length == 0
                ? "/bypassadmin"
                : "/bypassadmin " + string.Join(" ", command);
            string result = "Failed: internal error.";
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
                result = ExecuteCore(caller, command));
            DiscordWebhooks.DiscordBypassWebhook(caller.DisplayName, caller.Id, commandText, result);
        }

        private static string ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (command == null || command.Length < 2)
            {
                return ShowError(caller, "BypassAdminSyntax");
            }

            ulong playerIdValue;
            if (!ulong.TryParse(command[1], NumberStyles.None, CultureInfo.InvariantCulture, out playerIdValue) ||
                playerIdValue == 0)
            {
                return ShowError(caller, "BypassAdminInvalidPlayer");
            }

            string playerId = playerIdValue.ToString(CultureInfo.InvariantCulture);
            string action = command[0].ToLowerInvariant();
            if (action == "list" && command.Length == 2)
            {
                string errorKey;
                List<string> bypasses = Main.Instance.DonationBypassService.GetPersonalGroupBypasses(playerId, out errorKey);
                if (bypasses == null)
                {
                    return ShowError(caller, errorKey, playerId);
                }

                if (bypasses.Count == 0)
                {
                    return ShowResult(caller, "BypassAdminListEmpty", playerId);
                }

                string header = Main.Instance.Translate("BypassAdminListHeader", playerId);
                UnturnedChat.Say(caller, header, Main.Instance.MessageColor);
                foreach (string bypass in bypasses)
                {
                    UnturnedChat.Say(caller, bypass, Main.Instance.MessageColor);
                }

                Logger.Log($"{caller.DisplayName} ({caller.Id}) viewed bypasses for {playerId}: {string.Join(", ", bypasses)}.");
                return "Succeeded: " + header + " " + string.Join("; ", bypasses);
            }

            if ((action == "add" || action == "remove") && command.Length == 4)
            {
                ushort assetId;
                if (!ushort.TryParse(command[3], NumberStyles.None, CultureInfo.InvariantCulture, out assetId) ||
                    assetId == 0)
                {
                    return ShowError(caller, "BypassAdminInvalidAssetId");
                }

                string bypassPermission;
                string errorKey;
                if (!Main.Instance.DonationBypassService.TryGetBypassPermission(command[2], assetId, out bypassPermission, out errorKey))
                {
                    return ShowError(caller, errorKey);
                }

                bool succeeded = action == "add"
                    ? Main.Instance.DonationBypassService.TryAdminAddBypass(playerId, bypassPermission, out errorKey)
                    : Main.Instance.DonationBypassService.TryAdminRemoveBypass(playerId, bypassPermission, out errorKey);
                if (!succeeded)
                {
                    return ShowError(caller, errorKey, bypassPermission);
                }

                string message = Main.Instance.Translate(
                    action == "add" ? "BypassAdminAdded" : "BypassAdminRemoved",
                    command[2].ToLowerInvariant(), assetId, playerId, bypassPermission);
                UnturnedChat.Say(caller, message, Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) {action} bypass {bypassPermission} for {playerId}.");
                return "Succeeded: " + message;
            }

            if (action == "delete" && command.Length == 3 &&
                command[2].Equals("confirm", StringComparison.OrdinalIgnoreCase))
            {
                string errorKey;
                if (!Main.Instance.DonationBypassService.TryAdminDeletePersonalGroup(playerId, out errorKey))
                {
                    return ShowError(caller, errorKey, playerId);
                }

                string message = Main.Instance.Translate("BypassAdminGroupDeleted", playerId);
                UnturnedChat.Say(caller, message, Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) deleted the personal bypass group for {playerId}.");
                return "Succeeded: " + message;
            }

            return ShowError(caller, "BypassAdminSyntax");
        }

        private static string ShowError(IRocketPlayer caller, string translationKey, params object[] arguments)
        {
            string message = Main.Instance.Translate(translationKey, arguments);
            UnturnedChat.Say(caller, message, Main.Instance.MessageColor);
            return "Failed: " + message;
        }

        private static string ShowResult(IRocketPlayer caller, string translationKey, params object[] arguments)
        {
            string message = Main.Instance.Translate(translationKey, arguments);
            UnturnedChat.Say(caller, message, Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) {message}");
            return "Succeeded: " + message;
        }
    }
}
