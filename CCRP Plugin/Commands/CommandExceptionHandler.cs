using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using System;

namespace CCRPPlugin.Commands
{
    internal static class CommandExceptionHandler
    {
        public static void ExecuteSafely(IRocketPlayer caller, string commandName, Action command)
        {
            try
            {
                command();
            }
            catch (Exception exception)
            {
                Logger.LogError($"Command /{commandName} failed for {caller?.DisplayName ?? "unknown caller"}: {exception}");

                try
                {
                    UnturnedChat.Say(caller, "The command failed because of an internal error. Please contact server staff.");
                }
                catch (Exception responseException)
                {
                    Logger.LogError($"Could not send the failure response for /{commandName}: {responseException}");
                }
            }
        }
    }
}