using CCRP_Plugin.Database;
using CCRP_Plugin.Models;
using CCRPPlugin;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;
using System.Linq;

namespace CCRP_Plugin.Commands
{
    internal class CCRPHomesCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "homes";

        public string Help => "List your homes.";

        public string Syntax => "";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.homes" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller));
        }

        private void ExecuteCore(IRocketPlayer caller)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            List<Home> homes = Main.Instance.HomeDatabase.GetHomes(player.CSteamID.m_SteamID);
            if (homes.Count == 0)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("HomeListEmpty"), Main.Instance.MessageColor);
                return;
            }

            UnturnedChat.Say(caller, Main.Instance.Translate("HomeList", homes.Count, HomeDatabase.GetMaxHomes(caller), string.Join(", ", homes.Select(h => h.Name))), Main.Instance.MessageColor);
        }
    }
}
