using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal class CCRPGodCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "god";

        public string Help => "";

        public string Syntax => "<Name>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.god" };

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
                    UnturnedPlayer playerToGodSelf = (UnturnedPlayer)caller;
                    if(playerToGodSelf.GodMode == true)
                    {
                        playerToGodSelf.Features.GodMode = false;
                        UnturnedChat.Say(caller, Main.Instance.Translate("GodDisableSelf"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) disabled Godmode for themselves!");
                        return;
                    }
                    else
                    {
                        playerToGodSelf.Features.GodMode = true;
                        UnturnedChat.Say(caller, Main.Instance.Translate("GodEnableSelf"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) enabled Godmode for themselves!");
                        return;
                    }

                case 1:
                    if(caller.HasPermission("ccrp.god.others"))
                    {
                        UnturnedPlayer targetPlayer = UnturnedPlayer.FromName(command[0]);
                        if (targetPlayer == null)
                        {
                            UnturnedChat.Say(caller, Main.Instance.Translate("PlayerNotFound"), Main.Instance.MessageColor);
                            return;
                        }

                        if (targetPlayer.GodMode == true)
                        {
                            targetPlayer.Features.GodMode = false;
                            UnturnedChat.Say(caller, Main.Instance.Translate("GodDisableOther", targetPlayer.DisplayName, targetPlayer.Id), Main.Instance.MessageColor);
                            Logger.Log($"{caller.DisplayName} ({caller.Id}) disabled Godmode for {targetPlayer.DisplayName} ({targetPlayer.Id})!");
                            return;
                        }
                        else
                        {
                            targetPlayer.Features.GodMode = true;
                            UnturnedChat.Say(caller, Main.Instance.Translate("GodEnabledOther", targetPlayer.DisplayName, targetPlayer.Id), Main.Instance.MessageColor);
                            Logger.Log($"{caller.DisplayName} ({caller.Id}) enabled Godmode for {targetPlayer.DisplayName} ({targetPlayer.Id})!");
                            return;
                        }
                    } else
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("NoPerms"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't have enough permissions for /god");
                        return;
                    }
                    
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("GodSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't use /god properly!");
                    return;
            } 
        }
    }
}
