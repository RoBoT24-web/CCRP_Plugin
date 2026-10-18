using CCRP_Plugin.Models;
using CCRPPlugin;
using Rocket.API;
using Rocket.Core;
using Rocket.Core.Logging;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRP_Plugin.Services
{
    public sealed class PrivateMessageService
    {
        private const string ConsoleKey = "*console*";
        private readonly PrivateMessageSettings settings;
        private readonly Dictionary<string, string> lastPartner = new Dictionary<string, string>();
        private readonly HashSet<string> spies = new HashSet<string>();

        public PrivateMessageService(PrivateMessageSettings settings)
        {
            this.settings = settings ?? new PrivateMessageSettings();
        }

        public bool Enabled
        {
            get { return settings.Enabled; }
        }

        private static Color MessageColor
        {
            get { return Main.Instance != null ? Main.Instance.MessageColor : Color.cyan; }
        }

        public static string KeyOf(IRocketPlayer player)
        {
            return player is ConsolePlayer ? ConsoleKey : player.Id;
        }

        private string NameOf(IRocketPlayer player)
        {
            if (!(player is ConsolePlayer))
            {
                return player.DisplayName;
            }

            return string.IsNullOrWhiteSpace(settings.ConsoleDisplayName)
                ? ConsoleKey
                : settings.ConsoleDisplayName;
        }

        private static string Format(string format, string fallback, params object[] arguments)
        {
            string selected = string.IsNullOrEmpty(format) ? fallback : format;
            try
            {
                return string.Format(selected, arguments);
            }
            catch (FormatException exception)
            {
                Logger.LogError("Invalid private-message format '" + selected + "': " + exception.Message);
                return string.Format(fallback, arguments);
            }
        }

        public IRocketPlayer ResolveReplyTarget(IRocketPlayer sender)
        {
            string partner;
            if (!lastPartner.TryGetValue(KeyOf(sender), out partner))
            {
                return null;
            }

            if (partner == ConsoleKey)
            {
                return new ConsolePlayer();
            }

            foreach (SteamPlayer client in Provider.clients)
            {
                if (client != null && client.playerID.steamID.ToString() == partner)
                {
                    return UnturnedPlayer.FromSteamPlayer(client);
                }
            }

            lastPartner.Remove(KeyOf(sender));
            return null;
        }

        public void Send(IRocketPlayer sender, IRocketPlayer target, string message)
        {
            if (!Enabled || sender == null || target == null || string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            string senderName = NameOf(sender);
            string targetName = NameOf(target);
            Deliver(target, Format(settings.FormatFrom, "(From {0}): {1}", senderName, message));
            Deliver(sender, Format(settings.FormatTo, "(To {0}): {1}", targetName, message));

            string senderKey = KeyOf(sender);
            string targetKey = KeyOf(target);
            lastPartner[senderKey] = targetKey;
            lastPartner[targetKey] = senderKey;

            string safeSender = senderName.Replace("<", "‹").Replace(">", "›");
            string safeTarget = targetName.Replace("<", "‹").Replace(">", "›");
            string safeMessage = message.Replace("<", "‹").Replace(">", "›");
            string spyLine = Format(settings.FormatSpy, "Spy: {0} -> {1}: {2}",
                safeSender, safeTarget, safeMessage);
            foreach (SteamPlayer client in Provider.clients)
            {
                if (client == null)
                {
                    continue;
                }

                string id = client.playerID.steamID.ToString();
                if (id != senderKey && id != targetKey && spies.Contains(id))
                {
                    UnturnedPlayer spy = UnturnedPlayer.FromSteamPlayer(client);
                    if (spy != null && R.Permissions.HasPermission(spy, new List<string> { "ccrp.spypm" }))
                    {
                        UnturnedChat.Say(spy, spyLine, MessageColor, true);
                    }
                }
            }
        }

        private static void Deliver(IRocketPlayer recipient, string message)
        {
            if (recipient is ConsolePlayer)
            {
                Logger.Log(message);
                return;
            }

            UnturnedChat.Say(recipient, message, MessageColor, false);
        }

        public bool ToggleSpy(string playerId)
        {
            if (spies.Add(playerId))
            {
                return true;
            }

            spies.Remove(playerId);
            return false;
        }

        public void Forget(string playerId)
        {
            spies.Remove(playerId);
            lastPartner.Remove(playerId);
            List<string> staleEntries = new List<string>();
            foreach (KeyValuePair<string, string> pair in lastPartner)
            {
                if (pair.Value == playerId)
                {
                    staleEntries.Add(pair.Key);
                }
            }

            foreach (string key in staleEntries)
            {
                lastPartner.Remove(key);
            }
        }
    }
}
