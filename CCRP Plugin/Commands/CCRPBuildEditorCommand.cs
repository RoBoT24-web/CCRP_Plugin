using CCRPPlugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRP_Plugin.Commands
{
    internal class CCRPBuildEditorCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "editor";
        public string Help => "Toggle in-game object editor access.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "CCRPbuild.editor" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player))
            {
                return;
            }
            if (command.Length != 0)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("EditorSyntax"), Main.Instance.MessageColor);
                return;
            }

            bool enabled = !player.Player.look.canUseWorkzone;
            player.Player.look.sendWorkzoneAllowed(enabled);
            string state = enabled ? "enabled" : "disabled";
            UnturnedChat.Say(caller, Main.Instance.Translate(enabled ? "EditorEnabled" : "EditorDisabled"), Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) {state} the object editor.");
            DiscordWebhooks.DiscordBuildermodeWebhook(caller.DisplayName, caller.Id, $"Editor {state}");
        }
    }
}