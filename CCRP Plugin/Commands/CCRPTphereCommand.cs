using CCRPPlugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCRP_Plugin.Commands
{
    internal class CCRPTphereCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "tphere";

        public string Help => "";

        public string Syntax => "<name>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.tphere" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer callerPlayer = (UnturnedPlayer)caller;

            switch (command.Length)
            {
                case 1:
                    UnturnedPlayer otherPlayer = UnturnedPlayer.FromName(command[0]);
                    if (otherPlayer == null)
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("PlayerNotFound"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't find a player (/tphere)!");
                        return;
                    }
                    otherPlayer.Player.teleportToPlayer(callerPlayer.Player);

                    UnturnedChat.Say(caller, Main.Instance.Translate("TphereSuccess", otherPlayer.DisplayName), Main.Instance.MessageColor);
                    UnturnedChat.Say(otherPlayer, Main.Instance.Translate("TohereOtherPlayer", caller.DisplayName), Main.Instance.MessageColor);

                    Logger.Log($"{caller.DisplayName} ({caller.Id}) teleported {otherPlayer.DisplayName} ({otherPlayer.Id}) to themselves!");

                    string x = callerPlayer.Position.x.ToString();
                    string y = callerPlayer.Position.y.ToString();
                    string z = callerPlayer.Position.z.ToString();

                    DiscordWebhooks.DiscordTeleportWebhook(caller.DisplayName, caller.Id, "tphere", otherPlayer.DisplayName, otherPlayer.Id, x, y, z);

                    return;
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("TphereSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't use /tphere properly!");
                    return;
            }
        }
    }
}
