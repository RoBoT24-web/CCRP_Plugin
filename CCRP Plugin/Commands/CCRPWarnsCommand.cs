using CCRPPlugin;
using CCRPPlugin.Database;
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
    internal class CCRPWarnsCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "warns";

        public string Help => "";

        public string Syntax => "<name>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.warns" };
        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {

            switch (command.Length)
            {
                case 0:
                    string idToCheck = caller.Id;
                    Warns currentWarnings = Main.Instance.WarnService.GetPlayerWarning(idToCheck);
                    int warnAmount = currentWarnings == null ? 0 : currentWarnings.WarnAmount;
                    string reason = GetReason(currentWarnings);

                    UnturnedChat.Say(caller, Main.Instance.Translate("WarnsSuccess", warnAmount, reason), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) used /warns!");
                    return;

                case 1:
                    if(caller.HasPermission("ccrp.warns.other"))
                    {
                        UnturnedPlayer playerToCheck = UnturnedPlayer.FromName(command[0]);
                        if(playerToCheck == null)
                        {
                            UnturnedChat.Say(caller, Main.Instance.Translate("PlayerNotFound"), Main.Instance.MessageColor);
                            return;
                        }
                        Warns targetWarnings = Main.Instance.WarnService.GetPlayerWarning(playerToCheck.Id);
                        int targetWarnAmount = targetWarnings == null ? 0 : targetWarnings.WarnAmount;
                        string targetReason = GetReason(targetWarnings);
                        UnturnedChat.Say(caller, Main.Instance.Translate("WarnsSuccessOther", playerToCheck.DisplayName, playerToCheck.Id, targetWarnAmount, targetReason), Main.Instance.MessageColor);
                    } else
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("NoPerms"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't have enough permissions for /warns!");
                    }
                    return;
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("WarnsSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't use /warns properly!");

                    return;
            }
            
        }

        private static string GetReason(Warns warning)
        {
            if (warning == null || warning.WarnAmount <= 0 || string.IsNullOrWhiteSpace(warning.WarnReason))
            {
                return Main.Instance.Translate("NoActiveWarnReason");
            }

            return warning.WarnReason;
        }
    }
}
