using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal class CCRPKickCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "kick";

        public string Help => "";

        public string Syntax => "<name> [reason]";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.kick" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {

            switch (command.Length)
            {
                //Case kick
                case 2:
                    UnturnedPlayer playerToKick = UnturnedPlayer.FromName(command[0]);
                    if (playerToKick != null)
                    {
                        playerToKick.Kick(command[1]);

                        DiscordWebhooks.DiscordKickWebhook(caller.DisplayName, caller.Id, playerToKick.DisplayName, playerToKick.Id, command[1]); 

                        UnturnedChat.Say(caller, Main.Instance.Translate("KickSuccess", playerToKick.DisplayName, playerToKick.CSteamID, command[1]), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) kicked {playerToKick.DisplayName} ({playerToKick.Id}) ({command[1]})!");
                    }
                    else
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("PlayerNotFound"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't find a player (/kick)!");
                    }
                    return;

                //Default case
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("KickSyntax"));
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't use /kick properly!");
                    return;
            }
            
        }
    }
}
