using CCRPPlugin;
using CCRP_Plugin.Services;
using CCRPPlugin.Storage;
using Rocket.API;
using Rocket.Core.Logging;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRP_Plugin.Services
{
    public sealed class TeleportService : MonoBehaviour
    {
        private sealed class TeleportRequest
        {
            public ulong RequesterId;
            public ulong TargetId;
            public float ExpiresAt;
            public float CreatedAt;
        }

        private sealed class PendingTeleport
        {
            public UnturnedPlayer Player;
            public Vector3 Destination;
            public float Rotation;
            public ulong? TargetId;
        }

        private static TeleportService instance;
        private static Func<UnturnedPlayer, bool> isTeleportBlocked;

        private readonly Dictionary<ulong, TeleportRequest> outgoingRequests = new Dictionary<ulong, TeleportRequest>();
        private readonly Dictionary<ulong, PendingTeleport> pendingTeleports = new Dictionary<ulong, PendingTeleport>();
        private readonly Dictionary<ulong, Coroutine> pendingCoroutines = new Dictionary<ulong, Coroutine>();
        private readonly List<WarpRecord> warps = new List<WarpRecord>();
        private DataStorage<List<WarpRecord>> warpStorage;
        private bool warpDataValid = true;
        private bool isInitialized;

        public static Func<UnturnedPlayer, bool> IsTeleportBlocked
        {
            get { return isTeleportBlocked; }
            set { isTeleportBlocked = value; }
        }

        public static void Initialize(Main plugin)
        {
            if (instance != null)
            {
                Unload();
            }

            instance = plugin.gameObject.AddComponent<TeleportService>();
            instance.LoadWarps(plugin.DataDirectory);
            U.Events.OnPlayerDisconnected += instance.OnPlayerDisconnected;
            instance.isInitialized = true;
        }

        public static void Unload()
        {
            if (instance == null)
            {
                return;
            }

            U.Events.OnPlayerDisconnected -= instance.OnPlayerDisconnected;
            instance.StopAllCoroutines();
            instance.outgoingRequests.Clear();
            instance.pendingTeleports.Clear();
            instance.pendingCoroutines.Clear();
            instance.isInitialized = false;
            Destroy(instance);
            instance = null;
            isTeleportBlocked = null;
        }

        private void LoadWarps(string dataDirectory)
        {
            warpStorage = new DataStorage<List<WarpRecord>>(dataDirectory, "Warps.json");
            try
            {
                List<WarpRecord> savedWarps = warpStorage.Read();
                if (savedWarps == null)
                {
                    warpStorage.Save(warps);
                    return;
                }

                foreach (WarpRecord warp in savedWarps)
                {
                    if (warp == null || !IsValidWarpName(warp.Name) ||
                        warps.Any(existing => string.Equals(existing.Name, warp.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new InvalidOperationException("Warps.json contains an invalid or duplicate warp.");
                    }

                    warps.Add(warp);
                }
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                warpDataValid = false;
                Logger.LogError("Failed to load Warps.json. Warp editing and teleporting have been disabled: " + exception.Message);
            }
            catch (IOException exception)
            {
                warpDataValid = false;
                Logger.LogError("Failed to read Warps.json. Warp editing and teleporting have been disabled: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                warpDataValid = false;
                Logger.LogError("Insufficient access to read Warps.json. Warp editing and teleporting have been disabled: " + exception.Message);
            }
            catch (InvalidOperationException exception)
            {
                warpDataValid = false;
                Logger.LogError("Warps.json contains invalid data. Warp editing and teleporting have been disabled: " + exception.Message);
            }
        }

        public static void Request(UnturnedPlayer requester, string targetName)
        {
            TeleportService service = GetService(requester);
            if (service == null || IsBlocked(requester))
            {
                return;
            }

            UnturnedPlayer target = UnturnedPlayer.FromName(targetName);
            if (target == null)
            {
                service.Say(requester, "Could not find that player.");
                return;
            }
            if (target.CSteamID == requester.CSteamID)
            {
                service.Say(requester, "You cannot send a teleport request to yourself.");
                return;
            }
            if (service.outgoingRequests.ContainsKey(requester.CSteamID.m_SteamID))
            {
                service.Say(requester, "You already have an outgoing teleport request. Use /tpa cancel first.");
                return;
            }

            TeleportRequest request = new TeleportRequest
            {
                RequesterId = requester.CSteamID.m_SteamID,
                TargetId = target.CSteamID.m_SteamID,
                ExpiresAt = Time.time + 60f,
                CreatedAt = Time.time
            };
            service.outgoingRequests.Add(request.RequesterId, request);
            service.Say(requester, "Teleport request sent to " + target.DisplayName + ". It expires in 60 seconds.");
            service.Say(target, requester.DisplayName + " requested to teleport to you. Use /tpa accept or /tpa deny.");
        }

        public static void Accept(UnturnedPlayer target)
        {
            TeleportService service = GetService(target);
            if (service == null || IsBlocked(target))
            {
                return;
            }

            TeleportRequest request = service.outgoingRequests.Values
                .Where(entry => entry.TargetId == target.CSteamID.m_SteamID)
                .OrderByDescending(entry => entry.CreatedAt)
                .FirstOrDefault();
            if (request == null)
            {
                service.Say(target, "You have no incoming teleport requests.");
                return;
            }

            service.outgoingRequests.Remove(request.RequesterId);
            UnturnedPlayer requester = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
            if (requester == null)
            {
                service.Say(target, "The requester is no longer online.");
                return;
            }

            service.Say(target, "Accepted " + requester.DisplayName + "'s teleport request.");
            service.Say(requester, target.DisplayName + " accepted your request. Teleporting in 5 seconds.");
            service.BeginTeleport(requester, target.Position, target.Rotation, target.CSteamID.m_SteamID);
        }

        public static void Deny(UnturnedPlayer target)
        {
            TeleportService service = GetService(target);
            if (service == null)
            {
                return;
            }

            TeleportRequest request = service.outgoingRequests.Values
                .Where(entry => entry.TargetId == target.CSteamID.m_SteamID)
                .OrderByDescending(entry => entry.CreatedAt)
                .FirstOrDefault();
            if (request == null)
            {
                service.Say(target, "You have no incoming teleport requests.");
                return;
            }

            service.outgoingRequests.Remove(request.RequesterId);
            service.Say(target, "Teleport request denied.");
            UnturnedPlayer requester = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
            if (requester != null)
            {
                service.Say(requester, target.DisplayName + " denied your teleport request.");
            }
        }

        public static void Cancel(UnturnedPlayer requester)
        {
            TeleportService service = GetService(requester);
            if (service == null)
            {
                return;
            }

            TeleportRequest request;
            if (!service.outgoingRequests.TryGetValue(requester.CSteamID.m_SteamID, out request))
            {
                service.Say(requester, "You have no outgoing teleport request to cancel.");
                return;
            }

            service.outgoingRequests.Remove(request.RequesterId);
            service.Say(requester, "Teleport request cancelled.");
            UnturnedPlayer target = UnturnedPlayer.FromCSteamID(new CSteamID(request.TargetId));
            if (target != null)
            {
                service.Say(target, requester.DisplayName + " cancelled their teleport request.");
            }
        }

        public static void TeleportToWarp(UnturnedPlayer player, string name)
        {
            TeleportService service = GetService(player);
            if (service == null || IsBlocked(player))
            {
                return;
            }
            if (!service.warpDataValid)
            {
                service.Say(player, "Warp data is unavailable; check the server log.");
                return;
            }

            WarpRecord warp = service.warps.FirstOrDefault(entry =>
                string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
            if (warp == null)
            {
                service.Say(player, "Warp not found.");
                return;
            }

            service.BeginTeleport(player, new Vector3(warp.X, warp.Y, warp.Z), warp.Rotation, null);
        }

        public static bool SetWarp(UnturnedPlayer caller, string name)
        {
            TeleportService service = GetService(caller);
            if (service == null)
            {
                return false;
            }
            if (!service.warpDataValid)
            {
                service.Say(caller, "Warp data is unavailable; check the server log.");
                return false;
            }
            if (!IsValidWarpName(name))
            {
                service.Say(caller, "Warp names must be 1 to 32 characters and contain no spaces.");
                return false;
            }

            WarpRecord existing = service.warps.FirstOrDefault(entry =>
                string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
            WarpRecord previous = existing == null ? null : new WarpRecord(existing);
            WarpRecord updated = new WarpRecord
            {
                Name = name,
                X = caller.Position.x,
                Y = caller.Position.y + 0.5f,
                Z = caller.Position.z,
                Rotation = caller.Rotation
            };

            if (existing == null)
            {
                service.warps.Add(updated);
            }
            else
            {
                existing.X = updated.X;
                existing.Y = updated.Y;
                existing.Z = updated.Z;
                existing.Rotation = updated.Rotation;
            }

            if (!service.TrySaveWarps())
            {
                if (previous == null)
                {
                    service.warps.Remove(updated);
                }
                else
                {
                    existing.X = previous.X;
                    existing.Y = previous.Y;
                    existing.Z = previous.Z;
                    existing.Rotation = previous.Rotation;
                }
                service.Say(caller, "Warp could not be saved. Check the server log.");
                return false;
            }

            service.Say(caller, "Warp '" + name + "' has been set.");
            return true;
        }

        public static bool DeleteWarp(UnturnedPlayer caller, string name)
        {
            TeleportService service = GetService(caller);
            if (service == null)
            {
                return false;
            }
            if (!service.warpDataValid)
            {
                service.Say(caller, "Warp data is unavailable; check the server log.");
                return false;
            }

            WarpRecord existing = service.warps.FirstOrDefault(entry =>
                string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
                service.Say(caller, "Warp not found.");
                return false;
            }

            int index = service.warps.IndexOf(existing);
            service.warps.RemoveAt(index);
            if (!service.TrySaveWarps())
            {
                service.warps.Insert(index, existing);
                service.Say(caller, "Warp could not be saved. Check the server log.");
                return false;
            }

            service.Say(caller, "Warp '" + existing.Name + "' has been deleted.");
            return true;
        }

        public static void ListWarps(UnturnedPlayer caller)
        {
            TeleportService service = GetService(caller);
            if (service == null)
            {
                return;
            }
            if (!service.warpDataValid)
            {
                service.Say(caller, "Warp data is unavailable; check the server log.");
                return;
            }

            string list = service.warps.Count == 0
                ? "No warps have been set."
                : "Warps: " + string.Join(", ", service.warps.Select(warp => warp.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
            service.Say(caller, list);
        }

        private void BeginTeleport(UnturnedPlayer player, Vector3 destination, float rotation, ulong? targetId)
        {
            if (IsBlocked(player))
            {
                return;
            }

            ulong playerId = player.CSteamID.m_SteamID;
            Coroutine previous;
            if (pendingCoroutines.TryGetValue(playerId, out previous) && previous != null)
            {
                StopCoroutine(previous);
            }

            pendingTeleports[playerId] = new PendingTeleport
            {
                Player = player,
                Destination = destination,
                Rotation = rotation,
                TargetId = targetId
            };
            pendingCoroutines[playerId] = StartCoroutine(WaitAndTeleport(playerId));
        }

        private IEnumerator WaitAndTeleport(ulong playerId)
        {
            PendingTeleport pending;
            if (!pendingTeleports.TryGetValue(playerId, out pending))
            {
                yield break;
            }

            Vector3 start = pending.Player.Position;
            float deadline = Time.time + 5f;
            while (Time.time < deadline)
            {
                yield return new WaitForSeconds(0.25f);

                if (!pendingTeleports.TryGetValue(playerId, out pending) ||
                    pending.Player == null || pending.Player.Player == null || pending.Player.Player.life.isDead)
                {
                    pendingTeleports.Remove(playerId);
                    pendingCoroutines.Remove(playerId);
                    yield break;
                }

                if (IsBlocked(pending.Player))
                {
                    pendingTeleports.Remove(playerId);
                    pendingCoroutines.Remove(playerId);
                    yield break;
                }

                if ((pending.Player.Position - start).sqrMagnitude > 0.25f)
                {
                    pendingTeleports.Remove(playerId);
                    pendingCoroutines.Remove(playerId);
                    Say(pending.Player, "Teleport cancelled because you moved.");
                    yield break;
                }

                if (pending.TargetId.HasValue &&
                    UnturnedPlayer.FromCSteamID(new CSteamID(pending.TargetId.Value)) == null)
                {
                    pendingTeleports.Remove(playerId);
                    pendingCoroutines.Remove(playerId);
                    Say(pending.Player, "Teleport cancelled because the target left the server.");
                    yield break;
                }
            }

            if (pendingTeleports.TryGetValue(playerId, out pending))
            {
                pendingTeleports.Remove(playerId);
                pendingCoroutines.Remove(playerId);
                if (!IsBlocked(pending.Player))
                {
                    if (pending.TargetId.HasValue)
                    {
                        UnturnedPlayer target = UnturnedPlayer.FromCSteamID(new CSteamID(pending.TargetId.Value));
                        if (target == null)
                        {
                            Say(pending.Player, "Teleport cancelled because the target left the server.");
                            yield break;
                        }
                        pending.Destination = target.Position;
                        pending.Rotation = target.Rotation;
                    }

                    pending.Player.Teleport(pending.Destination, pending.Rotation);
                    Say(pending.Player, "Teleport complete.");
                }
            }
        }

        public static bool IsBlocked(UnturnedPlayer player)
        {
            if (isTeleportBlocked == null)
            {
                return false;
            }

            try
            {
                if (isTeleportBlocked(player))
                {
                    if (instance != null)
                    {
                        instance.Say(player, "You cannot teleport while frozen.");
                    }
                    return true;
                }
            }
            catch (Exception exception)
            {
                Logger.LogError("Failed to check whether a player is frozen before teleporting: " + exception.Message);
                if (instance != null)
                {
                    instance.Say(player, "Teleport is unavailable right now.");
                }
                return true;
            }

            return false;
        }

        private bool TrySaveWarps()
        {
            try
            {
                warpStorage.Save(warps);
                return true;
            }
            catch (IOException exception)
            {
                Logger.LogError("Failed to save Warps.json: " + exception.Message);
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                Logger.LogError("Insufficient access to save Warps.json: " + exception.Message);
                return false;
            }
            catch (Newtonsoft.Json.JsonException exception)
            {
                Logger.LogError("Failed to serialize Warps.json: " + exception.Message);
                return false;
            }
        }

        private void Update()
        {
            if (!isInitialized)
            {
                return;
            }

            foreach (ulong requesterId in outgoingRequests
                .Where(entry => Time.time >= entry.Value.ExpiresAt)
                .Select(entry => entry.Key)
                .ToList())
            {
                TeleportRequest request = outgoingRequests[requesterId];
                outgoingRequests.Remove(requesterId);
                UnturnedPlayer requester = UnturnedPlayer.FromCSteamID(new CSteamID(request.RequesterId));
                UnturnedPlayer target = UnturnedPlayer.FromCSteamID(new CSteamID(request.TargetId));
                if (requester != null)
                {
                    Say(requester, "Your teleport request expired.");
                }
                if (target != null)
                {
                    Say(target, "A teleport request expired.");
                }
            }
        }

        private void OnPlayerDisconnected(UnturnedPlayer player)
        {
            ulong playerId = player.CSteamID.m_SteamID;
            TeleportRequest outgoing;
            if (outgoingRequests.TryGetValue(playerId, out outgoing))
            {
                outgoingRequests.Remove(playerId);
                UnturnedPlayer target = UnturnedPlayer.FromCSteamID(new CSteamID(outgoing.TargetId));
                if (target != null)
                {
                    Say(target, "A teleport request was cancelled because the requester left.");
                }
            }

            List<ulong> requestsToRemove = outgoingRequests.Values
                .Where(request => request.TargetId == playerId)
                .Select(request => request.RequesterId)
                .ToList();
            foreach (ulong requesterId in requestsToRemove)
            {
                outgoingRequests.Remove(requesterId);
                UnturnedPlayer requester = UnturnedPlayer.FromCSteamID(new CSteamID(requesterId));
                if (requester != null)
                {
                    Say(requester, "Your teleport request was cancelled because the target left.");
                }
            }

            pendingTeleports.Remove(playerId);
            Coroutine pendingCoroutine;
            if (pendingCoroutines.TryGetValue(playerId, out pendingCoroutine))
            {
                if (pendingCoroutine != null)
                {
                    StopCoroutine(pendingCoroutine);
                }
                pendingCoroutines.Remove(playerId);
            }
        }

        private static TeleportService GetService(UnturnedPlayer player)
        {
            if (instance != null)
            {
                return instance;
            }

            if (player != null)
            {
                UnturnedChat.Say(player, "Teleport service is not available.");
            }
            return null;
        }

        private void Say(UnturnedPlayer player, string message)
        {
            if (player != null)
            {
                UnturnedChat.Say(player, message, Main.Instance.MessageColor);
            }
        }

        private static bool IsValidWarpName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) &&
                name.Length <= 32 &&
                !name.Any(char.IsWhiteSpace);
        }

        [Serializable]
        private sealed class WarpRecord
        {
            public string Name;
            public float X;
            public float Y;
            public float Z;
            public float Rotation;

            public WarpRecord()
            {
            }

            public WarpRecord(WarpRecord other)
            {
                Name = other.Name;
                X = other.X;
                Y = other.Y;
                Z = other.Z;
                Rotation = other.Rotation;
            }
        }
    }
}

namespace CCRP_Plugin.Commands
{
    internal class CCRPTpaCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "tpa";
        public string Help => "Request or manage a teleport to another player.";
        public string Syntax => "<player|accept|deny|cancel>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.tpa" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null)
                {
                    UnturnedChat.Say(caller, "This command can only be used by a player.");
                    return;
                }
                if (command.Length != 1)
                {
                    UnturnedChat.Say(caller, "Use /tpa <player|accept|deny|cancel>.");
                    return;
                }

                switch (command[0].ToLowerInvariant())
                {
                    case "accept":
                        TeleportService.Accept(player);
                        break;
                    case "deny":
                        TeleportService.Deny(player);
                        break;
                    case "cancel":
                        TeleportService.Cancel(player);
                        break;
                    default:
                        TeleportService.Request(player, command[0]);
                        break;
                }
            });
        }
    }

    internal class CCRPWarpCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "warp";
        public string Help => "Teleport to a shared warp.";
        public string Syntax => "<name>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.warp" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null)
                {
                    UnturnedChat.Say(caller, "This command can only be used by a player.");
                }
                else if (command.Length != 1)
                {
                    UnturnedChat.Say(caller, "Use /warp <name>.");
                }
                else
                {
                    TeleportService.TeleportToWarp(player, command[0]);
                }
            });
        }
    }

    internal class CCRPSetWarpCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "setwarp";
        public string Help => "Create or update a shared warp.";
        public string Syntax => "<name>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.setwarp" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null)
                {
                    UnturnedChat.Say(caller, "This command can only be used by a player.");
                }
                else if (command.Length != 1)
                {
                    UnturnedChat.Say(caller, "Use /setwarp <name>.");
                }
                else
                {
                    TeleportService.SetWarp(player, command[0]);
                }
            });
        }
    }

    internal class CCRPDelWarpCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "delwarp";
        public string Help => "Delete a shared warp.";
        public string Syntax => "<name>";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.delwarp" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null)
                {
                    UnturnedChat.Say(caller, "This command can only be used by a player.");
                }
                else if (command.Length != 1)
                {
                    UnturnedChat.Say(caller, "Use /delwarp <name>.");
                }
                else
                {
                    TeleportService.DeleteWarp(player, command[0]);
                }
            });
        }
    }

    internal class CCRPWarpsCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "warps";
        public string Help => "List all shared warps.";
        public string Syntax => "";
        public List<string> Aliases => new List<string>();
        public List<string> Permissions => new List<string> { "ccrp.warps" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            CCRPPlugin.Commands.CommandExceptionHandler.ExecuteSafely(caller, Name, () =>
            {
                UnturnedPlayer player = caller as UnturnedPlayer;
                if (player == null)
                {
                    UnturnedChat.Say(caller, "This command can only be used by a player.");
                }
                else if (command.Length != 0)
                {
                    UnturnedChat.Say(caller, "Use /warps without arguments.");
                }
                else
                {
                    TeleportService.ListWarps(player);
                }
            });
        }
    }
}
