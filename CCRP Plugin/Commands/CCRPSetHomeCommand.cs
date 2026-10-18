using CCRP_Plugin.Database;
using CCRP_Plugin.Models;
using CCRPPlugin;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRP_Plugin.Commands
{
    internal class CCRPSetHomeCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "sethome";

        public string Help => "Save your current position as a home. Limit is set with ccrp.sethome.<number>.";

        public string Syntax => "<name>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions { get; } = BuildPermissions();

        private static List<string> BuildPermissions()
        {
            // Rocket allows the command if the player holds any listed permission.
            List<string> permissions = new List<string> { "ccrp.sethome" };
            for (int i = 1; i <= HomeDatabase.MaxPermissionLimit; i++)
            {
                permissions.Add("ccrp.sethome." + i);
            }
            return permissions;
        }

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (command.Length != 1)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("SetHomeSyntax"), Main.Instance.MessageColor);
                return;
            }

            string name = command[0].Trim();
            if (name.Length == 0 || name.Length > 32)
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("HomeNameInvalid"), Main.Instance.MessageColor);
                return;
            }

            UnturnedPlayer player = (UnturnedPlayer)caller;
            int max = HomeDatabase.GetMaxHomes(caller);
            Home home = new Home
            {
                Steam64ID = player.CSteamID.m_SteamID,
                Name = name,
                X = player.Position.x,
                Y = player.Position.y,
                Z = player.Position.z,
                Rotation = player.Rotation
            };

            if (!Main.Instance.HomeDatabase.SetHome(home, max, out bool replaced))
            {
                UnturnedChat.Say(caller, Main.Instance.Translate("HomeLimitReached", max), Main.Instance.MessageColor);
                return;
            }

            UnturnedChat.Say(caller, Main.Instance.Translate(replaced ? "HomeUpdated" : "HomeSet", name), Main.Instance.MessageColor);
        }
    }
}
