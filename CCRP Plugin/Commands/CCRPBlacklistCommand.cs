using CCRPPlugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using System.Collections.Generic;

namespace CCRP_Plugin.Commands
{
    internal class CCRPBlacklistCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "blacklist";

        public string Help => "Add or remove an item ID, item-type rule, or vehicle blacklist entry.";

        public string Syntax => "add <give|pickup|both|vehicle> <id|item-type> <bypass-permission> | remove <give|pickup|both|vehicle> <id|item-type>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.blacklist.manage" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            bool isAdd = command.Length > 0 && command[0].Equals("add", System.StringComparison.OrdinalIgnoreCase);
            bool isRemove = command.Length > 0 && command[0].Equals("remove", System.StringComparison.OrdinalIgnoreCase);
            if ((!isAdd && !isRemove) || (isAdd && command.Length != 4) || (isRemove && command.Length != 3))
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("BlacklistCommandSyntax"), Main.Instance.MessageColor);
                return;
            }

            string errorKey;
            if (isAdd && !Main.Instance.TryAddBlacklistEntry(command[1], command[2], command[3], out errorKey))
            {
                UnturnedChat.Say(caller, Main.Instance.Translate(errorKey), Main.Instance.MessageColor);
                return;
            }
            if (isRemove && !Main.Instance.TryRemoveBlacklistEntry(command[1], command[2], out errorKey))
            {
                UnturnedChat.Say(caller, Main.Instance.Translate(errorKey), Main.Instance.MessageColor);
                return;
            }

            string category = command[1].ToLowerInvariant();
            if (isAdd)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("BlacklistEntryAdded", category, command[2], command[3]), Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) added selector {command[2]} to the {category} blacklist with bypass permission {command[3]}.");
            }
            else
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("BlacklistEntryRemoved", category, command[2]), Main.Instance.MessageColor);
                Logger.Log($"{caller.DisplayName} ({caller.Id}) removed selector {command[2]} from the {category} blacklist.");
            }
        }
    }
}