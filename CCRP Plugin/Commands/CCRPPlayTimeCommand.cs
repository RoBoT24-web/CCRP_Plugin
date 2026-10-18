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
    internal class CCRPPlayTimeCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "playtime";

        public string Help => "";

        public string Syntax => "<name>";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.playtime" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            switch (command.Length)
            {
                case 0:
                    int playtime = Main.Instance.PlaytimeService.CurrentPlaytime(caller.Id);
                    string formattedPlaytime = FormatPlaytime(playtime);

                    UnturnedChat.Say(caller, Main.Instance.Translate("PlaytimeSuccess", formattedPlaytime), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) has played {formattedPlaytime} on the Server!");
                    return;
                case 1:
                    UnturnedPlayer target = UnturnedPlayer.FromName(command[0]);
                    if (target == null)
                    {
                        UnturnedChat.Say(caller, Main.Instance.Translate("PlayerNotFound"), Main.Instance.MessageColor);
                        Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't find a player (/playtime)!");
                        return;
                    }

                    int targetPlaytime = Main.Instance.PlaytimeService.CurrentPlaytime(target.Id);
                    string formattedTargetPlaytime = FormatPlaytime(targetPlaytime);
                    UnturnedChat.Say(caller, Main.Instance.Translate("PlaytimeOtherSuccess", target.DisplayName, formattedTargetPlaytime), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) has checked {target.DisplayName} ({target.Id}) his playtime ({formattedTargetPlaytime})!");
                    return;
                default:
                    UnturnedChat.Say(caller, Main.Instance.Translate("PlaytimeSyntax"), Main.Instance.MessageColor);
                    Logger.Log($"{caller.DisplayName} ({caller.Id}) didn't use /playtime properly!");
                    return;
            }
        }

        private static string FormatPlaytime(int totalSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
            List<string> parts = new List<string>();
            AddTimeUnit(parts, time.Days, "day");
            AddTimeUnit(parts, time.Hours, "hour");
            AddTimeUnit(parts, time.Minutes, "minute");
            AddTimeUnit(parts, time.Seconds, "second");

            return parts.Count == 0 ? "0 seconds" : string.Join(", ", parts);
        }

        private static void AddTimeUnit(List<string> parts, int value, string unit)
        {
            if (value > 0)
            {
                parts.Add(value + " " + unit + (value == 1 ? string.Empty : "s"));
            }
        }
    }
}
