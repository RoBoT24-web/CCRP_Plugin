using CCRP_Plugin.Models;
using CCRPPlugin;
using CCRPPlugin.Commands;
using Rocket.Unturned;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRP_Plugin.Services
{
    public sealed class ChatFeaturesService : MonoBehaviour
    {
        private const int MinimumAnnounceIntervalSeconds = 5;

        private static ChatFeaturesService instance;
        private AutoAnnouncerSettings announcer;
        private int nextMessage;

        public static PrivateMessageService Messages { get; private set; }
        public static VoteService Votes { get; private set; }

        public static void Initialize(
            PrivateMessageSettings privateMessages,
            VoteSettings votes,
            AutoAnnouncerSettings announcerSettings)
        {
            Shutdown();
            if (Main.Instance == null)
            {
                throw new InvalidOperationException("The plugin must be loaded before chat features can be initialized.");
            }

            instance = Main.Instance.gameObject.AddComponent<ChatFeaturesService>();
            Messages = new PrivateMessageService(privateMessages);
            Votes = votes != null && votes.Enabled
                ? new VoteService(votes.Definitions, instance.Schedule)
                : null;

            instance.announcer = announcerSettings ?? new AutoAnnouncerSettings();
            int messageCount = instance.announcer.Messages == null ? 0 : instance.announcer.Messages.Count;
            if (messageCount > 0)
            {
                instance.nextMessage = ((instance.announcer.LastIndex % messageCount) + messageCount) % messageCount;
            }

            U.Events.OnPlayerDisconnected += instance.OnPlayerDisconnected;
            if (instance.announcer.Enabled)
            {
                instance.StartCoroutine(instance.AnnounceLoop());
            }
        }

        public static void Shutdown()
        {
            if (instance != null)
            {
                U.Events.OnPlayerDisconnected -= instance.OnPlayerDisconnected;
                instance.StopAllCoroutines();
                Destroy(instance);
                instance = null;
            }

            Messages = null;
            Votes = null;
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            if (player != null && Messages != null)
            {
                Messages.Forget(player.Id);
            }
        }

        private void Schedule(float seconds, System.Action action)
        {
            StartCoroutine(Delay(seconds, action));
        }

        private IEnumerator Delay(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            try
            {
                action();
            }
            catch (Exception exception)
            {
                Logger.LogError("Scheduled chat feature action failed: " + exception);
            }
        }

        private IEnumerator AnnounceLoop()
        {
            float interval = Math.Max(MinimumAnnounceIntervalSeconds, announcer.Interval);
            while (true)
            {
                yield return new WaitForSeconds(interval);
                try
                {
                    SendNextAnnouncement();
                }
                catch (Exception exception)
                {
                    Logger.LogError("Auto-announcer failed: " + exception);
                }
            }
        }

        private void SendNextAnnouncement()
        {
            if (announcer.Messages == null || announcer.Messages.Count == 0 || Provider.clients.Count == 0)
            {
                return;
            }

            for (int offset = 0; offset < announcer.Messages.Count; offset++)
            {
                int index = (nextMessage + offset) % announcer.Messages.Count;
                AnnouncerMessage message = announcer.Messages[index];
                if (message == null || string.IsNullOrWhiteSpace(message.Text))
                {
                    continue;
                }

                nextMessage = (index + 1) % announcer.Messages.Count;
                announcer.LastIndex = nextMessage;
                string icon = string.IsNullOrWhiteSpace(message.Icon) ? null : message.Icon.Trim();
                Color color = Main.Instance != null ? Main.Instance.MessageColor : Color.cyan;
                ChatManager.serverSendMessage(message.Text, color, null, null, EChatMode.GLOBAL, icon, true);

                if (Main.Instance != null)
                {
                    Main.Instance.Configuration.Save();
                }

                return;
            }
        }
    }
}
