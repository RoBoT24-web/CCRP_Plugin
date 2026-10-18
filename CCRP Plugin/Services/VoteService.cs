using CCRP_Plugin.Models;
using Rocket.API;
using Rocket.Core;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRP_Plugin.Services
{
    public sealed class VoteService
    {
        private sealed class ActiveVote
        {
            public VoteDefinition Definition;
            public string Starter;
            public string Command;
            public readonly Dictionary<string, bool> Ballots = new Dictionary<string, bool>();
        }

        private static readonly Regex Placeholder = new Regex(@"\{(\d+)\}", RegexOptions.Compiled);
        private static readonly Regex PermissionToken = new Regex(@"[^a-z0-9_.-]", RegexOptions.Compiled);

        private readonly List<VoteDefinition> definitions;
        private readonly System.Action<float, System.Action> schedule;
        private readonly Dictionary<string, float> cooldownUntil = new Dictionary<string, float>();
        private ActiveVote active;

        public VoteService(IEnumerable<VoteDefinition> definitions, System.Action<float, System.Action> schedule)
        {
            this.definitions = (definitions ?? Enumerable.Empty<VoteDefinition>())
                .Where(definition => definition != null)
                .ToList();
            this.schedule = schedule;
        }

        public static bool IsBallot(string entry)
        {
            return string.Equals(entry, "yes", StringComparison.OrdinalIgnoreCase)
                || string.Equals(entry, "no", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Matches(string configured, string entry)
        {
            return !string.IsNullOrWhiteSpace(configured)
                && string.Equals(configured.Trim(), entry, StringComparison.OrdinalIgnoreCase);
        }

        public string ListEntries()
        {
            return string.Join(", ", definitions
                .Where(definition => definition.Enabled && !string.IsNullOrWhiteSpace(definition.Name))
                .Select(definition => definition.Name.Trim())
                .ToArray());
        }

        public VoteDefinition Find(string entry)
        {
            return definitions.FirstOrDefault(definition => definition.Enabled
                && (Matches(definition.Name, entry) || Matches(definition.Alias, entry)));
        }

        public string Start(VoteDefinition definition, IRocketPlayer starter, string[] args)
        {
            if (active != null)
            {
                return "A vote is already in progress.";
            }

            if (definition == null || starter == null)
            {
                return "Invalid vote.";
            }

            if (string.IsNullOrWhiteSpace(definition.Command))
            {
                return "This vote has no command configured.";
            }

            if (definition.RequirePermission)
            {
                string token = string.IsNullOrWhiteSpace(definition.Alias)
                    ? definition.Name
                    : definition.Alias;
                token = PermissionToken.Replace((token ?? "vote").Trim().ToLowerInvariant(), string.Empty);
                if (token.Length == 0)
                {
                    token = "vote";
                }

                if (!R.Permissions.HasPermission(starter, new List<string> { "ccrp.ck." + token }))
                {
                    return "You do not have permission to start this vote.";
                }
            }

            args = args ?? new string[0];
            if (args.Length < Math.Max(0, definition.MinimumArguments))
            {
                return "This vote needs at least " + definition.MinimumArguments + " argument(s).";
            }

            if (args.Any(argument => argument.IndexOf('"') >= 0 || argument.IndexOf('\n') >= 0 || argument.IndexOf('\r') >= 0))
            {
                return "Vote arguments cannot contain quotes or line breaks.";
            }

            if (Provider.clients.Count < Math.Max(0, definition.MinimumPlayers))
            {
                return "At least " + definition.MinimumPlayers + " player(s) must be online to start this vote.";
            }

            string key = VoteKey(definition);
            float until;
            float now = Time.realtimeSinceStartup;
            if (cooldownUntil.TryGetValue(key, out until) && now < until)
            {
                return "This vote is on cooldown for " + (int)Math.Ceiling(until - now) + "s.";
            }

            string command;
            if (!TryBuildCommand(definition.Command, args, out command))
            {
                return "The configured vote command has invalid argument placeholders.";
            }

            ActiveVote vote = new ActiveVote
            {
                Definition = definition,
                Starter = starter.DisplayName,
                Command = command
            };
            active = vote;

            float timer = Math.Max(1, definition.Timer);
            string label = (definition.Name ?? string.Empty).Trim()
                + (args.Length > 0 ? " " + string.Join(" ", args) : string.Empty);
            Broadcast(definition, string.Format(
                "[Vote] {0} (started by {1}) - type /ck yes or /ck no. Ends in {2}s.",
                label, vote.Starter, (int)timer));
            schedule(timer, () => Finish(vote, label));
            return null;
        }

        private static bool TryBuildCommand(string template, string[] args, out string command)
        {
            command = template.Trim().TrimStart('/');
            MatchCollection matches = Placeholder.Matches(command);
            if (matches.Count == 0)
            {
                if (args.Length > 0)
                {
                    command += " " + string.Join(" ", args.Select(QuoteArgument).ToArray());
                }

                return command.Length > 0;
            }

            foreach (Match match in matches)
            {
                int index;
                if (!int.TryParse(match.Groups[1].Value, out index) || index < 0 || index >= args.Length)
                {
                    return false;
                }

                command = command.Replace(match.Value, QuoteArgument(args[index]));
            }

            return !Placeholder.IsMatch(command) && command.Length > 0;
        }

        private static string QuoteArgument(string argument)
        {
            return argument.IndexOf(' ') >= 0 ? "\"" + argument + "\"" : argument;
        }

        private static string VoteKey(VoteDefinition definition)
        {
            return string.IsNullOrWhiteSpace(definition.Alias)
                ? (definition.Name ?? string.Empty).Trim().ToLowerInvariant()
                : definition.Alias.Trim().ToLowerInvariant();
        }

        public string Cast(IRocketPlayer voter, bool yes)
        {
            if (active == null)
            {
                return "There is no vote in progress.";
            }

            if (voter == null || voter is ConsolePlayer)
            {
                return "Only players can cast ballots.";
            }

            active.Ballots[voter.Id] = yes;
            return null;
        }

        private void Finish(ActiveVote vote, string label)
        {
            if (active != vote)
            {
                return;
            }

            active = null;
            VoteDefinition definition = vote.Definition;
            if (definition.CooldownTime > 0)
            {
                cooldownUntil[VoteKey(definition)] = Time.realtimeSinceStartup + definition.CooldownTime;
            }

            int total = vote.Ballots.Count;
            int yes = vote.Ballots.Values.Count(ballot => ballot);
            double required = Math.Min(100d, Math.Max(1d, definition.RequiredPercent));
            double percent = total == 0 ? 0d : yes * 100d / total;
            bool passed = total > 0 && percent >= required;

            Broadcast(definition, string.Format(
                "[Vote] {0}: {1} (yes {2}, no {3}, {4:0.#}% yes, {5:0.#}% required).",
                label, passed ? "PASSED" : "FAILED", yes, total - yes, percent, required));
            Logger.Log("[Vote] " + label + " started by " + vote.Starter + " "
                + (passed ? "passed" : "failed") + " (" + yes + "/" + total + ").");

            if (!passed || string.IsNullOrWhiteSpace(vote.Command))
            {
                return;
            }

            try
            {
                if (!R.Commands.Execute(new ConsolePlayer(), vote.Command))
                {
                    Logger.LogError("[Vote] Could not execute configured command '" + vote.Command + "'.");
                }
            }
            catch (Exception exception)
            {
                Logger.LogError("[Vote] Command '" + vote.Command + "' failed: " + exception);
            }
        }

        private static void Broadcast(VoteDefinition definition, string text)
        {
            Color color;
            if (!ColorUtility.TryParseHtmlString(definition.Color ?? string.Empty, out color))
            {
                color = UnturnedChat.GetColorFromName(definition.Color ?? "cyan", Color.cyan);
            }

            string icon = string.IsNullOrWhiteSpace(definition.Icon) ? null : definition.Icon.Trim();
            ChatManager.serverSendMessage(text, color, null, null, EChatMode.GLOBAL, icon, false);
        }
    }
}
