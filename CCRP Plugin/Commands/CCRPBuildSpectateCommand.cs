using CCRPPlugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRP_Plugin.Commands
{
    internal class CCRPBuildSpectateCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "spectate";
        public string Help => "Toggle player-name and map visibility.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "CCRPbuild.spectate" };

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
                UnturnedChat.Say(caller, Main.Instance.Translate("SpectateSyntax"), Main.Instance.MessageColor);
                return;
            }

            bool enabled = !player.Player.look.canUseSpecStats;
            player.Player.look.sendSpecStatsAllowed(enabled);
            string state = enabled ? "enabled" : "disabled";
            UnturnedChat.Say(caller, Main.Instance.Translate(enabled ? "SpectateEnabled" : "SpectateDisabled"), Main.Instance.MessageColor);
            Logger.Log($"{caller.DisplayName} ({caller.Id}) {state} player name and map visibility.");
            DiscordWebhooks.DiscordBuildermodeWebhook(caller.DisplayName, caller.Id, $"Spectate {state}");
        }
    }
}