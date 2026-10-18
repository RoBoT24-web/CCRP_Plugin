using CCRP_Plugin.Models;
using CCRPPlugin;
using CCRPPlugin.Storage;
using Rocket.API;
using Rocket.API.Serialisation;
using Rocket.Core;
using Rocket.Unturned.Player;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRP_Plugin.Services
{
    public class DonationBypassService
    {
        private const int MaximumDisplayNameLength = 24;
        private static readonly TimeSpan BypassRemovalConfirmationTimeout = TimeSpan.FromSeconds(30);
        private static readonly Regex HexColorPattern = new Regex("^#?[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        private readonly Main plugin;
        private readonly DataStorage<BypassGroupRegistry> registryStorage;
        private readonly BypassGroupRegistry registry;
        private readonly Dictionary<string, PendingBypassRemoval> pendingBypassRemovals =
            new Dictionary<string, PendingBypassRemoval>(StringComparer.OrdinalIgnoreCase);

        public DonationBypassService(Main plugin)
        {
            this.plugin = plugin;
            registryStorage = new DataStorage<BypassGroupRegistry>(plugin.Directory, "DonationBypassGroups.json", Newtonsoft.Json.Formatting.Indented);

            try
            {
                BypassGroupRegistry savedRegistry = registryStorage.Read();
                if ((savedRegistry == null && File.Exists(registryStorage.DataPath)) ||
                    (savedRegistry != null && savedRegistry.CreatedPlayerIds == null))
                {
                    throw new InvalidDataException("DonationBypassGroups.json is missing its player creation records.");
                }

                registry = savedRegistry ?? new BypassGroupRegistry();
            }
            catch (Exception exception)
            {
                Logger.LogError("Failed to read DonationBypassGroups.json: " + exception.Message);
                throw new InvalidDataException("Could not load the donation bypass group registry.", exception);
            }

        }

        public bool TryCreateGroup(UnturnedPlayer player, string displayName, string color, out string errorKey)
        {
            errorKey = null;
            if (!IsValidDisplayName(displayName))
            {
                errorKey = "BypassInvalidName";
                return false;
            }

            if (!TryNormalizeColor(color, out color))
            {
                errorKey = "BypassInvalidColor";
                return false;
            }

            if (WasCreated(player.Id) || R.Permissions.GetGroup(GetPersonalGroupId(player)) != null)
            {
                errorKey = "BypassGroupAlreadyCreated";
                return false;
            }

            DonationBypassTier tier = GetHighestTier(player);
            if (tier == null)
            {
                errorKey = "BypassNoDonationTier";
                return false;
            }

            RocketPermissionsGroup group = new RocketPermissionsGroup
            {
                Id = GetPersonalGroupId(player),
                DisplayName = displayName.Trim(),
                Prefix = displayName.Trim(),
                Color = color,
                ParentGroup = tier.GroupId,
                Members = new List<string> { player.Id },
                Permissions = new List<Permission>()
            };

            RocketPermissionsProviderResult result = R.Permissions.AddGroup(group);
            if (result != RocketPermissionsProviderResult.Success)
            {
                Logger.LogError($"Could not create personal bypass group for {player.Id}: {result}.");
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            registry.CreatedPlayerIds.Add(player.Id);
            if (!TrySaveRegistry())
            {
                registry.CreatedPlayerIds.Remove(player.Id);
                try
                {
                    RocketPermissionsProviderResult deleteResult = R.Permissions.DeleteGroup(group.Id);
                    if (deleteResult != RocketPermissionsProviderResult.Success)
                    {
                        Logger.LogError($"Could not roll back personal bypass group {group.Id} after registry save failed: {deleteResult}.");
                    }
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Could not roll back personal bypass group {group.Id} after registry save failed: {exception.Message}");
                }

                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            return true;
        }

        public bool TrySetGroupName(UnturnedPlayer player, string displayName, out string errorKey)
        {
            errorKey = null;
            if (!IsValidDisplayName(displayName))
            {
                errorKey = "BypassInvalidName";
                return false;
            }

            RocketPermissionsGroup group = GetOwnedGroup(player, out errorKey);
            if (group == null)
            {
                return false;
            }

            string previousDisplayName = group.DisplayName;
            string previousPrefix = group.Prefix;
            group.DisplayName = displayName.Trim();
            group.Prefix = displayName.Trim();
            if (!SaveGroup(group))
            {
                group.DisplayName = previousDisplayName;
                group.Prefix = previousPrefix;
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            return true;
        }

        public bool TrySetGroupColor(UnturnedPlayer player, string color, out string errorKey)
        {
            errorKey = null;
            if (!TryNormalizeColor(color, out color))
            {
                errorKey = "BypassInvalidColor";
                return false;
            }

            RocketPermissionsGroup group = GetOwnedGroup(player, out errorKey);
            if (group == null)
            {
                return false;
            }

            string previousColor = group.Color;
            group.Color = color;
            if (!SaveGroup(group))
            {
                group.Color = previousColor;
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            return true;
        }

        public bool TryClaimItem(UnturnedPlayer player, ushort itemId, out string errorKey)
        {
            errorKey = null;
            if (Assets.find(EAssetType.ITEM, itemId) == null)
            {
                errorKey = "BypassItemNotFound";
                return false;
            }

            GiveSoftBlacklist entry = (plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                .FirstOrDefault(item => item != null && item.Id == itemId);
            if (entry == null)
            {
                errorKey = "BypassItemNotEligible";
                return false;
            }

            return TryClaim(player, entry.Bypass, BypassType.Item, itemId, out errorKey);
        }

        public bool TryClaimVehicle(UnturnedPlayer player, ushort vehicleId, out string errorKey)
        {
            errorKey = null;
            if (Assets.find(EAssetType.VEHICLE, vehicleId) == null)
            {
                errorKey = "BypassVehicleNotFound";
                return false;
            }

            VehicleSpawnBlacklist entry = (plugin.Blacklists.VehicleSpawnBlacklist ?? new List<VehicleSpawnBlacklist>())
                .FirstOrDefault(vehicle => vehicle != null && vehicle.VehicleId == vehicleId);
            if (entry == null)
            {
                errorKey = "BypassVehicleNotEligible";
                return false;
            }

            return TryClaim(player, entry.Bypass, BypassType.Vehicle, vehicleId, out errorKey);
        }

        public List<string> GetPlayerBypasses(UnturnedPlayer player, out string errorKey)
        {
            return GetPersonalGroupBypasses(player.Id, out errorKey);
        }

        public List<string> GetPersonalGroupBypasses(string playerId, out string errorKey)
        {
            RocketPermissionsGroup group = GetRegisteredPersonalGroup(playerId, out errorKey);
            if (group == null)
            {
                return null;
            }

            return GetBypasses(group);
        }

        public bool TryGetBypassPermission(string type, ushort assetId, out string bypassPermission, out string errorKey)
        {
            bypassPermission = null;
            errorKey = null;
            if (string.Equals(type, "item", StringComparison.OrdinalIgnoreCase))
            {
                GiveSoftBlacklist entry = (plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Concat(plugin.Blacklists.GiveItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Concat(plugin.Blacklists.PickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .FirstOrDefault(item => item != null && item.Id == assetId);
                bypassPermission = entry?.Bypass;
            }
            else if (string.Equals(type, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                VehicleSpawnBlacklist entry = (plugin.Blacklists.VehicleSpawnBlacklist ?? new List<VehicleSpawnBlacklist>())
                    .FirstOrDefault(vehicle => vehicle != null && vehicle.VehicleId == assetId);
                bypassPermission = entry?.Bypass;
            }
            else
            {
                errorKey = "BypassAdminSyntax";
                return false;
            }

            if (string.IsNullOrWhiteSpace(bypassPermission))
            {
                errorKey = "BypassAdminAssetNotFound";
                return false;
            }

            return true;
        }

        public bool TryAdminAddBypass(string playerId, string bypassPermission, out string errorKey)
        {
            errorKey = null;
            RocketPermissionsGroup group = GetRegisteredPersonalGroup(playerId, out errorKey);
            if (group == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(bypassPermission) || !GetKnownBypassPermissions().Contains(bypassPermission))
            {
                errorKey = "BypassAdminPermissionUnknown";
                return false;
            }

            if (group.Permissions.Any(permission => permission != null &&
                    string.Equals(permission.Name, bypassPermission, StringComparison.OrdinalIgnoreCase)))
            {
                errorKey = "BypassAdminAlreadyAdded";
                return false;
            }

            group.Permissions.Add(new Permission(bypassPermission, 0));
            if (!SaveGroup(group))
            {
                group.Permissions.RemoveAll(permission =>
                    string.Equals(permission.Name, bypassPermission, StringComparison.OrdinalIgnoreCase));
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            return true;
        }

        public bool TryAdminRemoveBypass(string playerId, string bypassPermission, out string errorKey)
        {
            errorKey = null;
            RocketPermissionsGroup group = GetRegisteredPersonalGroup(playerId, out errorKey);
            if (group == null)
            {
                return false;
            }

            int permissionIndex = group.Permissions.FindIndex(permission =>
                permission != null && string.Equals(permission.Name, bypassPermission, StringComparison.OrdinalIgnoreCase));
            if (permissionIndex < 0)
            {
                errorKey = "BypassAdminNotAdded";
                return false;
            }

            Permission removedPermission = group.Permissions[permissionIndex];
            group.Permissions.RemoveAt(permissionIndex);
            if (!SaveGroup(group))
            {
                group.Permissions.Insert(permissionIndex, removedPermission);
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            return true;
        }

        public bool TryAdminDeletePersonalGroup(string playerId, out string errorKey)
        {
            errorKey = null;
            RocketPermissionsGroup group = GetRegisteredPersonalGroup(playerId, out errorKey);
            if (group == null)
            {
                return false;
            }

            RocketPermissionsProviderResult result;
            try
            {
                result = R.Permissions.DeleteGroup(group.Id);
            }
            catch (Exception exception)
            {
                Logger.LogError($"Failed to delete personal bypass group {group.Id}: {exception.Message}");
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            if (result != RocketPermissionsProviderResult.Success)
            {
                Logger.LogError($"Rocket permissions provider failed to delete group {group.Id}: {result}.");
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            List<string> createdPlayerIds = registry.CreatedPlayerIds
                .Where(id => string.Equals(id, playerId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            registry.CreatedPlayerIds.RemoveAll(id =>
                string.Equals(id, playerId, StringComparison.OrdinalIgnoreCase));
            if (!TrySaveRegistry())
            {
                registry.CreatedPlayerIds.AddRange(createdPlayerIds);
                try
                {
                    RocketPermissionsProviderResult restoreResult = R.Permissions.AddGroup(group);
                    if (restoreResult != RocketPermissionsProviderResult.Success)
                    {
                        Logger.LogError($"Could not restore personal bypass group {group.Id} after registry save failed: {restoreResult}.");
                    }
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Could not restore personal bypass group {group.Id} after registry save failed: {exception.Message}");
                }

                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            return true;
        }

        private List<string> GetBypasses(RocketPermissionsGroup group)
        {
            HashSet<string> ownedPermissions = new HashSet<string>(
                group.Permissions
                    .Where(permission => permission != null && !string.IsNullOrWhiteSpace(permission.Name))
                    .Select(permission => permission.Name),
                StringComparer.OrdinalIgnoreCase);
            List<string> bypasses = new List<string>();

            foreach (GiveSoftBlacklist item in plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>())
            {
                AddItemBypassDescription(item, ownedPermissions, bypasses);
            }

            foreach (GiveSoftBlacklist item in plugin.Blacklists.GiveItemBlacklist ?? new List<GiveSoftBlacklist>())
            {
                AddItemBypassDescription(item, ownedPermissions, bypasses);
            }

            foreach (GiveSoftBlacklist item in plugin.Blacklists.PickupItemBlacklist ?? new List<GiveSoftBlacklist>())
            {
                AddItemBypassDescription(item, ownedPermissions, bypasses);
            }

            foreach (VehicleSpawnBlacklist vehicle in plugin.Blacklists.VehicleSpawnBlacklist ?? new List<VehicleSpawnBlacklist>())
            {
                if (vehicle == null || !ownedPermissions.Contains(vehicle.Bypass))
                {
                    continue;
                }

                VehicleAsset asset = Assets.find(EAssetType.VEHICLE, vehicle.VehicleId) as VehicleAsset;
                bypasses.Add($"Vehicle: {asset?.vehicleName ?? "Unknown"} ({vehicle.VehicleId})");
            }

            return bypasses
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(description => description, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private RocketPermissionsGroup GetRegisteredPersonalGroup(string playerId, out string errorKey)
        {
            errorKey = null;
            if (string.IsNullOrWhiteSpace(playerId) || !WasCreated(playerId))
            {
                errorKey = "BypassPersonalGroupNotFound";
                return null;
            }

            RocketPermissionsGroup group = R.Permissions.GetGroup(GetPersonalGroupId(playerId));
            if (group == null)
            {
                errorKey = "BypassGroupMissing";
                return null;
            }

            if (group.Permissions == null)
            {
                Logger.LogError($"Personal bypass group {group.Id} has no permissions collection.");
                errorKey = "BypassGroupMissing";
                return null;
            }

            return group;
        }

        private static void AddItemBypassDescription(
            GiveSoftBlacklist item,
            HashSet<string> ownedPermissions,
            List<string> bypasses)
        {
            if (item == null || !ownedPermissions.Contains(item.Bypass))
            {
                return;
            }

            ItemAsset asset = Assets.find(EAssetType.ITEM, item.Id) as ItemAsset;
            bypasses.Add($"Item: {asset?.itemName ?? "Unknown"} ({item.Id})");
        }

        public bool TryConfirmItemRemoval(UnturnedPlayer player, out ushort itemId, out string errorKey)
        {
            return TryConfirmRemoval(player, BypassType.Item, out itemId, out errorKey);
        }

        public bool TryConfirmVehicleRemoval(UnturnedPlayer player, out ushort vehicleId, out string errorKey)
        {
            return TryConfirmRemoval(player, BypassType.Vehicle, out vehicleId, out errorKey);
        }

        public bool TryCancelItemRemoval(UnturnedPlayer player)
        {
            return TryCancelRemoval(player, BypassType.Item);
        }

        public bool TryCancelVehicleRemoval(UnturnedPlayer player)
        {
            return TryCancelRemoval(player, BypassType.Vehicle);
        }

        private bool TryCancelRemoval(UnturnedPlayer player, BypassType type)
        {
            string pendingKey = GetPendingRemovalKey(player.Id, type);
            lock (pendingBypassRemovals)
            {
                PendingBypassRemoval pending;
                if (!pendingBypassRemovals.TryGetValue(pendingKey, out pending) ||
                    pending.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    pendingBypassRemovals.Remove(pendingKey);
                    return false;
                }

                pendingBypassRemovals.Remove(pendingKey);
                return true;
            }
        }

        private bool TryClaim(UnturnedPlayer player, string bypassPermission, BypassType type, ushort id, out string errorKey)
        {
            errorKey = null;
            RocketPermissionsGroup group = GetOwnedGroup(player, out errorKey);
            if (group == null)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(bypassPermission))
            {
                errorKey = "BypassNotEligible";
                return false;
            }

            Permission ownedPermission = group.Permissions.FirstOrDefault(permission =>
                permission != null && string.Equals(permission.Name, bypassPermission, StringComparison.OrdinalIgnoreCase));
            if (ownedPermission != null)
            {
                lock (pendingBypassRemovals)
                {
                    pendingBypassRemovals[GetPendingRemovalKey(player.Id, type)] = new PendingBypassRemoval(
                        type, id, bypassPermission, DateTime.UtcNow.Add(BypassRemovalConfirmationTimeout));
                }

                errorKey = type == BypassType.Item
                    ? "BypassItemRemoveConfirmation"
                    : "BypassVehicleRemoveConfirmation";
                return false;
            }

            if (player.HasPermission(bypassPermission))
            {
                errorKey = "BypassAlreadyOwned";
                return false;
            }

            if (!IsAllowedPermission(bypassPermission))
            {
                errorKey = "BypassNotEligible";
                return false;
            }

            List<DonationBypassTier> matchingTiers = GetMatchingTiers(player);
            if (matchingTiers.Count == 0)
            {
                errorKey = "BypassNoDonationTier";
                return false;
            }
            long maximumBypasses = matchingTiers.Sum(tier => (long)tier.MaxBypasses);

            HashSet<string> knownBypassPermissions = GetKnownBypassPermissions();
            int claimedCount = group.Permissions
                .Where(permission => permission != null)
                .Select(permission => permission.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name) && knownBypassPermissions.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();

            if (claimedCount >= maximumBypasses)
            {
                errorKey = "BypassLimitReached";
                return false;
            }

            group.Permissions.Add(new Permission(bypassPermission, 0));
            if (!SaveGroup(group))
            {
                group.Permissions.RemoveAll(permission =>
                    string.Equals(permission.Name, bypassPermission, StringComparison.OrdinalIgnoreCase));
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            return true;
        }

        private bool TryConfirmRemoval(UnturnedPlayer player, BypassType expectedType, out ushort id, out string errorKey)
        {
            id = 0;
            errorKey = null;
            PendingBypassRemoval pending;
            string pendingKey = GetPendingRemovalKey(player.Id, expectedType);
            lock (pendingBypassRemovals)
            {
                if (!pendingBypassRemovals.TryGetValue(pendingKey, out pending) ||
                    pending.Type != expectedType || pending.ExpiresAtUtc <= DateTime.UtcNow)
                {
                    if (pending != null && pending.ExpiresAtUtc <= DateTime.UtcNow)
                    {
                        pendingBypassRemovals.Remove(pendingKey);
                    }

                    errorKey = "BypassRemovalNoRequest";
                    return false;
                }

                pendingBypassRemovals.Remove(pendingKey);
            }

            RocketPermissionsGroup group = GetOwnedGroup(player, out errorKey);
            if (group == null)
            {
                return false;
            }

            string currentPermission;
            if (expectedType == BypassType.Item)
            {
                GiveSoftBlacklist entry = (plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .FirstOrDefault(item => item != null && item.Id == pending.Id);
                currentPermission = entry == null ? null : entry.Bypass;
            }
            else
            {
                VehicleSpawnBlacklist entry = (plugin.Blacklists.VehicleSpawnBlacklist ?? new List<VehicleSpawnBlacklist>())
                    .FirstOrDefault(vehicle => vehicle != null && vehicle.VehicleId == pending.Id);
                currentPermission = entry == null ? null : entry.Bypass;
            }

            if (!string.Equals(currentPermission, pending.Permission, StringComparison.OrdinalIgnoreCase))
            {
                errorKey = "BypassRemovalTargetChanged";
                return false;
            }

            int permissionIndex = group.Permissions.FindIndex(permission =>
                permission != null && string.Equals(permission.Name, pending.Permission, StringComparison.OrdinalIgnoreCase));
            if (permissionIndex < 0)
            {
                errorKey = "BypassRemovalNoRequest";
                return false;
            }

            Permission permissionToRestore = group.Permissions[permissionIndex];
            group.Permissions.RemoveAt(permissionIndex);
            if (!SaveGroup(group))
            {
                group.Permissions.Insert(permissionIndex, permissionToRestore);
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            id = pending.Id;
            return true;
        }

        private static string GetPendingRemovalKey(string playerId, BypassType type)
        {
            return playerId + ":" + type;
        }

        private enum BypassType
        {
            Item,
            Vehicle
        }

        private sealed class PendingBypassRemoval
        {
            public BypassType Type { get; private set; }
            public ushort Id { get; private set; }
            public string Permission { get; private set; }
            public DateTime ExpiresAtUtc { get; private set; }

            public PendingBypassRemoval(BypassType type, ushort id, string permission, DateTime expiresAtUtc)
            {
                Type = type;
                Id = id;
                Permission = permission;
                ExpiresAtUtc = expiresAtUtc;
            }
        }

        private List<GiveSoftBlacklist> GetEligibleItemEntries()
        {
            HashSet<ushort> excludedIds = new HashSet<ushort>(
                (plugin.Blacklists.GiveItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Concat(plugin.Blacklists.PickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Where(item => item != null)
                    .Select(item => item.Id));

            return (plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                .Where(item => item != null && !excludedIds.Contains(item.Id) && IsAllowedPermission(item.Bypass))
                .ToList();
        }

        private HashSet<string> GetKnownBypassPermissions()
        {
            IEnumerable<string> permissions = (plugin.Blacklists.GiveItemBlacklist ?? new List<GiveSoftBlacklist>())
                .Where(item => item != null)
                .Select(item => item.Bypass)
                .Concat((plugin.Blacklists.PickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Where(item => item != null)
                    .Select(item => item.Bypass))
                .Concat((plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Where(item => item != null)
                    .Select(item => item.Bypass))
                .Concat((plugin.Blacklists.VehicleSpawnBlacklist ?? new List<VehicleSpawnBlacklist>())
                    .Where(vehicle => vehicle != null)
                    .Select(vehicle => vehicle.Bypass))
                .Where(permission => !string.IsNullOrWhiteSpace(permission));
            return new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);
        }

        private bool IsAllowedPermission(string permission)
        {
            return !string.IsNullOrWhiteSpace(permission) &&
                !(plugin.PluginConfiguration.DonationBypasses.DeniedPermissions ?? new List<string>())
                    .Any(denied => string.Equals(denied, permission, StringComparison.OrdinalIgnoreCase));
        }

        private DonationBypassTier GetHighestTier(UnturnedPlayer player)
        {
            return GetMatchingTiers(player).FirstOrDefault();
        }

        private List<DonationBypassTier> GetMatchingTiers(UnturnedPlayer player)
        {
            List<RocketPermissionsGroup> playerGroups = R.Permissions.GetGroups(player, true);
            if (playerGroups == null)
            {
                return new List<DonationBypassTier>();
            }

            return plugin.PluginConfiguration.DonationBypasses.Tiers
                .Where(tier => tier != null && playerGroups.Any(group => group != null &&
                    string.Equals(group.Id, tier.GroupId, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        private RocketPermissionsGroup GetOwnedGroup(UnturnedPlayer player, out string errorKey)
        {
            errorKey = null;
            if (!WasCreated(player.Id))
            {
                errorKey = "BypassGroupRequired";
                return null;
            }

            RocketPermissionsGroup group = R.Permissions.GetGroup(GetPersonalGroupId(player));
            if (group == null)
            {
                errorKey = "BypassGroupMissing";
                return null;
            }

            if (group.Members == null || !group.Members.Contains(player.Id))
            {
                Logger.LogError($"Personal bypass group {group.Id} is not owned by expected player {player.Id}.");
                errorKey = "BypassGroupOwnershipInvalid";
                return null;
            }

            if (group.Permissions == null)
            {
                Logger.LogError($"Personal bypass group {group.Id} has no permissions collection.");
                errorKey = "BypassGroupMissing";
                return null;
            }

            return group;
        }

        private bool WasCreated(string playerId)
        {
            return registry.CreatedPlayerIds.Any(id =>
                string.Equals(id, playerId, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetPersonalGroupId(UnturnedPlayer player)
        {
            return GetPersonalGroupId(player.Id);
        }

        private static string GetPersonalGroupId(string playerId)
        {
            return "ccrp-player-" + playerId;
        }

        private static bool IsValidDisplayName(string displayName)
        {
            return !string.IsNullOrWhiteSpace(displayName) &&
                displayName.Trim().Length <= MaximumDisplayNameLength &&
                displayName.IndexOfAny(new[] { '<', '>' }) < 0;
        }

        private static bool TryNormalizeColor(string color, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(color))
            {
                return false;
            }

            string trimmedColor = color.Trim();
            if (!HexColorPattern.IsMatch(trimmedColor))
            {
                return false;
            }

            if (!trimmedColor.StartsWith("#", StringComparison.Ordinal))
            {
                trimmedColor = "#" + trimmedColor;
            }

            if (!ColorUtility.TryParseHtmlString(trimmedColor, out _))
            {
                return false;
            }

            normalized = trimmedColor.ToUpperInvariant();
            return true;
        }

        private bool SaveGroup(RocketPermissionsGroup group)
        {
            try
            {
                RocketPermissionsProviderResult result = R.Permissions.SaveGroup(group);
                if (result == RocketPermissionsProviderResult.Success)
                {
                    return true;
                }

                Logger.LogError($"Rocket permissions provider failed to save group {group.Id}: {result}.");
                return false;
            }
            catch (Exception exception)
            {
                Logger.LogError($"Failed to save personal bypass group {group.Id}: {exception.Message}");
                return false;
            }
        }

        private bool TrySaveRegistry()
        {
            try
            {
                registryStorage.Save(registry);
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogError("Failed to save DonationBypassGroups.json: " + exception.Message);
                return false;
            }
        }
    }
}
