using CCRP_Plugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal class CCRPUnbanCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "unban";

        public string Help => "";

        public string Syntax => "<Steam64ID>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.unban" };


        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            switch (command.Length)
            {
                //Case success
                case 2:
                    if (ulong.TryParse(command[0], out ulong ulongToUnban))
                    {
                        CSteamID playerToUnban;
                        
                        if (caller is UnturnedPlayer)
                        {
                            UnturnedPlayer investigator = (UnturnedPlayer)caller;

                            CSteamID callerID = investigator.CSteamID;
                            playerToUnban = (CSteamID)ulongToUnban;

                            Provider.requestUnbanPlayer(callerID, playerToUnban);
                            DiscordWebhooks.DiscordUnbanWebhook(caller.DisplayName, caller.Id, playerToUnban.m_SteamID, command[1]);
                        } else
                        {
                            playerToUnban = (CSteamID)ulongToUnban;
                            Provider.requestUnbanPlayer(CSteamID.Nil, playerToUnban);
                            DiscordWebhooks.DiscordUnbanWebhook("Console", "Console", playerToUnban.m_SteamID, command[1]);
                        }
                        
                        UnturnedChat.Say(caller, Main.Instance.Translate("UnbanSuccess", playerToUnban, command[1]), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) unbanned {ulongToUnban} ({command[1]})!");
                    } else
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("UnbanSyntax"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) didnt't use /unban properly!");
                    }
                    return;
                //Case default
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("UnbanSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didnt't use /unban properly!" );
                    return;
            }
        }
        
    }
}
