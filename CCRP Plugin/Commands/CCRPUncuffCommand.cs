using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    public sealed class CCRPUncuffCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "uncuff";
        public string Help => "Remove a player's handcuffs.";
        public string Syntax => "<player>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.uncuff" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (command == null || command.Length != 1)
            {
                UnturnedChat.Say(caller, "Usage: /uncuff <player>");
                return;
            }

            UnturnedPlayer target = UnturnedPlayer.FromName(command[0]);
            if (target == null || target.Player == null)
            {
                UnturnedChat.Say(caller, "Player not found.");
                return;
            }

            PlayerAnimator animator = target.Player.animator;
            if (animator == null || animator.captorID.m_SteamID == 0)
            {
                UnturnedChat.Say(caller, target.DisplayName + " is not cuffed.");
                return;
            }

            animator.captorID = Steamworks.CSteamID.Nil;
            animator.captorItem = 0;
            animator.captorStrength = 0;
            animator.sendGesture(EPlayerGesture.ARREST_STOP, false);

            UnturnedChat.Say(caller, "Uncuffed " + target.DisplayName + ".");
            UnturnedChat.Say(target, "You have been uncuffed.");
        }
    }
}
