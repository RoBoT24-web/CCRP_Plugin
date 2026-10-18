using CCRP_Plugin.Database;
using CCRP_Plugin.Models;
using CCRPPlugin;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CCRP_Plugin.Services
{
    public class PlaytimeService : MonoBehaviour
    {
        public Dictionary<string, List<Playtime>> PlayTimeInfo { get; private set; }
        private PlayTimeDatabase playtimeDatabase => Main.Instance.PlayTimeDatabase;
        private readonly Dictionary<string, Playtime> playtimesByPlayer = new Dictionary<string, Playtime>();
        private readonly Dictionary<string, DateTime> activeSessions = new Dictionary<string, DateTime>();
        private const float SaveIntervalSeconds = 60f;
        private float timeUntilSave = SaveIntervalSeconds;
        private bool hasUnsavedChanges;

        void Awake()
        {
            PlayTimeInfo = new Dictionary<string, List<Playtime>>();
            foreach (Playtime playtime in playtimeDatabase.Data)
            {
                if (playtime != null && !string.IsNullOrEmpty(playtime.Steam64ID))
                {
                    playtimesByPlayer[playtime.Steam64ID] = playtime;
                }
            }
        }

        void Update()
        {
            timeUntilSave -= Time.unscaledDeltaTime;
            if (timeUntilSave <= 0f)
            {
                timeUntilSave = SaveIntervalSeconds;
                Flush();
            }
        }

        public int CurrentPlaytime(string pId)
        {
            Playtime playtime;
            if (!playtimesByPlayer.TryGetValue(pId, out playtime))
            {
                return 0;
            }

            long totalPlaytime = Math.Max(0, playtime.PlayTime);
            DateTime sessionStartedAt;
            if (activeSessions.TryGetValue(pId, out sessionStartedAt))
            {
                totalPlaytime += GetElapsedSeconds(sessionStartedAt, DateTime.Now);
            }

            return (int)Math.Min(int.MaxValue, totalPlaytime);
        }
        public void PlaytimeJoin(string pId)
        {
            CreatePlayerForPlaytime(pId);
            if (!activeSessions.ContainsKey(pId))
            {
                activeSessions.Add(pId, DateTime.Now);
            }
        }
        public void EndSession(string pId)
        {
            if (!activeSessions.ContainsKey(pId))
            {
                return;
            }

            CommitPlaytime(pId, DateTime.Now);
            activeSessions.Remove(pId);
        }

        public void Flush()
        {
            DateTime now = DateTime.Now;
            foreach (string playerId in new List<string>(activeSessions.Keys))
            {
                CommitPlaytime(playerId, now);
            }

            if (!hasUnsavedChanges)
            {
                return;
            }

            try
            {
                playtimeDatabase.Save();
                hasUnsavedChanges = false;
            }
            catch (Exception exception)
            {
                Rocket.Core.Logging.Logger.LogWarning("Failed to save playtime data: " + exception.Message);
            }
        }

        private void CommitPlaytime(string pId, DateTime now)
        {
            Playtime playtime;
            DateTime sessionStartedAt;
            if (!playtimesByPlayer.TryGetValue(pId, out playtime) || !activeSessions.TryGetValue(pId, out sessionStartedAt))
            {
                return;
            }

            long totalPlaytime = Math.Max(0, playtime.PlayTime) + GetElapsedSeconds(sessionStartedAt, now);
            playtime.PlayTime = (int)Math.Min(int.MaxValue, totalPlaytime);
            playtime.LastPlaytime = now;
            activeSessions[pId] = now;
            playtimeDatabase.UpsertPlaytime(playtime);
            hasUnsavedChanges = true;
        }

        private static int GetElapsedSeconds(DateTime start, DateTime end)
        {
            double elapsedSeconds = (end - start).TotalSeconds;
            if (elapsedSeconds <= 0)
            {
                return 0;
            }

            return elapsedSeconds >= int.MaxValue ? int.MaxValue : (int)elapsedSeconds;
        }
        public void CreatePlayerForPlaytime(string pId)
        {
            if (playtimesByPlayer.ContainsKey(pId))
            {
                return;
            }

            Playtime playtime = new Playtime()
            {
                Steam64ID = pId,
                PlayTime = 0,
                LastPlaytime = DateTime.Now
            };
            playtimesByPlayer.Add(pId, playtime);
            playtimeDatabase.UpsertPlaytime(playtime);
            hasUnsavedChanges = true;
        }
    }
}
