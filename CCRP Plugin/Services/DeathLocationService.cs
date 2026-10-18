using CCRPPlugin;
using CCRPPlugin.Storage;
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
        private readonly Dictionary<ulong, DeathLocation> locations = new Dictionary<ulong, DeathLocation>();
        private DataStorage<List<DeathLocationEntry>> storage;
        private bool available = true;

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

        public bool TryGet(CSteamID playerId, out DeathLocation location)
        {
            if (available)
            {
                return locations.TryGetValue(playerId.m_SteamID, out location);
            }

            location = null;
            return false;
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

                foreach (DeathLocationEntry entry in savedLocations)
                {
                    if (entry == null || !IsValid(entry.X, entry.Y, entry.Z, entry.Rotation)
                        || locations.ContainsKey(entry.SteamId))
                    {
                        throw new InvalidDataException("DeathLocations.json contains an invalid or duplicate entry.");
                    }

                    locations.Add(entry.SteamId, new DeathLocation(entry.X, entry.Y, entry.Z, entry.Rotation));
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

            Vector3 position = player.Position;
            float rotation = player.Rotation;
            ulong steamId = player.CSteamID.m_SteamID;
            if (!IsValid(position.x, position.y, position.z, rotation))
            {
                Logger.LogWarning("Skipped saving an invalid death location for " + steamId + ".");
                return;
            }

            DeathLocation current = new DeathLocation(position.x, position.y, position.z, rotation);
            locations[steamId] = current;

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
                        Rotation = entry.Value.Rotation
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
        }
    }

    public sealed class DeathLocation
    {
        public Vector3 Position { get; private set; }
        public float Rotation { get; private set; }

        public DeathLocation(float x, float y, float z, float rotation)
        {
            Position = new Vector3(x, y, z);
            Rotation = rotation;
        }
    }
}
