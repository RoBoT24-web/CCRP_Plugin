using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal class CCRPVanishCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "vanish";

        public string Help => "";

        public string Syntax => "<Name>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.vanish" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (!(caller is UnturnedPlayer player))
                return;

            switch (command.Length)
            {
                case 0:
                    UnturnedPlayer playerToVanishSelf = (UnturnedPlayer)caller;

                    if(playerToVanishSelf.VanishMode == true)
                    {
                        playerToVanishSelf.Features.VanishMode = false;
                        UnturnedChat.Say(caller, Main.Instance.Translate("VanishDisableSelf"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) disabled Vanish for themselves!");
                        return;
                    }
                    else
                    {
                        playerToVanishSelf.Features.VanishMode = true;
                        UnturnedChat.Say(caller, Main.Instance.Translate("VanishEnableSelf"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) enabled Vanish for themselves!");
                        return;
                    }

                case 1:
                    if (caller.HasPermission("ccrp.vanish.others"))
                    {
                        UnturnedPlayer targetPlayer = UnturnedPlayer.FromName(command[0]);
                        if (targetPlayer == null)
                        {
                            UnturnedChat.Say(caller, Main.Instance.Translate("PlayerNotFound"), Main.Instance.MessageColor);
                            return;
                        }
                        if (targetPlayer.Features.VanishMode == true)
                        {
                            targetPlayer.Features.VanishMode = false;
                            UnturnedChat.Say(caller, Main.Instance.Translate("VanishDisableOther", targetPlayer.DisplayName, targetPlayer.Id), Main.Instance.MessageColor);
                            Logger.Log($"{caller.DisplayName} ({caller.Id}) disabled Vanish for {targetPlayer.DisplayName} ({targetPlayer.Id})!");
                            return;
                        }
                        else
                        {
                            targetPlayer.Features.VanishMode = true;
                            UnturnedChat.Say(caller, Main.Instance.Translate("VanishEnabledOther", targetPlayer.DisplayName, targetPlayer.Id), Main.Instance.MessageColor);
                            Logger.Log($"{caller.DisplayName} ({caller.Id}) enabled Vanish for {targetPlayer.DisplayName} ({targetPlayer.Id})!");
                            return;
                        }
                    }
                    else
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("NoPerms"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't have enough permissions for /vanish");
                        return;
                    }

                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("VanishSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't use /vanish properly!");
                    return;
            }
        }
    }
}
