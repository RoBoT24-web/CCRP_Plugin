using CCRPPlugin;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CCRP_Plugin.Commands
{
    internal class CCRPWarnCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "warn";

        public string Help => "";

        public string Syntax => "<name> [Reason]";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.warn" };
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
                    int warnAmount;

                    if(Main.Instance.WarnService.WarnDoesExist(targetId))
                    {
                        warnAmount = Main.Instance.WarnService.WarnAmountCheck(targetId);
                        Main.Instance.WarnService.RemoveOldWarn(targetId);
                    } else
                    {
                        Main.Instance.WarnService.CreateWarn(targetId);
                        warnAmount = Main.Instance.WarnService.WarnAmountCheck(targetId);
                        Main.Instance.WarnService.RemoveOldWarn(targetId);
                    }
                    
                    Main.Instance.WarnService.RegisterWarn(targetId, command[1], warnAmount);

                    Main.Instance.WarnLogService.OnWarnAdd(caller.DisplayName, caller.Id, target.DisplayName, target.Id, warnAmount);

                    UnturnedChat.Say(caller, Main.Instance.Translate("WarnSuccess", target.DisplayName, target.Id, command[1]), Main.Instance.MessageColor);
                    UnturnedChat.Say(target, Main.Instance.Translate("WarnSuccessOther", caller.DisplayName, command[1]), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) warned {target.DisplayName} ({target.Id})!");

                    warnAmount = Main.Instance.WarnService.WarnAmountCheck(targetId);
                    WarnPunishment punishment = Main.Instance.Configuration.Instance.WarnPunishments.FirstOrDefault(x => x.WarnCount == warnAmount);
                    if (punishment != null)
                    {
                        if (punishment.Action == WarnPunishmentAction.Kick)
                        {
                            target.Kick(punishment.Reason);
                            Logger.Log($"{target.DisplayName} ({target.Id}) got kicked because of {warnAmount} warnings!");
                            DiscordWebhooks.DiscordKickWebhook(caller.DisplayName, caller.Id, target.DisplayName, target.Id, punishment.Reason);
                        }
                        else
                        {
                            uint banDurationSeconds = punishment.Permanent
                                ? SteamBlacklist.PERMANENT
                                : (uint)punishment.DurationSeconds;
                            target.Ban(punishment.Reason, banDurationSeconds);

                            string unbanDate = punishment.Permanent
                                ? "perm"
                                : DateTime.Now.AddSeconds(banDurationSeconds).ToString();
                            string durationDescription = punishment.Permanent
                                ? "permanently"
                                : $"for {banDurationSeconds} seconds";

                            Logger.Log($"{target.DisplayName} ({target.Id}) got banned {durationDescription} because of {warnAmount} warnings!");
                            DiscordWebhooks.DiscordBanWebhook(caller.DisplayName, caller.Id, target.Id, punishment.Reason, unbanDate);
                        }
                    }

                    DiscordWebhooks.DiscordWarnWebHook(caller.DisplayName, caller.Id, target.DisplayName, target.Id, command[1], warnAmount, "add");
                    return;

                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("WarnSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't use /warn properly!");
                    return;
            }

            

            

            
            
        }
    }
}
