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
using System.Globalization;
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
        private readonly DataStorage<BypassShopCatalog> shopCatalogStorage;
        private readonly BypassShopCatalog shopCatalog;
        private readonly DataStorage<BypassPurchaseRegistry> purchaseStorage;
        private readonly BypassPurchaseRegistry purchaseRegistry;
        private readonly EconomyService economyService = new EconomyService();
        private readonly object purchaseLock = new object();
        private readonly Dictionary<string, PendingBypassRemoval> pendingBypassRemovals =
            new Dictionary<string, PendingBypassRemoval>(StringComparer.OrdinalIgnoreCase);

        public DonationBypassService(Main plugin)
        {
            this.plugin = plugin;
            registryStorage = new DataStorage<BypassGroupRegistry>(plugin.DataDirectory, "DonationBypassGroups.json", Newtonsoft.Json.Formatting.Indented);
            shopCatalogStorage = new DataStorage<BypassShopCatalog>(plugin.DataDirectory, "BypassShop.json", Newtonsoft.Json.Formatting.Indented);
            purchaseStorage = new DataStorage<BypassPurchaseRegistry>(plugin.DataDirectory, "BypassPurchases.json", Newtonsoft.Json.Formatting.Indented);

            try
            {
                BypassGroupRegistry savedRegistry = registryStorage.Read();
                if ((savedRegistry == null && File.Exists(registryStorage.DataPath)) ||
                    (savedRegistry != null && savedRegistry.CreatedPlayerIds == null))
                {
                    throw new InvalidDataException("DonationBypassGroups.json is missing its player creation records.");
                }

                registry = savedRegistry ?? new BypassGroupRegistry();
                if (registry.ShopCreatedPlayerIds == null)
                {
                    registry.ShopCreatedPlayerIds = new List<string>();
                }

                BypassShopCatalog savedCatalog = shopCatalogStorage.Read();
                if (savedCatalog == null && File.Exists(shopCatalogStorage.DataPath))
                {
                    throw new InvalidDataException("BypassShop.json could not be read.");
                }

                shopCatalog = savedCatalog ?? new BypassShopCatalog();
                if (shopCatalog.Offers == null)
                {
                    throw new InvalidDataException("BypassShop.json is missing its offers collection.");
                }

                BypassPurchaseRegistry savedPurchases = purchaseStorage.Read();
                if (savedPurchases == null && File.Exists(purchaseStorage.DataPath))
                {
                    throw new InvalidDataException("BypassPurchases.json could not be read.");
                }

                purchaseRegistry = savedPurchases ?? new BypassPurchaseRegistry();
                if (purchaseRegistry.Purchases == null)
                {
                    throw new InvalidDataException("BypassPurchases.json is missing its purchases collection.");
                }

                if (savedCatalog == null)
                {
                    shopCatalogStorage.Save(shopCatalog);
                }
                if (savedPurchases == null)
                {
                    purchaseStorage.Save(purchaseRegistry);
                }
                ValidateShopCatalog();
            }
            catch (Exception exception)
            {
                Logger.LogError("Failed to load donation bypass shop data: " + exception.Message);
                throw new InvalidDataException("Could not load donation bypass data.", exception);
            }

        }

        private void ValidateShopCatalog()
        {
            foreach (BypassShopOffer offer in shopCatalog.Offers)
            {
                if (offer == null)
                {
                    Logger.LogError("BypassShop.json contains a null offer; it will not be available.");
                    continue;
                }

                if (FindShopOffers(offer.Type, offer.AssetId).Count != 1)
                {
                    Logger.LogError($"BypassShop.json contains a duplicate offer for {offer.Type} ID {offer.AssetId}; it will not be available.");
                    continue;
                }

                string permission;
                string assetName;
                string errorKey;
                if (!TryResolveShopOffer(offer, out permission, out assetName, out errorKey))
                {
                    Logger.LogError($"BypassShop offer {offer.Type} ID {offer.AssetId} is unavailable: {errorKey}.");
                }
            }
        }

        public bool TrySetGroupName(UnturnedPlayer player, string displayName, out string errorKey)
        {
            errorKey = null;
            DonationBypassTier tier = GetHighestTier(player);
            if (tier == null)
            {
                errorKey = "BypassNoDonationTier";
                return false;
            }

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
            string previousParentGroup = group.ParentGroup;
            group.DisplayName = displayName.Trim();
            group.Prefix = displayName.Trim();
            if (IsShopCreated(player.Id))
            {
                group.ParentGroup = tier.GroupId;
            }
            if (!SaveGroup(group))
            {
                group.DisplayName = previousDisplayName;
                group.Prefix = previousPrefix;
                group.ParentGroup = previousParentGroup;
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            return true;
        }

        public bool TrySetGroupColor(UnturnedPlayer player, string color, out string errorKey)
        {
            errorKey = null;
            DonationBypassTier tier = GetHighestTier(player);
            if (tier == null)
            {
                errorKey = "BypassNoDonationTier";
                return false;
            }

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
            string previousParentGroup = group.ParentGroup;
            group.Color = color;
            if (IsShopCreated(player.Id))
            {
                group.ParentGroup = tier.GroupId;
            }
            if (!SaveGroup(group))
            {
                group.Color = previousColor;
                group.ParentGroup = previousParentGroup;
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

            GiveSoftBlacklist entry = plugin.FindApplicableItemBlacklistEntry(
                plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>(),
                itemId);
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

        public bool TryGetPlayerBypassUsage(
            UnturnedPlayer player,
            out long donorUsed,
            out long donorMaximum,
            out int paidUsed,
            out int paidMaximum,
            out string errorKey)
        {
            donorUsed = 0;
            donorMaximum = 0;
            paidUsed = 0;
            paidMaximum = plugin.PluginConfiguration.DonationBypasses.MaxPaidBypasses;
            errorKey = null;
            if (player == null)
            {
                errorKey = "BypassesPlayerOnly";
                return false;
            }

            List<DonationBypassTier> matchingTiers = GetMatchingTiers(player);
            donorMaximum = matchingTiers.Sum(tier => (long)tier.MaxBypasses);

            RocketPermissionsGroup personalGroup = null;
            if (HasAnyPurchase(player.Id))
            {
                if (!TryGetOrCreateShopGroup(player, out personalGroup, out errorKey))
                {
                    return false;
                }
            }
            else if (WasCreated(player.Id))
            {
                personalGroup = GetOwnedGroup(player, out errorKey);
                if (personalGroup == null)
                {
                    return false;
                }
            }

            HashSet<string> purchasedPermissions = GetPurchasedPermissions(player.Id);
            paidUsed = purchasedPermissions.Count;
            if (personalGroup != null && personalGroup.Permissions != null)
            {
                HashSet<string> knownBypassPermissions = GetKnownBypassPermissions();
                donorUsed = personalGroup.Permissions
                    .Where(permission => permission != null)
                    .Select(permission => permission.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                        knownBypassPermissions.Contains(name) &&
                        !purchasedPermissions.Contains(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .LongCount();
            }

            return true;
        }

        public List<string> GetPlayerBypasses(UnturnedPlayer player, out string errorKey)
        {
            if (HasAnyPurchase(player.Id))
            {
                RocketPermissionsGroup purchasedGroup;
                if (!TryGetOrCreateShopGroup(player, out purchasedGroup, out errorKey))
                {
                    return null;
                }

                return GetBypasses(purchasedGroup);
            }

            if (!WasCreated(player.Id))
            {
                errorKey = null;
                return new List<string>();
            }

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
                IEnumerable<GiveSoftBlacklist> entries = (plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Concat(plugin.Blacklists.GiveItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Concat(plugin.Blacklists.PickupItemBlacklist ?? new List<GiveSoftBlacklist>());
                GiveSoftBlacklist entry = plugin.FindApplicableItemBlacklistEntry(entries, assetId);
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

        public List<string> GetShopOfferLines()
        {
            List<string> lines = new List<string>();
            foreach (BypassShopOffer offer in shopCatalog.Offers
                .Where(item => item != null)
                .OrderBy(item => item.Type, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.AssetId))
            {
                List<BypassShopOffer> matchingOffers = FindShopOffers(offer.Type, offer.AssetId);
                if (matchingOffers.Count != 1 || !ReferenceEquals(matchingOffers[0], offer))
                {
                    continue;
                }

                string bypassPermission;
                string assetName;
                string errorKey;
                if (!TryResolveShopOffer(offer, out bypassPermission, out assetName, out errorKey))
                {
                    continue;
                }

                lines.Add(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}: {1} ({2}) - {3}",
                    offer.Type,
                    assetName,
                    offer.AssetId,
                    offer.Cost.ToString("0.##", CultureInfo.InvariantCulture)));
            }

            return lines;
        }

        public bool TryGetShopCost(string type, ushort assetId, out decimal cost, out string offerType, out string errorKey)
        {
            cost = 0;
            offerType = null;
            errorKey = null;
            List<BypassShopOffer> offers = shopCatalog.Offers
                .Where(offer => offer != null &&
                    offer.AssetId == assetId &&
                    (string.IsNullOrWhiteSpace(type) ||
                     string.Equals(offer.Type, type, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (offers.Count == 0)
            {
                errorKey = "BypassShopOfferUnavailable";
                return false;
            }
            List<BypassShopOffer> validOffers = new List<BypassShopOffer>();
            foreach (BypassShopOffer offer in offers)
            {
                string permission;
                string assetName;
                string offerError;
                if (TryResolveShopOffer(offer, out permission, out assetName, out offerError))
                {
                    validOffers.Add(offer);
                }
            }

            if (validOffers.Count == 0)
            {
                errorKey = "BypassShopOfferUnavailable";
                return false;
            }
            if (validOffers.Count != 1)
            {
                errorKey = "BypassShopOfferAmbiguous";
                return false;
            }

            cost = validOffers[0].Cost;
            offerType = validOffers[0].Type;
            return true;
        }

        public bool TryPurchase(
            UnturnedPlayer player,
            string type,
            ushort assetId,
            out decimal cost,
            out bool alreadyOwned,
            out string errorKey)
        {
            cost = 0;
            alreadyOwned = false;
            errorKey = null;
            if (player == null)
            {
                errorKey = "BypassShopPlayerOnly";
                return false;
            }

            lock (purchaseLock)
            {
                List<BypassShopOffer> offers = FindShopOffers(type, assetId);
                if (offers.Count == 0)
                {
                    errorKey = "BypassShopOfferUnavailable";
                    return false;
                }
                if (offers.Count != 1)
                {
                    errorKey = "BypassShopOfferAmbiguous";
                    return false;
                }

                BypassShopOffer offer = offers[0];
                cost = offer.Cost;
                string bypassPermission;
                string assetName;
                if (!TryResolveShopOffer(offer, out bypassPermission, out assetName, out errorKey))
                {
                    return false;
                }

                if (HasPurchasedPermission(player.Id, bypassPermission))
                {
                    RocketPermissionsGroup ownedGroup;
                    if (!TryGetOrCreateShopGroup(player, out ownedGroup, out errorKey))
                    {
                        return false;
                    }

                    if (!HasGroupPermission(ownedGroup, bypassPermission))
                    {
                        ownedGroup.Permissions.Add(new Permission(bypassPermission, 0));
                        if (!SaveGroup(ownedGroup))
                        {
                            ownedGroup.Permissions.RemoveAll(permission =>
                                permission != null &&
                                string.Equals(permission.Name, bypassPermission, StringComparison.OrdinalIgnoreCase));
                            errorKey = "BypassGroupSaveFailed";
                            return false;
                        }
                    }

                    alreadyOwned = true;
                    return true;
                }

                if (Main.HasBlacklistBypass(player, bypassPermission))
                {
                    errorKey = "BypassShopAlreadyOwned";
                    return false;
                }

                int purchasedCount = GetPurchasedPermissions(player.Id).Count;
                if (purchasedCount >= plugin.PluginConfiguration.DonationBypasses.MaxPaidBypasses)
                {
                    errorKey = "BypassShopPaidLimitReached";
                    return false;
                }

                decimal balance;
                if (!economyService.TryGetBalance(player.Id, out balance, out errorKey))
                {
                    return false;
                }
                if (balance < offer.Cost)
                {
                    errorKey = "BypassShopInsufficientFunds";
                    return false;
                }

                RocketPermissionsGroup group;
                if (!TryGetOrCreateShopGroup(player, out group, out errorKey))
                {
                    return false;
                }
                if (HasGroupPermission(group, bypassPermission))
                {
                    errorKey = "BypassShopAlreadyOwned";
                    return false;
                }

                decimal previousBalance;
                decimal currentBalance;
                if (!economyService.TryChangeBalance(
                    player.Id,
                    -offer.Cost,
                    out previousBalance,
                    out currentBalance,
                    out errorKey))
                {
                    if (currentBalance != previousBalance &&
                        !TryRefund(player.Id, previousBalance - currentBalance))
                    {
                        errorKey = "BypassShopRefundFailed";
                    }
                    return false;
                }

                Permission purchasedPermission = new Permission(bypassPermission, 0);
                group.Permissions.Add(purchasedPermission);
                if (!SaveGroup(group))
                {
                    group.Permissions.Remove(purchasedPermission);
                    if (!TryRefund(player.Id, offer.Cost))
                    {
                        errorKey = "BypassShopRefundFailed";
                    }
                    else
                    {
                        errorKey = "BypassGroupSaveFailed";
                    }
                    return false;
                }

                BypassPurchase purchase = new BypassPurchase
                {
                    PlayerId = player.Id,
                    Type = offer.Type,
                    AssetId = offer.AssetId,
                    BypassPermission = bypassPermission,
                    Cost = offer.Cost,
                    PurchasedAtUtc = DateTime.UtcNow
                };
                purchaseRegistry.Purchases.Add(purchase);
                if (!TrySavePurchaseRegistry())
                {
                    purchaseRegistry.Purchases.Remove(purchase);
                    group.Permissions.Remove(purchasedPermission);
                    bool permissionRolledBack = SaveGroup(group);
                    if (!permissionRolledBack)
                    {
                        group.Permissions.Add(purchasedPermission);
                        purchaseRegistry.Purchases.Add(purchase);
                        if (SaveGroup(group) && TrySavePurchaseRegistry())
                        {
                            Logger.LogWarning($"Completed bypass purchase for {player.Id} after the initial purchase record save failed.");
                            return true;
                        }

                        Logger.LogError($"Could not reliably persist paid bypass permission {bypassPermission} for {player.Id} after the purchase record save failed.");
                        errorKey = "BypassShopPurchaseRecordFailed";
                        return false;
                    }

                    if (TryRefund(player.Id, offer.Cost))
                    {
                        errorKey = "BypassShopPurchaseRecordFailed";
                        return false;
                    }

                    group.Permissions.Add(purchasedPermission);
                    purchaseRegistry.Purchases.Add(purchase);
                    bool permissionRestored = SaveGroup(group);
                    bool recordRestored = TrySavePurchaseRegistry();
                    if (permissionRestored && recordRestored)
                    {
                        Logger.LogWarning($"Completed bypass purchase for {player.Id} because the charge could not be refunded.");
                        return true;
                    }

                    errorKey = "BypassShopRefundFailed";
                    return false;
                }

                return true;
            }
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
            List<string> shopCreatedPlayerIds = registry.ShopCreatedPlayerIds
                .Where(id => string.Equals(id, playerId, StringComparison.OrdinalIgnoreCase))
                .ToList();
            registry.CreatedPlayerIds.RemoveAll(id =>
                string.Equals(id, playerId, StringComparison.OrdinalIgnoreCase));
            registry.ShopCreatedPlayerIds.RemoveAll(id =>
                string.Equals(id, playerId, StringComparison.OrdinalIgnoreCase));
            if (!TrySaveRegistry())
            {
                registry.CreatedPlayerIds.AddRange(createdPlayerIds);
                registry.ShopCreatedPlayerIds.AddRange(shopCreatedPlayerIds);
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

            if (item.IsItemTypeEntry)
            {
                bypasses.Add($"Item type: {item.ItemType}");
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

        private List<BypassShopOffer> FindShopOffers(string type, ushort assetId)
        {
            return shopCatalog.Offers
                .Where(offer => offer != null &&
                    offer.AssetId == assetId &&
                    string.Equals(offer.Type, type, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private bool TryResolveShopOffer(
            BypassShopOffer offer,
            out string bypassPermission,
            out string assetName,
            out string errorKey)
        {
            bypassPermission = null;
            assetName = null;
            errorKey = null;
            if (offer == null || offer.AssetId == 0 || offer.Cost <= 0 ||
                decimal.Round(offer.Cost, 2) != offer.Cost)
            {
                errorKey = "BypassShopOfferInvalid";
                return false;
            }

            if (string.Equals(offer.Type, "item", StringComparison.OrdinalIgnoreCase))
            {
                ItemAsset asset = Assets.find(EAssetType.ITEM, offer.AssetId) as ItemAsset;
                if (asset == null)
                {
                    errorKey = "BypassShopOfferUnavailable";
                    return false;
                }

                assetName = asset.itemName;
                IEnumerable<GiveSoftBlacklist> entries =
                    (plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Concat(plugin.Blacklists.GiveItemBlacklist ?? new List<GiveSoftBlacklist>())
                    .Concat(plugin.Blacklists.PickupItemBlacklist ?? new List<GiveSoftBlacklist>());
                List<string> permissions = new List<string>();
                foreach (GiveSoftBlacklist entry in entries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    bool applies = entry.IsItemTypeEntry
                        ? plugin.FindApplicableItemBlacklistEntry(new[] { entry }, offer.AssetId) != null
                        : entry.Id == offer.AssetId;
                    if (!applies)
                    {
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(entry.Bypass))
                    {
                        errorKey = "BypassShopOfferPermissionInvalid";
                        return false;
                    }

                    permissions.Add(entry.Bypass);
                }

                List<string> uniquePermissions = permissions
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (uniquePermissions.Count == 0)
                {
                    errorKey = "BypassShopOfferUnavailable";
                    return false;
                }
                if (uniquePermissions.Count != 1)
                {
                    errorKey = "BypassShopOfferAmbiguous";
                    return false;
                }

                bypassPermission = uniquePermissions[0];
            }
            else if (string.Equals(offer.Type, "vehicle", StringComparison.OrdinalIgnoreCase))
            {
                VehicleAsset asset = Assets.find(EAssetType.VEHICLE, offer.AssetId) as VehicleAsset;
                if (asset == null)
                {
                    errorKey = "BypassShopOfferUnavailable";
                    return false;
                }

                assetName = asset.vehicleName;
                List<string> permissions = (plugin.Blacklists.VehicleSpawnBlacklist ?? new List<VehicleSpawnBlacklist>())
                    .Where(vehicle => vehicle != null && vehicle.VehicleId == offer.AssetId)
                    .Select(vehicle => vehicle.Bypass)
                    .ToList();
                if (permissions.Any(string.IsNullOrWhiteSpace))
                {
                    errorKey = "BypassShopOfferPermissionInvalid";
                    return false;
                }

                List<string> uniquePermissions = permissions
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (uniquePermissions.Count == 0)
                {
                    errorKey = "BypassShopOfferUnavailable";
                    return false;
                }
                if (uniquePermissions.Count != 1)
                {
                    errorKey = "BypassShopOfferAmbiguous";
                    return false;
                }

                bypassPermission = uniquePermissions[0];
            }
            else
            {
                errorKey = "BypassShopOfferInvalid";
                return false;
            }

            if (!IsAllowedPermission(bypassPermission))
            {
                errorKey = "BypassShopPermissionDenied";
                return false;
            }

            return true;
        }

        private bool TryGetOrCreateShopGroup(
            UnturnedPlayer player,
            out RocketPermissionsGroup group,
            out string errorKey)
        {
            errorKey = null;
            group = null;
            string groupId = GetPersonalGroupId(player);
            bool hasPurchase = HasAnyPurchase(player.Id);
            group = R.Permissions.GetGroup(groupId);

            if (group != null)
            {
                if (!WasCreated(player.Id) && !hasPurchase)
                {
                    Logger.LogError($"Refusing to use unregistered Rocket permissions group {groupId} for {player.Id}.");
                    errorKey = "BypassGroupOwnershipInvalid";
                    return false;
                }
                if (group.Members == null || !group.Members.Contains(player.Id) || group.Permissions == null)
                {
                    Logger.LogError($"Personal bypass group {groupId} is not owned by expected player {player.Id}.");
                    errorKey = "BypassGroupOwnershipInvalid";
                    return false;
                }

                List<Permission> previousPermissions = new List<Permission>(group.Permissions);
                AddPurchasedPermissions(player.Id, group.Permissions);
                if (group.Permissions.Count != previousPermissions.Count && !SaveGroup(group))
                {
                    group.Permissions = previousPermissions;
                    errorKey = "BypassGroupSaveFailed";
                    return false;
                }

                if (!WasCreated(player.Id))
                {
                    registry.CreatedPlayerIds.Add(player.Id);
                    if (!IsShopCreated(player.Id))
                    {
                        registry.ShopCreatedPlayerIds.Add(player.Id);
                    }
                    if (!TrySaveRegistry())
                    {
                        registry.CreatedPlayerIds.RemoveAll(id =>
                            string.Equals(id, player.Id, StringComparison.OrdinalIgnoreCase));
                        registry.ShopCreatedPlayerIds.RemoveAll(id =>
                            string.Equals(id, player.Id, StringComparison.OrdinalIgnoreCase));
                        errorKey = "BypassGroupSaveFailed";
                        return false;
                    }
                }
                return true;
            }

            if (WasCreated(player.Id) && !IsShopCreated(player.Id) && !hasPurchase)
            {
                errorKey = "BypassGroupMissing";
                return false;
            }

            group = CreateShopGroup(player);
            RocketPermissionsProviderResult addResult;
            try
            {
                addResult = R.Permissions.AddGroup(group);
            }
            catch (Exception exception)
            {
                Logger.LogError($"Could not create shop bypass group for {player.Id}: {exception.Message}");
                errorKey = "BypassGroupSaveFailed";
                return false;
            }
            if (addResult != RocketPermissionsProviderResult.Success)
            {
                Logger.LogError($"Could not create shop bypass group for {player.Id}: {addResult}.");
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            if (!WasCreated(player.Id))
            {
                registry.CreatedPlayerIds.Add(player.Id);
            }
            if (!IsShopCreated(player.Id))
            {
                registry.ShopCreatedPlayerIds.Add(player.Id);
            }
            if (!TrySaveRegistry())
            {
                registry.CreatedPlayerIds.RemoveAll(id =>
                    string.Equals(id, player.Id, StringComparison.OrdinalIgnoreCase));
                registry.ShopCreatedPlayerIds.RemoveAll(id =>
                    string.Equals(id, player.Id, StringComparison.OrdinalIgnoreCase));
                try
                {
                    RocketPermissionsProviderResult deleteResult = R.Permissions.DeleteGroup(group.Id);
                    if (deleteResult != RocketPermissionsProviderResult.Success)
                    {
                        Logger.LogError($"Could not roll back shop bypass group {group.Id} after registry save failed: {deleteResult}.");
                    }
                }
                catch (Exception exception)
                {
                    Logger.LogError($"Could not roll back shop bypass group {group.Id} after registry save failed: {exception.Message}");
                }

                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            AddPurchasedPermissions(player.Id, group.Permissions);
            if (group.Permissions.Count > 0 && !SaveGroup(group))
            {
                errorKey = "BypassGroupSaveFailed";
                return false;
            }

            return true;
        }

        private static RocketPermissionsGroup CreateShopGroup(UnturnedPlayer player)
        {
            return new RocketPermissionsGroup
            {
                Id = GetPersonalGroupId(player),
                DisplayName = null,
                Prefix = null,
                Suffix = null,
                Color = null,
                ParentGroup = null,
                Members = new List<string> { player.Id },
                Permissions = new List<Permission>()
            };
        }

        private bool TryAttachDonorParent(
            RocketPermissionsGroup group,
            DonationBypassTier tier,
            out string errorKey)
        {
            errorKey = null;
            if (string.Equals(group.ParentGroup, tier.GroupId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string previousParentGroup = group.ParentGroup;
            group.ParentGroup = tier.GroupId;
            if (SaveGroup(group))
            {
                return true;
            }

            group.ParentGroup = previousParentGroup;
            errorKey = "BypassGroupSaveFailed";
            return false;
        }

        private bool TryRefund(string playerId, decimal amount)
        {
            if (amount <= 0)
            {
                return true;
            }

            decimal previousBalance;
            decimal currentBalance;
            string errorKey;
            if (economyService.TryChangeBalance(
                playerId,
                amount,
                out previousBalance,
                out currentBalance,
                out errorKey))
            {
                return true;
            }

            Logger.LogError($"Could not refund {amount.ToString(CultureInfo.InvariantCulture)} to {playerId} after a bypass purchase failure ({errorKey}).");
            return false;
        }

        private bool TrySavePurchaseRegistry()
        {
            try
            {
                purchaseStorage.Save(purchaseRegistry);
                return true;
            }
            catch (Exception exception)
            {
                Logger.LogError("Failed to save BypassPurchases.json: " + exception.Message);
                return false;
            }
        }

        private bool HasAnyPurchase(string playerId)
        {
            return purchaseRegistry.Purchases.Any(purchase => purchase != null &&
                string.Equals(purchase.PlayerId, playerId, StringComparison.OrdinalIgnoreCase));
        }

        private bool HasPurchasedPermission(string playerId, string bypassPermission)
        {
            return purchaseRegistry.Purchases.Any(purchase => purchase != null &&
                string.Equals(purchase.PlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(purchase.BypassPermission, bypassPermission, StringComparison.OrdinalIgnoreCase));
        }

        private HashSet<string> GetPurchasedPermissions(string playerId)
        {
            return new HashSet<string>(
                purchaseRegistry.Purchases
                    .Where(purchase => purchase != null &&
                        string.Equals(purchase.PlayerId, playerId, StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(purchase.BypassPermission))
                    .Select(purchase => purchase.BypassPermission),
                StringComparer.OrdinalIgnoreCase);
        }

        private void AddPurchasedPermissions(string playerId, List<Permission> permissions)
        {
            foreach (string bypassPermission in GetPurchasedPermissions(playerId))
            {
                if (!HasGroupPermission(permissions, bypassPermission))
                {
                    permissions.Add(new Permission(bypassPermission, 0));
                }
            }
        }

        private bool IsShopCreated(string playerId)
        {
            return registry.ShopCreatedPlayerIds.Any(id =>
                string.Equals(id, playerId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasGroupPermission(RocketPermissionsGroup group, string bypassPermission)
        {
            return group != null && HasGroupPermission(group.Permissions, bypassPermission);
        }

        private static bool HasGroupPermission(List<Permission> permissions, string bypassPermission)
        {
            return permissions != null && permissions.Any(permission => permission != null &&
                string.Equals(permission.Name, bypassPermission, StringComparison.OrdinalIgnoreCase));
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
            RocketPermissionsGroup group = null;
            if (WasCreated(player.Id))
            {
                group = GetOwnedGroup(player, out errorKey);
                if (group == null)
                {
                    return false;
                }
            }
            else if (HasAnyPurchase(player.Id))
            {
                if (!TryGetOrCreateShopGroup(player, out group, out errorKey))
                {
                    return false;
                }
            }
            else if (R.Permissions.GetGroup(GetPersonalGroupId(player)) != null)
            {
                Logger.LogError($"Refusing to use unregistered Rocket permissions group {GetPersonalGroupId(player)} for {player.Id}.");
                errorKey = "BypassGroupOwnershipInvalid";
                return false;
            }

            if (string.IsNullOrWhiteSpace(bypassPermission))
            {
                errorKey = "BypassNotEligible";
                return false;
            }

            if (HasPurchasedPermission(player.Id, bypassPermission))
            {
                if (!TryGetOrCreateShopGroup(player, out group, out errorKey))
                {
                    return false;
                }

                if (!HasGroupPermission(group, bypassPermission))
                {
                    group.Permissions.Add(new Permission(bypassPermission, 0));
                    if (!SaveGroup(group))
                    {
                        group.Permissions.RemoveAll(permission =>
                            string.Equals(permission.Name, bypassPermission, StringComparison.OrdinalIgnoreCase));
                        errorKey = "BypassGroupSaveFailed";
                        return false;
                    }
                }

                errorKey = "BypassPurchasedLocked";
                return false;
            }

            if (group != null && HasGroupPermission(group, bypassPermission))
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
            HashSet<string> purchasedPermissions = GetPurchasedPermissions(player.Id);
            long claimedCount = group == null
                ? 0
                : group.Permissions
                    .Where(permission => permission != null)
                    .Select(permission => permission.Name)
                    .Where(name => !string.IsNullOrWhiteSpace(name) &&
                        knownBypassPermissions.Contains(name) &&
                        !purchasedPermissions.Contains(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .LongCount();

            if (claimedCount >= maximumBypasses)
            {
                errorKey = "BypassLimitReached";
                return false;
            }

            if (group == null && !TryGetOrCreateShopGroup(player, out group, out errorKey))
            {
                return false;
            }

            if (IsShopCreated(player.Id) && !TryAttachDonorParent(group, matchingTiers[0], out errorKey))
            {
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
                GiveSoftBlacklist entry = plugin.FindApplicableItemBlacklistEntry(
                    plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>(),
                    pending.Id);
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

            if (HasPurchasedPermission(player.Id, pending.Permission))
            {
                errorKey = "BypassPurchasedLocked";
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
            List<GiveSoftBlacklist> excludedEntries = (plugin.Blacklists.GiveItemBlacklist ?? new List<GiveSoftBlacklist>())
                .Concat(plugin.Blacklists.PickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                .Where(item => item != null)
                .ToList();

            return (plugin.Blacklists.GiveAndPickupItemBlacklist ?? new List<GiveSoftBlacklist>())
                .Where(item => item != null &&
                    !excludedEntries.Any(excluded => HasSameItemSelector(item, excluded)) &&
                    IsAllowedPermission(item.Bypass))
                .ToList();
        }

        private static bool HasSameItemSelector(GiveSoftBlacklist first, GiveSoftBlacklist second)
        {
            if (first.IsItemTypeEntry != second.IsItemTypeEntry)
            {
                return false;
            }

            return first.IsItemTypeEntry
                ? string.Equals(first.ItemType, second.ItemType, StringComparison.OrdinalIgnoreCase)
                : first.Id == second.Id;
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
