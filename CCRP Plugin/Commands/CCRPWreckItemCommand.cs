using Rocket.API;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal class CCRPWreckItemCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;

        public string Name => "wreckitem";

        public string Help => "Queue nearby objects with specific asset IDs for destruction.";

        public string Syntax => "<id[,id...]> <radius> [username|Steam64] | scan <id[,id...]> <radius> [username|Steam64] | confirm <reason> | abort";

        public List<string> Aliases => new List<string>();

        public List<string> Permissions => new List<string> { "ccrp.wreck" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPWreckCommand.ExecuteItemCommand(caller, command);
        }
    }
}
