using CCRPPlugin;
using CCRPPlugin.Storage;
using Rocket.Core;
using Rocket.Core.Logging;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRP_Plugin.Services
{
    public sealed class DeathLocationService : MonoBehaviour
    {
        private const string BackPermission = "ccrp.back";
        private const long DeathLocationLifetimeTicks = TimeSpan.TicksPerSecond * 60;

        private readonly Dictionary<ulong, DeathLocation> locations = new Dictionary<ulong, DeathLocation>();
        private DataStorage<List<DeathLocationEntry>> storage;
        private bool available = true;
        private float nextExpirationCheck;

        public static DeathLocationService Initialize(Main plugin)
        {
            DeathLocationService service = plugin.gameObject.AddComponent<DeathLocationService>();
            service.Load(plugin.DataDirectory);
            UnturnedPlayerEvents.OnPlayerDeath += service.OnPlayerDeath;
            return service;
        }

        public static void Shutdown(DeathLocationService service)
        {
            if (service == null)
            {
                return;
            }

            UnturnedPlayerEvents.OnPlayerDeath -= service.OnPlayerDeath;
            Destroy(service);
        }

        public bool TryConsume(CSteamID playerId, out DeathLocation location)
        {
            location = null;
            if (!available)
            {
                return false;
            }

            DeathLocation savedLocation;
            if (!locations.TryGetValue(playerId.m_SteamID, out savedLocation))
            {
                return false;
            }

            locations.Remove(playerId.m_SteamID);
            SaveLocations();
            if (savedLocation.ExpiresAtUtcTicks <= DateTime.UtcNow.Ticks)
            {
                return false;
            }

            location = savedLocation;
            return true;
        }

        private void Load(string dataDirectory)
        {
            storage = new DataStorage<List<DeathLocationEntry>>(dataDirectory, "DeathLocations.json");
            try
            {
                List<DeathLocationEntry> savedLocations = storage.Read();
                if (savedLocations == null)
                {
                    return;
                }

                HashSet<ulong> loadedPlayerIds = new HashSet<ulong>();
                foreach (DeathLocationEntry entry in savedLocations)
                {
                    if (entry == null || !IsValid(entry.X, entry.Y, entry.Z, entry.Rotation)
                        || !loadedPlayerIds.Add(entry.SteamId))
                    {
                        throw new InvalidDataException("DeathLocations.json contains an invalid or duplicate entry.");
                    }

                    if (entry.ExpiresAtUtcTicks > DateTime.UtcNow.Ticks &&
                        entry.ExpiresAtUtcTicks <= DateTime.MaxValue.Ticks)
                    {
                        locations.Add(entry.SteamId, new DeathLocation(
                            entry.X,
                            entry.Y,
                            entry.Z,
                            entry.Rotation,
                            entry.ExpiresAtUtcTicks));
                    }
                }

                if (savedLocations.Count != locations.Count)
                {
                    SaveLocations();
                }
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                Disable("Failed to load DeathLocations.json: " + exception.Message);
            }
            catch (IOException exception)
            {
                Disable("Failed to read DeathLocations.json: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                Disable("Insufficient access to read DeathLocations.json: " + exception.Message);
            }
            catch (InvalidDataException exception)
            {
                Disable(exception.Message);
            }
        }

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer)
        {
            if (!available || player == null)
            {
                return;
            }

            ulong steamId = player.CSteamID.m_SteamID;
            if (!R.Permissions.HasPermission(player, new List<string> { BackPermission }))
            {
                if (locations.Remove(steamId))
                {
                    SaveLocations();
                }
                return;
            }

            Vector3 position = player.Position;
            float rotation = player.Rotation;
            if (!IsValid(position.x, position.y, position.z, rotation))
            {
                Logger.LogWarning("Skipped saving an invalid death location for " + steamId + ".");
                return;
            }

            DeathLocation current = new DeathLocation(
                position.x,
                position.y,
                position.z,
                rotation,
                DateTime.UtcNow.AddTicks(DeathLocationLifetimeTicks).Ticks);
            locations[steamId] = current;
            SaveLocations();
        }

        private void Update()
        {
            if (!available || Time.time < nextExpirationCheck)
            {
                return;
            }

            nextExpirationCheck = Time.time + 1f;
            if (RemoveExpiredLocations(DateTime.UtcNow.Ticks))
            {
                SaveLocations();
            }
        }

        private bool RemoveExpiredLocations(long currentUtcTicks)
        {
            List<ulong> expiredIds = null;
            foreach (KeyValuePair<ulong, DeathLocation> entry in locations)
            {
                if (entry.Value.ExpiresAtUtcTicks <= currentUtcTicks)
                {
                    if (expiredIds == null)
                    {
                        expiredIds = new List<ulong>();
                    }
                    expiredIds.Add(entry.Key);
                }
            }

            if (expiredIds == null)
            {
                return false;
            }

            for (int i = 0; i < expiredIds.Count; i++)
            {
                locations.Remove(expiredIds[i]);
            }

            return true;
        }

        private void SaveLocations()
        {
            try
            {
                List<DeathLocationEntry> entries = new List<DeathLocationEntry>(locations.Count);
                foreach (KeyValuePair<ulong, DeathLocation> entry in locations)
                {
                    entries.Add(new DeathLocationEntry
                    {
                        SteamId = entry.Key,
                        X = entry.Value.Position.x,
                        Y = entry.Value.Position.y,
                        Z = entry.Value.Position.z,
                        Rotation = entry.Value.Rotation,
                        ExpiresAtUtcTicks = entry.Value.ExpiresAtUtcTicks
                    });
                }

                storage.Save(entries);
            }
            catch (IOException exception)
            {
                Logger.LogError("Failed to save DeathLocations.json: " + exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                Logger.LogError("Insufficient access to save DeathLocations.json: " + exception);
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                Logger.LogError("Failed to serialize DeathLocations.json: " + exception);
            }
        }

        private void Disable(string message)
        {
            available = false;
            Logger.LogError(message + " The /back command is disabled until the data file is repaired.");
        }

        private static bool IsValid(float x, float y, float z, float rotation)
        {
            return IsFinite(x) && IsFinite(y) && IsFinite(z) && IsFinite(rotation);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private sealed class DeathLocationEntry
        {
            public ulong SteamId { get; set; }
            public float X { get; set; }
            public float Y { get; set; }
            public float Z { get; set; }
            public float Rotation { get; set; }
            public long ExpiresAtUtcTicks { get; set; }
        }
    }

    public sealed class DeathLocation
    {
        public Vector3 Position { get; private set; }
        public float Rotation { get; private set; }
        public long ExpiresAtUtcTicks { get; private set; }

        public DeathLocation(float x, float y, float z, float rotation, long expiresAtUtcTicks)
        {
            Position = new Vector3(x, y, z);
            Rotation = rotation;
            ExpiresAtUtcTicks = expiresAtUtcTicks;
        }
    }
}
