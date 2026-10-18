using CCRPPlugin;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRP_Plugin.Commands
{
    internal class CCRPDelHomeCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "delhome";

        public string Help => "Delete one of your homes.";

        public string Syntax => "<name>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.delhome" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (command.Length != 1)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("DelHomeSyntax"), Main.Instance.MessageColor);
                return;
            }

            UnturnedPlayer player = (UnturnedPlayer)caller;
            bool deleted = Main.Instance.HomeDatabase.DeleteHome(player.CSteamID.m_SteamID, command[0]);
            UnturnedChat.Say(caller, Main.Instance.Translate(deleted ? "HomeDeleted" : "HomeNotFound", command[0]), Main.Instance.MessageColor);
        }
    }
}
