using CCRPPlugin;
using Rocket.API;
using Rocket.API.Serialisation;
using Rocket.Core;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CCRP_Plugin.Commands
{
    internal class CCRPDutyCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "duty";

        public string Help => "";

        public string Syntax => "";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.duty" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            UnturnedPlayer player = (UnturnedPlayer)caller;
            player.Admin(false);
            foreach (DutyGroups Group in Main.Instance.ValidGroups)
            {
                if (caller.HasPermission(Group.Permission))
                {
                    DoDuty(player, Group);
                    return;
                }
            }
        }

        public void DoDuty(UnturnedPlayer player, DutyGroups group)
        {
            RocketPermissionsGroup target = R.Permissions.GetGroup(group.GroupID);

            if(target.Members.Contains(player.CSteamID.ToString())) 
            {
                R.Permissions.RemovePlayerFromGroup(target.Id, player);
                player.Features.GodMode = false;
                player.Features.VanishMode = false;
                player.Admin(false);
                UnturnedChat.Say(Main.Instance.Translate("DutyOff", player.DisplayName, target.Prefix), Main.Instance.MessageColor);
                return;
            }
            R.Permissions.AddPlayerToGroup(target.Id, player);
            UnturnedChat.Say(Main.Instance.Translate("DutyOn", player.DisplayName, target.Prefix), Main.Instance.MessageColor);
            return;
        }
    }
}
