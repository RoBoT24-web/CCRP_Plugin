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
    internal class CCRPRemoveWarnCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "removewarn";

        public string Help => "";

        public string Syntax => "<name> [Reason]";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.removewarn" };
        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            switch (command.Length)
            {
                case 2:
                    UnturnedPlayer target = UnturnedPlayer.FromName(command[0]);
                    if (target == null)
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("PlayerNotFound"), Main.Instance.MessageColor);
                        return;
                    }
                    string targetId = target.Id;
                    int warnAmount = Main.Instance.WarnService.WarnAmountCheck(targetId);

                    if (warnAmount == 0)
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("RemoveWarnNoWarns"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) player has no Warns!");
                        return;
                    }
                    Main.Instance.WarnService.RegisterWarnRemove(targetId, warnAmount);
                    Main.Instance.WarnService.RemoveOldWarn(targetId);

                    Main.Instance.WarnLogService.OnWarnRemove(caller.DisplayName, caller.Id, target.DisplayName, targetId, warnAmount);

                    UnturnedChat.Say(caller, Main.Instance.Translate("RemoveWarnSuccess", target.DisplayName, target.Id, command[1]), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) removed warn from {target.DisplayName} ({target.Id})!");

                    DiscordWebhooks.DiscordWarnWebHook(caller.DisplayName, caller.Id, target.DisplayName, target.Id, command[1], warnAmount, "remove");

                    return;
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("RemoveWarnSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't use /removewarn properly!");
                    break;
            }
        }
    }
}
