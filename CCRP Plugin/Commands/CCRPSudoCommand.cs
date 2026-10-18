using Rocket.API;
using Rocket.Core;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CCRPPlugin.Commands
{
    public sealed class CCRPSudoCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "sudo";
        public string Help => "Run a command as an online player, all players, or the console.";
        public string Syntax => "<player|*|*console*> <command>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.sudo" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () => ExecuteCore(caller, command));
        }

        private static void ExecuteCore(IRocketPlayer caller, string[] command)
        {
            if (command == null || command.Length < 2)
            {
                UnturnedChat.Say(caller, "Usage: /sudo <player|*|*console*> <command>");
                return;
            }

            if (R.Commands == null)
            {
                UnturnedChat.Say(caller, "The command system is unavailable.");
                return;
            }

            string commandLine = string.Join(" ", command.Skip(1));
            if (string.Equals(command[0], "*console*", StringComparison.OrdinalIgnoreCase))
            {
                bool succeeded = R.Commands.Execute(new ConsolePlayer(), commandLine);
                UnturnedChat.Say(caller, succeeded ? "Command executed as console." : "Console command could not be executed.");
                return;
            }

            if (command[0] == "*")
            {
                int executed = 0;
                List<SteamPlayer> clients = Provider.clients;
                if (clients == null)
                {
                    UnturnedChat.Say(caller, "No online players found.");
                    return;
                }

                foreach (SteamPlayer steamPlayer in clients.ToList())
                {
                    UnturnedPlayer target = steamPlayer == null ? null : UnturnedPlayer.FromSteamPlayer(steamPlayer);
                    if (target != null && R.Commands.Execute(target, commandLine))
                        executed++;
                }

                UnturnedChat.Say(caller, "Command executed successfully for " + executed + " player(s).");
                return;
            }

            UnturnedPlayer singleTarget = UnturnedPlayer.FromName(command[0]);
            if (singleTarget == null)
            {
                UnturnedChat.Say(caller, "Player not found.");
                return;
            }

            bool result = R.Commands.Execute(singleTarget, commandLine);
            UnturnedChat.Say(caller, result
                ? "Command executed as " + singleTarget.DisplayName + "."
                : "Command could not be executed as " + singleTarget.DisplayName + ".");
        }
    }
}
