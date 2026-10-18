using CCRPPlugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRP_Plugin.Commands
{
    internal class CCRPFreecamCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "freecam";
        public string Help => "Toggle freecam access.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "CCRPbuild.freecam" };

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
                UnturnedChat.Say(caller, Main.Instance.Translate("FreecamSyntax"), Main.Instance.MessageColor);
                return;
            }

            bool enabled = !player.Player.look.canUseFreecam;
            player.Player.look.sendFreecamAllowed(enabled);
            string state = enabled ? "enabled" : "disabled";
            UnturnedChat.Say(caller, Main.Instance.Translate(enabled ? "FreecamEnabled" : "FreecamDisabled"), Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) {state} freecam.");
            DiscordWebhooks.DiscordBuildermodeWebhook(caller.DisplayName, caller.Id, $"Freecam {state}");
        }
    }
}