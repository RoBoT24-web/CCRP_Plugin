using CCRP_Plugin.Models;
using CCRP_Plugin.Services;
using Rocket.API;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using System;
using System.Collections.Generic;

namespace CCRPPlugin.Commands
{
    internal sealed class CCRPTellCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "tell";
        public string Help => "Send a private message to a player.";
        public string Syntax => "<player> <message>";
        public List<string> Aliases => new List<string> { "msg", "pm" };
        public List<string> Permissions => new List<string> { "ccrp.tell" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
            {
                PrivateMessageService service = ChatFeaturesService.Messages;
                if (service == null || !service.Enabled)
                {
                    UnturnedChat.Say(caller, "Private messages are disabled.");
                    return;
                }

                if (command.Length < 2)
                {
                    UnturnedChat.Say(caller, "Usage: /tell <player> <message>");
                    return;
                }

                UnturnedPlayer target = UnturnedPlayer.FromName(command[0]);
                if (target == null)
                {
                    UnturnedChat.Say(caller, "Player not found.");
                    return;
                }

                if (!(caller is ConsolePlayer) && string.Equals(caller.Id, target.Id, StringComparison.Ordinal))
                {
                    UnturnedChat.Say(caller, "You cannot message yourself.");
                    return;
                }

                service.Send(caller, target, string.Join(" ", command, 1, command.Length - 1));
            });
        }
    }

    internal sealed class CCRPReplyCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "reply";
        public string Help => "Reply to the most recent private message.";
        public string Syntax => "<message>";
        public List<string> Aliases => new List<string> { "r" };
        public List<string> Permissions => new List<string> { "ccrp.reply" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
            {
                PrivateMessageService service = ChatFeaturesService.Messages;
                if (service == null || !service.Enabled)
                {
                    UnturnedChat.Say(caller, "Private messages are disabled.");
                    return;
                }

                if (command.Length == 0)
                {
                    UnturnedChat.Say(caller, "Usage: /reply <message>");
                    return;
                }

                IRocketPlayer target = service.ResolveReplyTarget(caller);
                if (target == null)
                {
                    UnturnedChat.Say(caller, "You have no one to reply to.");
                    return;
                }

                service.Send(caller, target, string.Join(" ", command));
            });
        }
    }

    internal sealed class CCRPSpyPmCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "spypm";
        public string Help => "Toggle viewing other players' private messages.";
        public string Syntax => string.Empty;
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.spypm" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
            {
                PrivateMessageService service = ChatFeaturesService.Messages;
                if (service == null || !service.Enabled)
                {
                    UnturnedChat.Say(caller, "Private messages are disabled.");
                    return;
                }

                bool enabled = service.ToggleSpy(caller.Id);
                UnturnedChat.Say(caller, enabled
                    ? "Private message spy enabled."
                    : "Private message spy disabled.");
            });
        }
    }

    internal sealed class CCRPCkCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;
        public string Name => "ck";
        public string Help => "Start a configured vote, or cast a yes/no ballot.";
        public string Syntax => "<entry|yes|no> [arguments]";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.ck" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
            {
                VoteService service = ChatFeaturesService.Votes;
                if (service == null)
                {
                    UnturnedChat.Say(caller, "Voting is disabled.");
                    return;
                }

                if (command.Length == 0)
                {
                    SayUsage(caller, service);
                    return;
                }

                string entry = command[0].Trim();
                if (VoteService.IsBallot(entry))
                {
                    string error = service.Cast(
                        caller,
                        string.Equals(entry, "yes", StringComparison.OrdinalIgnoreCase));
                    UnturnedChat.Say(caller, error ?? "Your ballot has been recorded.");
                    return;
                }

                VoteDefinition definition = service.Find(entry);
                if (definition == null)
                {
                    SayUsage(caller, service);
                    return;
                }

                string[] arguments = new string[command.Length - 1];
                Array.Copy(command, 1, arguments, 0, arguments.Length);
                string startError = service.Start(definition, caller, arguments);
                if (startError != null)
                {
                    UnturnedChat.Say(caller, startError);
                }
            });
        }

        private static void SayUsage(IRocketPlayer caller, VoteService service)
        {
            string entries = service.ListEntries();
            UnturnedChat.Say(caller, "Usage: /ck <entry|yes|no> [arguments]"
                + (entries.Length > 0 ? ". Entries: " + entries : string.Empty));
        }
    }
}
