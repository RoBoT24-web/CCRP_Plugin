using CCRP_Plugin.Database;
using CCRP_Plugin.Models;
using CCRP_Plugin.Services;
using CCRPPlugin.Database;
using CCRPPlugin.Commands;
using CCRPPlugin.Storage;
using Rocket.API;
using Rocket.API.Collections;
using Rocket.API.Serialisation;
using Rocket.Core;
using Rocket.Core.Plugins;
using Rocket.Unturned;
using Rocket.Unturned.Chat;
using Rocket.Unturned.Events;
using Rocket.Unturned.Player;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Logger = Rocket.Core.Logging.Logger;

namespace CCRPPlugin
{
    public class Main : RocketPlugin<CCRPConfig>
    {
        public static Main Instance { get; private set; }

        public UnityEngine.Color MessageColor { get; private set; }

        //Sevices
        public WarnService WarnService { get; private set; }
        public WarnLogService WarnLogService { get; private set; }
        public PlaytimeService PlaytimeService { get; private set; }
        public VehicleCleanupService VehicleCleanupService { get; private set; }
        public WreckService WreckService { get; private set; }

        //Database
        public WarnDatabase WarnDatabase { get; private set; }

        public PlayTimeDatabase PlayTimeDatabase { get; private set; }

        public HomeDatabase HomeDatabase { get; private set; }

        public WarnLogDatabase WarnLogDatabase { get; private set; }

        public BlacklistSettings Blacklists { get; private set; }

        public CCRPConfig PluginConfiguration => Configuration.Instance;

        public string DataDirectory { get; private set; }

        public DonationBypassService DonationBypassService { get; private set; }
        public DeathLocationService DeathLocationService { get; private set; }

        public List<DutyGroups> ValidGroups;
        private readonly Dictionary<ushort, List<string>> giveItemBypassPermissions = new Dictionary<ushort, List<string>>();
        private readonly Dictionary<ushort, List<string>> pickupItemBypassPermissions = new Dictionary<ushort, List<string>>();
        private readonly Dictionary<EItemType, List<string>> giveItemTypeBypassPermissions = new Dictionary<EItemType, List<string>>();
        private readonly Dictionary<EItemType, List<string>> pickupItemTypeBypassPermissions = new Dictionary<EItemType, List<string>>();
        private DataStorage<BlacklistSettings> blacklistStorage;


        protected override void Load()
        {
            Instance = this;
            InitializeDataDirectory();
            NormalizeConfiguration();

            //Database
            WarnDatabase = new WarnDatabase();
            WarnDatabase.Reload(); 

            WarnLogDatabase = new WarnLogDatabase();
            WarnLogDatabase.Reload();

            PlayTimeDatabase = new PlayTimeDatabase();
            PlayTimeDatabase.Reload();

            HomeDatabase = new HomeDatabase();
            HomeDatabase.Reload();

            LoadBlacklists();
            DonationBypassService = new DonationBypassService(this);
            ValidateWarnPunishments();

            //Services
            WarnService = gameObject.AddComponent<WarnService>();
            WarnLogService = gameObject.AddComponent<WarnLogService>();
            PlaytimeService = gameObject.AddComponent<PlaytimeService>();
            VehicleCleanupService = gameObject.AddComponent<VehicleCleanupService>();
            WreckService = gameObject.AddComponent<WreckService>();
            TeleportService.Initialize(this);
            DeathLocationService = CCRP_Plugin.Services.DeathLocationService.Initialize(this);
            CCRPFreezeManager.Initialize(gameObject);
            TeleportService.IsTeleportBlocked = CCRPFreezeManager.IsFrozen;
            ChatFeaturesService.Initialize(
                Configuration.Instance.PrivateMessages,
                Configuration.Instance.Votes,
                Configuration.Instance.AutoAnnouncer);

            //Valid Groups
            ValidGroups = Configuration.Instance.Groups;

            foreach (DutyGroups Group in ValidGroups.ToList())
            {
                RocketPermissionsGroup g = R.Permissions.GetGroup(Group.GroupID);
                if (g == null)
                {
                    Logger.LogWarning("Permission group " + Group.GroupID + " does not exist!");
                    ValidGroups.Remove(Group);
                }
            }

            //Events
            U.Events.OnPlayerConnected += OnPlayerConnected;
            U.Events.OnPlayerDisconnected += OnPlayerDisconnected;
            UnturnedPlayerEvents.OnPlayerDeath += OnPlayerDeath;
            UnturnedPlayerEvents.OnPlayerRevive += OnPlayerRevive;
            ItemManager.onTakeItemRequested += OnTakeItemRequested;
            VehicleManager.onEnterVehicleRequested += OnEnterVehicleRequested;
            VehicleManager.onSwapSeatRequested += OnSwapSeatRequested;
            BarricadeManager.onTransformRequested += OnBarricadeTransformRequested;
            StructureManager.onTransformRequested += OnStructureTransformRequested;

            //Color Chat Msg
            MessageColor = UnturnedChat.GetColorFromName(Configuration.Instance.MessageColor, UnityEngine.Color.cyan);
            //Start IDs
            Logger.Log("");
            Logger.Log("==========================");
            Logger.Log("");
            Logger.Log("Blacklisted Items");
            foreach (var item in Blacklists.GiveItemBlacklist.Select(FormatItemBlacklistEntry))
            {
                Logger.Log(item);
            }
            foreach (var item in Blacklists.GiveAndPickupItemBlacklist.Select(x => FormatItemBlacklistEntry(x) + " (give and pickup)"))
            {
                Logger.Log(item);
            }
            Logger.Log("Pickup-only Blacklisted Items");
            foreach (var item in Blacklists.PickupItemBlacklist.Select(FormatItemBlacklistEntry))
            {
                Logger.Log(item);
            }
            Logger.Log("");
            Logger.Log("==========================");
            Logger.Log("");
            Logger.Log("Blacklisted Vehicles");
            foreach (var vehicle in Blacklists.VehicleSpawnBlacklist.Select(x => $"ID: {x.VehicleId} | " +
            $"Name: {Assets.find(EAssetType.ITEM, x.VehicleId)?.name ?? "UNKNOWN"} | " +
            $"Bypass: {x.Bypass ?? "NONE"}"))
            {
                Logger.Log(vehicle);
            }

            //Logger.Log(Configuration.Instance.LoadMsg);
            Logger.Log($"{Name} with the version V2.0.0 has been loaded!");
            Logger.Log("Plugin edited by RoBoT24");
            Logger.Log("Discord Tag: robot24");
            Logger.Log("==========================");
        }

        private void InitializeDataDirectory()
        {
            DataDirectory = Path.Combine(Directory, "Data");
            System.IO.Directory.CreateDirectory(DataDirectory);
            string migrationMarker = Path.Combine(DataDirectory, ".legacy-json-migrated");
            if (File.Exists(migrationMarker))
            {
                return;
            }

            foreach (string sourcePath in System.IO.Directory.GetFiles(Directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                string destinationPath = Path.Combine(DataDirectory, Path.GetFileName(sourcePath));
                if (File.Exists(destinationPath))
                {
                    continue;
                }

                try
                {
                    File.Copy(sourcePath, destinationPath, false);
                    Logger.Log($"Migrated plugin data file {Path.GetFileName(sourcePath)} to the Data folder. The original was retained.");
                }
                catch (IOException exception)
                {
                    Logger.LogError($"Failed to migrate plugin data file {Path.GetFileName(sourcePath)}: {exception.Message}");
                    throw;
                }
                catch (UnauthorizedAccessException exception)
                {
                    Logger.LogError($"Insufficient access to migrate plugin data file {Path.GetFileName(sourcePath)}: {exception.Message}");
                    throw;
                }
            }

            try
            {
                File.WriteAllText(migrationMarker, string.Empty);
            }
            catch (IOException exception)
            {
                Logger.LogError("Failed to mark the legacy JSON migration as complete: " + exception.Message);
                throw;
            }
            catch (UnauthorizedAccessException exception)
            {
                Logger.LogError("Insufficient access to mark the legacy JSON migration as complete: " + exception.Message);
                throw;
            }
        }

        private void NormalizeConfiguration()
        {
            if (Configuration.Instance.Warnings == null)
            {
                Configuration.Instance.Warnings = new WarningSettings();
            }
            if (Configuration.Instance.General == null)
            {
                Configuration.Instance.General = new GeneralSettings();
            }
            if (Configuration.Instance.Webhooks == null)
            {
                Configuration.Instance.Webhooks = new WebhookSettings();
            }

            if (Configuration.Instance.VehicleCleanup == null)
            {
                Configuration.Instance.VehicleCleanup = new VehicleCleanupSettings();
            }
            if (Configuration.Instance.VehicleCleanup.EmptyVehicles == null)
            {
                Configuration.Instance.VehicleCleanup.EmptyVehicles = new VehicleCleanupTask();
            }
            if (Configuration.Instance.VehicleCleanup.AllVehicles == null)
            {
                Configuration.Instance.VehicleCleanup.AllVehicles = new VehicleCleanupTask { IntervalMinutes = 120 };
            }
            if (Configuration.Instance.MaxSkills == null)
            {
                Configuration.Instance.MaxSkills = new MaxSkillsSettings();
            }
            if (Configuration.Instance.MaxSkills.IgnoredSkills == null)
            {
                Configuration.Instance.MaxSkills.IgnoredSkills = new List<string>();
            }
            if (Configuration.Instance.PrivateMessages == null)
            {
                Configuration.Instance.PrivateMessages = new PrivateMessageSettings();
            }
            if (Configuration.Instance.Votes == null)
            {
                Configuration.Instance.Votes = new VoteSettings { Definitions = VoteSettings.CreateDefaults() };
            }
            else if (Configuration.Instance.Votes.Definitions == null)
            {
                Configuration.Instance.Votes.Definitions = VoteSettings.CreateDefaults();
            }
            if (Configuration.Instance.AutoAnnouncer == null)
            {
                Configuration.Instance.AutoAnnouncer = new AutoAnnouncerSettings();
            }
            if (Configuration.Instance.DonationBypasses == null)
            {
                Configuration.Instance.DonationBypasses = new DonationBypassSettings();
            }
            if (Configuration.Instance.DonationBypasses.Tiers == null)
            {
                Configuration.Instance.DonationBypasses.Tiers = new List<DonationBypassTier>();
            }
            if (Configuration.Instance.DonationBypasses.DeniedPermissions == null)
            {
                Configuration.Instance.DonationBypasses.DeniedPermissions = new List<string>();
            }
            if (Configuration.Instance.DonationBypasses.MaxPaidBypasses < 0)
            {
                Logger.LogError("DonationBypasses.MaxPaidBypasses cannot be negative.");
                throw new InvalidDataException("DonationBypasses.MaxPaidBypasses cannot be negative.");
            }
            // Clean up duplicates written by earlier versions.
            Configuration.Instance.DonationBypasses.DeniedPermissions = Configuration.Instance.DonationBypasses.DeniedPermissions
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(System.StringComparer.OrdinalIgnoreCase)
                .ToList();
            Configuration.Save();
            HashSet<string> donationGroupIds = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (DonationBypassTier tier in Configuration.Instance.DonationBypasses.Tiers)
            {
                if (tier == null || string.IsNullOrWhiteSpace(tier.GroupId) || tier.MaxBypasses < 0 ||
                    !donationGroupIds.Add(tier.GroupId))
                {
                    Logger.LogError("DonationBypasses contains an invalid tier or duplicate group ID.");
                    throw new InvalidDataException("DonationBypasses contains an invalid tier or duplicate group ID.");
                }
            }

            foreach (System.Xml.XmlElement legacyElement in Configuration.Instance.LegacyElements ?? new System.Xml.XmlElement[0])
            {
                switch (legacyElement.Name)
                {
                    case "WarnPunishments":
                        Configuration.Instance.WarnPunishments = ReadLegacyWarnPunishments(legacyElement);
                        break;
                    case "MessageColor":
                        Configuration.Instance.MessageColor = legacyElement.InnerText;
                        break;
                    case "KickWebHook":
                        Configuration.Instance.Webhooks.Kick = legacyElement.InnerText;
                        break;
                    case "BanWebHook":
                        Configuration.Instance.Webhooks.Ban = legacyElement.InnerText;
                        break;
                    case "WarnWebHook":
                        Configuration.Instance.Webhooks.Warn = legacyElement.InnerText;
                        break;
                    case "BuilderModeWebHook":
                        Configuration.Instance.Webhooks.BuilderMode = legacyElement.InnerText;
                        break;
                    case "TeleportWebHook":
                        Configuration.Instance.Webhooks.Teleport = legacyElement.InnerText;
                        break;
                    case "Groups":
                        Configuration.Instance.LegacyGroups = ReadLegacyDutyGroups(legacyElement);
                        Configuration.Instance.Groups = Configuration.Instance.LegacyGroups;
                        break;
                    case "GiveItemBlacklist":
                        Configuration.Instance.LegacyGiveItemBlacklist = ReadLegacyItemBlacklist(legacyElement);
                        break;
                    case "VehicleSpawnBlacklist":
                        Configuration.Instance.LegacyVehicleSpawnBlacklist = ReadLegacyVehicleBlacklist(legacyElement);
                        break;
                }
            }

            if (Configuration.Instance.Groups == null)
            {
                Configuration.Instance.Groups = new List<DutyGroups>();
            }

            Configuration.Instance.LegacyElements = null;
        }

        private static List<WarnPunishment> ReadLegacyWarnPunishments(System.Xml.XmlElement element)
        {
            List<WarnPunishment> punishments = new List<WarnPunishment>();
            foreach (System.Xml.XmlElement punishmentElement in element.ChildNodes.OfType<System.Xml.XmlElement>())
            {
                int warnCount = 0;
                int durationSeconds = 0;
                bool permanent = false;
                WarnPunishmentAction action = WarnPunishmentAction.Kick;
                string reason = punishmentElement["Reason"]?.InnerText;
                if (!int.TryParse(punishmentElement.GetAttribute("WarnCount"), NumberStyles.None, CultureInfo.InvariantCulture, out warnCount) ||
                    !System.Enum.TryParse(punishmentElement.GetAttribute("Action"), true, out action) ||
                    !int.TryParse(punishmentElement.GetAttribute("DurationSeconds"), NumberStyles.None, CultureInfo.InvariantCulture, out durationSeconds) ||
                    !bool.TryParse(punishmentElement.GetAttribute("Permanent"), out permanent) ||
                    string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidDataException("Legacy WarnPunishments contains an invalid entry.");
                }

                punishments.Add(new WarnPunishment
                {
                    WarnCount = warnCount,
                    Action = action,
                    DurationSeconds = durationSeconds,
                    Permanent = permanent,
                    Reason = reason
                });
            }

            return punishments;
        }

        private static List<DutyGroups> ReadLegacyDutyGroups(System.Xml.XmlElement element)
        {
            List<DutyGroups> groups = new List<DutyGroups>();
            foreach (System.Xml.XmlElement groupElement in element.ChildNodes.OfType<System.Xml.XmlElement>())
            {
                string groupId = groupElement["GroupID"]?.InnerText;
                string permission = groupElement["Permission"]?.InnerText;
                if (string.IsNullOrWhiteSpace(groupId) || string.IsNullOrWhiteSpace(permission))
                {
                    throw new InvalidDataException("Legacy Groups contains an invalid entry.");
                }

                groups.Add(new DutyGroups(groupId, permission));
            }

            return groups;
        }

        private static List<GiveSoftBlacklist> ReadLegacyItemBlacklist(System.Xml.XmlElement element)
        {
            List<GiveSoftBlacklist> entries = new List<GiveSoftBlacklist>();
            foreach (System.Xml.XmlElement itemElement in element.ChildNodes.OfType<System.Xml.XmlElement>())
            {
                string bypass = itemElement.GetAttribute("BypassPermission");
                string itemTypeName = itemElement.GetAttribute("ItemType");
                if (string.IsNullOrWhiteSpace(bypass))
                {
                    throw new InvalidDataException("Legacy GiveItemBlacklist contains an invalid entry.");
                }

                if (!string.IsNullOrWhiteSpace(itemTypeName))
                {
                    EItemType itemType;
                    if (!TryParseItemType(itemTypeName, out itemType))
                    {
                        throw new InvalidDataException("Legacy GiveItemBlacklist contains an invalid item type.");
                    }

                    entries.Add(new GiveSoftBlacklist { ItemType = itemType.ToString(), Bypass = bypass });
                    continue;
                }

                ushort id;
                if (!ushort.TryParse(itemElement.InnerText, out id) || id == 0)
                {
                    throw new InvalidDataException("Legacy GiveItemBlacklist contains an invalid entry.");
                }

                entries.Add(new GiveSoftBlacklist { Id = id, Bypass = bypass });
            }

            return entries;
        }

        private static List<VehicleSpawnBlacklist> ReadLegacyVehicleBlacklist(System.Xml.XmlElement element)
        {
            List<VehicleSpawnBlacklist> entries = new List<VehicleSpawnBlacklist>();
            foreach (System.Xml.XmlElement vehicleElement in element.ChildNodes.OfType<System.Xml.XmlElement>())
            {
                ushort id;
                string bypass = vehicleElement.GetAttribute("BypassPermission");
                if (!ushort.TryParse(vehicleElement.InnerText, out id) || id == 0 || string.IsNullOrWhiteSpace(bypass))
                {
                    throw new InvalidDataException("Legacy VehicleSpawnBlacklist contains an invalid entry.");
                }

                entries.Add(new VehicleSpawnBlacklist { VehicleId = id, Bypass = bypass });
            }

            return entries;
        }

        private void LoadBlacklists()
        {
            blacklistStorage = new DataStorage<BlacklistSettings>(DataDirectory, "Blacklists.json", Newtonsoft.Json.Formatting.Indented);
            bool needsSave = false;
            try
            {
                Blacklists = blacklistStorage.Read();
            }
            catch (System.Exception exception)
            {
                Logger.LogError("Failed to read Blacklists.json: " + exception.Message);
                throw;
            }

            if (Blacklists == null)
            {
                Blacklists = new BlacklistSettings
                {
                    GiveItemBlacklist = Configuration.Instance.LegacyGiveItemBlacklist ?? new List<GiveSoftBlacklist>
                    {
                        new GiveSoftBlacklist { Bypass = "bypass.1441", Id = 1441 },
                        new GiveSoftBlacklist { Bypass = "bypass.363", Id = 363 }
                    },
                    VehicleSpawnBlacklist = Configuration.Instance.LegacyVehicleSpawnBlacklist ?? new List<VehicleSpawnBlacklist>
                    {
                        new VehicleSpawnBlacklist { Bypass = "vehiclebypass.109", VehicleId = 109 },
                        new VehicleSpawnBlacklist { Bypass = "vehiclebypass.120", VehicleId = 120 }
                    }
                };
                needsSave = true;
            }

            Configuration.Instance.LegacyGiveItemBlacklist = null;
            Configuration.Instance.LegacyVehicleSpawnBlacklist = null;

            if (Blacklists.GiveItemBlacklist == null)
            {
                Blacklists.GiveItemBlacklist = new List<GiveSoftBlacklist>();
            }
            if (Blacklists.PickupItemBlacklist == null)
            {
                Blacklists.PickupItemBlacklist = new List<GiveSoftBlacklist>();
            }
            if (Blacklists.GiveAndPickupItemBlacklist == null)
            {
                Blacklists.GiveAndPickupItemBlacklist = new List<GiveSoftBlacklist>();
            }
            if (Blacklists.VehicleSpawnBlacklist == null)
            {
                Blacklists.VehicleSpawnBlacklist = new List<VehicleSpawnBlacklist>();
            }
            if (Blacklists.GiveItemBlacklist.Any(IsInvalidItemBlacklistEntry) ||
                Blacklists.PickupItemBlacklist.Any(IsInvalidItemBlacklistEntry) ||
                Blacklists.GiveAndPickupItemBlacklist.Any(IsInvalidItemBlacklistEntry) ||
                Blacklists.VehicleSpawnBlacklist.Any(vehicle => vehicle == null || string.IsNullOrWhiteSpace(vehicle.Bypass)))
            {
                Logger.LogError("Blacklists.json contains an invalid item entry or a missing bypass permission.");
                throw new InvalidDataException("Blacklists.json contains an invalid entry.");
            }

            if (needsSave)
            {
                try
                {
                    blacklistStorage.Save(Blacklists);
                }
                catch (System.Exception exception)
                {
                    Logger.LogError("Failed to update Blacklists.json: " + exception.Message);
                    throw;
                }
            }

            RebuildBlacklistIndexes();
        }

        private void ValidateWarnPunishments()
        {
            if (Configuration.Instance.WarnPunishments == null)
            {
                Configuration.Instance.WarnPunishments = CCRPConfig.CreateDefaultWarnPunishments();
            }

            HashSet<int> configuredWarnCounts = new HashSet<int>();
            foreach (WarnPunishment punishment in Configuration.Instance.WarnPunishments)
            {
                bool invalid = punishment == null || punishment.WarnCount <= 0 || string.IsNullOrWhiteSpace(punishment.Reason);
                if (!invalid && !configuredWarnCounts.Add(punishment.WarnCount))
                {
                    invalid = true;
                }

                if (!invalid && punishment.Action == WarnPunishmentAction.Kick)
                {
                    invalid = punishment.Permanent || punishment.DurationSeconds != 0;
                }
                else if (!invalid && punishment.Action == WarnPunishmentAction.Ban)
                {
                    invalid = punishment.Permanent
                        ? punishment.DurationSeconds != 0
                        : punishment.DurationSeconds <= 0 || (uint)punishment.DurationSeconds >= SteamBlacklist.PERMANENT;
                }
                else if (!invalid)
                {
                    invalid = true;
                }

                if (invalid)
                {
                    Logger.LogError("WarnPunishments contains an invalid or duplicate entry.");
                    throw new InvalidDataException("WarnPunishments contains an invalid or duplicate entry.");
                }
            }
        }

        private void RebuildBlacklistIndexes()
        {
            giveItemBypassPermissions.Clear();
            pickupItemBypassPermissions.Clear();
            giveItemTypeBypassPermissions.Clear();
            pickupItemTypeBypassPermissions.Clear();
            foreach (GiveSoftBlacklist item in Blacklists.GiveItemBlacklist)
            {
                AddBypassPermission(giveItemBypassPermissions, giveItemTypeBypassPermissions, item);
            }
            foreach (GiveSoftBlacklist item in Blacklists.PickupItemBlacklist)
            {
                AddBypassPermission(pickupItemBypassPermissions, pickupItemTypeBypassPermissions, item);
            }
            foreach (GiveSoftBlacklist item in Blacklists.GiveAndPickupItemBlacklist)
            {
                AddBypassPermission(giveItemBypassPermissions, giveItemTypeBypassPermissions, item);
                AddBypassPermission(pickupItemBypassPermissions, pickupItemTypeBypassPermissions, item);
            }
        }

        public bool TryAddBlacklistEntry(string category, ushort assetId, string bypassPermission, out string errorKey)
        {
            return TryAddBlacklistEntry(category, assetId.ToString(CultureInfo.InvariantCulture), bypassPermission, out errorKey);
        }

        public bool TryAddBlacklistEntry(string category, string selector, string bypassPermission, out string errorKey)
        {
            errorKey = null;
            if (string.IsNullOrWhiteSpace(bypassPermission))
            {
                errorKey = "BlacklistInvalidBypass";
                return false;
            }

            category = category == null ? string.Empty : category.ToLowerInvariant();
            if (category == "vehicle")
            {
                ushort vehicleId;
                if (!ushort.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out vehicleId) || vehicleId == 0)
                {
                    errorKey = "BlacklistInvalidId";
                    return false;
                }
                if (Assets.find(EAssetType.VEHICLE, vehicleId) == null)
                {
                    errorKey = "BlacklistAssetNotFound";
                    return false;
                }
                if (Blacklists.VehicleSpawnBlacklist.Any(vehicle => vehicle.VehicleId == vehicleId))
                {
                    errorKey = "BlacklistAlreadyExists";
                    return false;
                }

                VehicleSpawnBlacklist entry = new VehicleSpawnBlacklist { VehicleId = vehicleId, Bypass = bypassPermission };
                Blacklists.VehicleSpawnBlacklist.Add(entry);
                if (!SaveBlacklistChanges())
                {
                    Blacklists.VehicleSpawnBlacklist.Remove(entry);
                    errorKey = "BlacklistSaveFailed";
                    return false;
                }

                return true;
            }

            if (category != "give" && category != "pickup" && category != "both")
            {
                errorKey = "BlacklistInvalidCategory";
                return false;
            }

            ushort assetId;
            EItemType itemType = default(EItemType);
            bool isItemId = ushort.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out assetId);
            if (isItemId && assetId == 0)
            {
                errorKey = "BlacklistInvalidId";
                return false;
            }
            if (isItemId && Assets.find(EAssetType.ITEM, assetId) == null)
            {
                errorKey = "BlacklistAssetNotFound";
                return false;
            }
            if (!isItemId && IsNumericSelector(selector))
            {
                errorKey = "BlacklistInvalidId";
                return false;
            }
            if (!isItemId && !TryParseItemType(selector, out itemType))
            {
                errorKey = "BlacklistInvalidType";
                return false;
            }

            bool alreadyInGive = Blacklists.GiveItemBlacklist.Any(item => MatchesBlacklistSelector(item, isItemId, assetId, isItemId ? (EItemType?)null : itemType));
            bool alreadyInPickup = Blacklists.PickupItemBlacklist.Any(item => MatchesBlacklistSelector(item, isItemId, assetId, isItemId ? (EItemType?)null : itemType));
            bool alreadyInBoth = Blacklists.GiveAndPickupItemBlacklist.Any(item => MatchesBlacklistSelector(item, isItemId, assetId, isItemId ? (EItemType?)null : itemType));
            if ((category == "give" && (alreadyInGive || alreadyInBoth)) ||
                (category == "pickup" && (alreadyInPickup || alreadyInBoth)) ||
                (category == "both" && (alreadyInGive || alreadyInPickup || alreadyInBoth)))
            {
                errorKey = "BlacklistAlreadyExists";
                return false;
            }

            List<GiveSoftBlacklist> targetList = category == "give"
                ? Blacklists.GiveItemBlacklist
                : category == "pickup" ? Blacklists.PickupItemBlacklist : Blacklists.GiveAndPickupItemBlacklist;
            GiveSoftBlacklist itemEntry = isItemId
                ? new GiveSoftBlacklist { Id = assetId, Bypass = bypassPermission }
                : new GiveSoftBlacklist { ItemType = itemType.ToString(), Bypass = bypassPermission };
            targetList.Add(itemEntry);
            if (!SaveBlacklistChanges())
            {
                targetList.Remove(itemEntry);
                errorKey = "BlacklistSaveFailed";
                return false;
            }

            RebuildBlacklistIndexes();
            return true;
        }

        public bool TryRemoveBlacklistEntry(string category, ushort assetId, out string errorKey)
        {
            return TryRemoveBlacklistEntry(category, assetId.ToString(CultureInfo.InvariantCulture), out errorKey);
        }

        public bool TryRemoveBlacklistEntry(string category, string selector, out string errorKey)
        {
            errorKey = null;
            category = category == null ? string.Empty : category.ToLowerInvariant();

            if (category == "vehicle")
            {
                ushort vehicleId;
                if (!ushort.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out vehicleId) || vehicleId == 0)
                {
                    errorKey = "BlacklistInvalidId";
                    return false;
                }
                int index = Blacklists.VehicleSpawnBlacklist.FindIndex(vehicle => vehicle.VehicleId == vehicleId);
                if (index < 0)
                {
                    errorKey = "BlacklistEntryNotFound";
                    return false;
                }

                VehicleSpawnBlacklist entry = Blacklists.VehicleSpawnBlacklist[index];
                Blacklists.VehicleSpawnBlacklist.RemoveAt(index);
                if (!SaveBlacklistChanges())
                {
                    Blacklists.VehicleSpawnBlacklist.Insert(index, entry);
                    errorKey = "BlacklistSaveFailed";
                    return false;
                }

                return true;
            }

            List<GiveSoftBlacklist> targetList;
            if (category == "give")
            {
                targetList = Blacklists.GiveItemBlacklist;
            }
            else if (category == "pickup")
            {
                targetList = Blacklists.PickupItemBlacklist;
            }
            else if (category == "both")
            {
                targetList = Blacklists.GiveAndPickupItemBlacklist;
            }
            else
            {
                errorKey = "BlacklistInvalidCategory";
                return false;
            }

            ushort assetId;
            EItemType itemType = default(EItemType);
            bool isItemId = ushort.TryParse(selector, NumberStyles.None, CultureInfo.InvariantCulture, out assetId);
            if (isItemId && assetId == 0)
            {
                errorKey = "BlacklistInvalidId";
                return false;
            }
            if (!isItemId && IsNumericSelector(selector))
            {
                errorKey = "BlacklistInvalidId";
                return false;
            }
            if (!isItemId && !TryParseItemType(selector, out itemType))
            {
                errorKey = "BlacklistInvalidType";
                return false;
            }

            int itemIndex = targetList.FindIndex(item =>
                MatchesBlacklistSelector(item, isItemId, assetId, isItemId ? (EItemType?)null : itemType));
            if (itemIndex < 0)
            {
                errorKey = "BlacklistEntryNotFound";
                return false;
            }

            GiveSoftBlacklist itemEntry = targetList[itemIndex];
            targetList.RemoveAt(itemIndex);
            if (!SaveBlacklistChanges())
            {
                targetList.Insert(itemIndex, itemEntry);
                errorKey = "BlacklistSaveFailed";
                return false;
            }

            RebuildBlacklistIndexes();
            return true;
        }

        private bool SaveBlacklistChanges()
        {
            try
            {
                blacklistStorage.Save(Blacklists);
                return true;
            }
            catch (System.Exception exception)
            {
                Logger.LogError("Failed to save Blacklists.json: " + exception.Message);
                return false;
            }
        }

        private static void AddBypassPermission(
            Dictionary<ushort, List<string>> idIndex,
            Dictionary<EItemType, List<string>> typeIndex,
            GiveSoftBlacklist item)
        {
            if (item.IsItemTypeEntry)
            {
                EItemType itemType;
                if (!TryParseItemType(item.ItemType, out itemType))
                {
                    throw new InvalidDataException("Blacklists.json contains an unknown item type.");
                }

                AddBypassPermission(typeIndex, itemType, item.Bypass);
                return;
            }

            AddBypassPermission(idIndex, item.Id, item.Bypass);
        }

        private static void AddBypassPermission<TKey>(
            Dictionary<TKey, List<string>> index,
            TKey key,
            string permission)
        {
            List<string> permissions;
            if (!index.TryGetValue(key, out permissions))
            {
                permissions = new List<string>();
                index.Add(key, permissions);
            }

            permissions.Add(permission);
        }

        private static bool IsInvalidItemBlacklistEntry(GiveSoftBlacklist item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Bypass))
            {
                return true;
            }

            if (!item.IsItemTypeEntry)
            {
                return false;
            }

            EItemType itemType;
            return item.Id != 0 || !TryParseItemType(item.ItemType, out itemType);
        }

        private static bool TryParseItemType(string value, out EItemType itemType)
        {
            if (Enum.TryParse(value, true, out itemType) &&
                Enum.IsDefined(typeof(EItemType), itemType) &&
                !IsNumericSelector(value))
            {
                return true;
            }

            itemType = default(EItemType);
            return false;
        }

        private static bool IsNumericSelector(string value)
        {
            ulong numericValue;
            return ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out numericValue);
        }

        private static bool MatchesBlacklistSelector(
            GiveSoftBlacklist entry,
            bool isItemId,
            ushort itemId,
            EItemType? itemType)
        {
            if (entry == null)
            {
                return false;
            }

            if (isItemId)
            {
                return !entry.IsItemTypeEntry && entry.Id == itemId;
            }

            EItemType entryType;
            return entry.IsItemTypeEntry && TryParseItemType(entry.ItemType, out entryType) && entryType == itemType.Value;
        }

        private static string FormatItemBlacklistEntry(GiveSoftBlacklist item)
        {
            if (item.IsItemTypeEntry)
            {
                return $"Type: {item.ItemType} | Bypass: {item.Bypass ?? "NONE"}";
            }

            return $"ID: {item.Id} | Name: {Assets.find(EAssetType.ITEM, item.Id)?.name ?? "UNKNOWN"} | Bypass: {item.Bypass ?? "NONE"}";
        }

        public GiveSoftBlacklist FindApplicableItemBlacklistEntry(IEnumerable<GiveSoftBlacklist> entries, ushort itemId)
        {
            GiveSoftBlacklist exactEntry = entries.FirstOrDefault(item =>
                item != null && !item.IsItemTypeEntry && item.Id == itemId);
            if (exactEntry != null)
            {
                return exactEntry;
            }

            ItemAsset asset = Assets.find(EAssetType.ITEM, itemId) as ItemAsset;
            if (asset == null)
            {
                return null;
            }

            return entries.FirstOrDefault(item =>
            {
                EItemType itemType;
                return item != null && item.IsItemTypeEntry &&
                    TryParseItemType(item.ItemType, out itemType) && itemType == asset.type;
            });
        }

        public const string BypassAllPermission = "ccrp.bypass.all";

        // Explicit permission match only; HasPermission would let admins (and "*") bypass blacklists.
        public static bool HasBlacklistBypass(IRocketPlayer player, string bypassPermission)
        {
            return player.GetPermissions().Any(x => x != null &&
                (string.Equals(x.Name, bypassPermission, System.StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(x.Name, BypassAllPermission, System.StringComparison.OrdinalIgnoreCase)));
        }

        public bool IsItemBlockedFromGive(ushort itemId, UnturnedPlayer player)
        {
            return IsItemBlocked(itemId, player, giveItemBypassPermissions, giveItemTypeBypassPermissions);
        }

        private bool IsItemBlocked(
            ushort itemId,
            UnturnedPlayer player,
            Dictionary<ushort, List<string>> idIndex,
            Dictionary<EItemType, List<string>> typeIndex)
        {
            List<string> permissions;
            if (idIndex.TryGetValue(itemId, out permissions) &&
                IsBlockedByPermissions(permissions, player))
            {
                return true;
            }

            if (typeIndex.Count == 0)
            {
                return false;
            }

            ItemAsset asset = Assets.find(EAssetType.ITEM, itemId) as ItemAsset;
            return asset != null && typeIndex.TryGetValue(asset.type, out permissions) &&
                IsBlockedByPermissions(permissions, player);
        }

        private static bool IsBlockedByPermissions(List<string> permissions, UnturnedPlayer player)
        {
            return permissions.Any(permission => player == null || !HasBlacklistBypass(player, permission));
        }

        private void OnTakeItemRequested(Player player, byte x, byte y, uint instanceID, byte toX, byte toY, byte toRot, byte toPage, ItemData itemData, ref bool shouldAllow)
        {
            if (!shouldAllow || itemData == null || itemData.item == null)
            {
                return;
            }

            bool hasApplicableRule = pickupItemBypassPermissions.ContainsKey(itemData.item.id);
            if (!hasApplicableRule && pickupItemTypeBypassPermissions.Count > 0)
            {
                ItemAsset asset = Assets.find(EAssetType.ITEM, itemData.item.id) as ItemAsset;
                hasApplicableRule = asset != null && pickupItemTypeBypassPermissions.ContainsKey(asset.type);
            }
            if (!hasApplicableRule)
            {
                return;
            }

            UnturnedPlayer unturnedPlayer = UnturnedPlayer.FromPlayer(player);
            if (IsItemBlocked(itemData.item.id, unturnedPlayer, pickupItemBypassPermissions, pickupItemTypeBypassPermissions))
            {
                shouldAllow = false;
                if (unturnedPlayer != null)
                {
                    UnturnedChat.Say(unturnedPlayer, Translate("PickupItemNoBypass"), MessageColor);
                }
            }
        }

        private void OnPlayerDeath(UnturnedPlayer player, EDeathCause cause, ELimb limb, CSteamID murderer)
        {
            if (player == null)
            {
                return;
            }

            string message;
            if (murderer.m_SteamID != 0 && murderer.m_SteamID != player.CSteamID.m_SteamID)
            {
                UnturnedPlayer killer = UnturnedPlayer.FromCSteamID(murderer);
                string killerName = killer == null ? murderer.m_SteamID.ToString(CultureInfo.InvariantCulture) : killer.DisplayName;
                string killerDistance = killer == null
                    ? "an unknown distance"
                    : UnityEngine.Vector3.Distance(player.Position, killer.Position).ToString("0", CultureInfo.InvariantCulture) + "m";
                message = Translate("PlayerDeathByPlayer", player.DisplayName, killerName, killerDistance);
            }
            else
            {
                message = Translate("PlayerDeath", player.DisplayName, cause.ToString().ToLowerInvariant());
            }

            UnturnedChat.Say(message, MessageColor);
        }
        protected override void Unload()
        {
            if (PlaytimeService != null)
            {
                PlaytimeService.Flush();
            }

            Destroy(WarnService);
            Destroy(WarnLogService);
            Destroy(PlaytimeService);
            Destroy(VehicleCleanupService);
            Destroy(WreckService);
            CCRP_Plugin.Services.DeathLocationService.Shutdown(DeathLocationService);
            DeathLocationService = null;
            CCRPFreezeManager.Shutdown();
            TeleportService.Unload();
            ChatFeaturesService.Shutdown();

            UnturnedPlayerEvents.OnPlayerDeath -= OnPlayerDeath;
            UnturnedPlayerEvents.OnPlayerRevive -= OnPlayerRevive;
            U.Events.OnPlayerConnected -= OnPlayerConnected;
            U.Events.OnPlayerDisconnected -= OnPlayerDisconnected;
            ItemManager.onTakeItemRequested -= OnTakeItemRequested;
            VehicleManager.onEnterVehicleRequested -= OnEnterVehicleRequested;
            VehicleManager.onSwapSeatRequested -= OnSwapSeatRequested;
            BarricadeManager.onTransformRequested -= OnBarricadeTransformRequested;
            StructureManager.onTransformRequested -= OnStructureTransformRequested;

            Rocket.Core.Logging.Logger.Log($"{Name} has been unloaded!");
        }

        // The base game lets any workzone-enabled player move any buildable, so ownership is enforced here.
        private void OnBarricadeTransformRequested(CSteamID instigator, byte x, byte y, ushort plant, uint instanceID, ref UnityEngine.Vector3 point, ref byte angle_x, ref byte angle_y, ref byte angle_z, ref bool shouldAllow)
        {
            if (!shouldAllow)
            {
                return;
            }

            BarricadeRegion region;
            BarricadeDrop target = null;
            BarricadeData data = null;
            if (BarricadeManager.tryGetRegion(x, y, plant, out region) && region != null && region.drops != null)
            {
                foreach (BarricadeDrop drop in region.drops)
                {
                    if (drop.instanceID == instanceID)
                    {
                        target = drop;
                        data = drop.GetServersideData();
                        break;
                    }
                }
            }
            if (!CanEditBuildable(instigator, data != null, data != null ? data.owner : 0))
            {
                shouldAllow = false;
                if (target != null && target.model != null)
                {
                    RestoreTransform(target.model, ref point, ref angle_x, ref angle_y, ref angle_z);
                }
            }
        }

        private void OnStructureTransformRequested(CSteamID instigator, byte x, byte y, uint instanceID, ref UnityEngine.Vector3 point, ref byte angle_x, ref byte angle_y, ref byte angle_z, ref bool shouldAllow)
        {
            if (!shouldAllow)
            {
                return;
            }

            StructureDrop target = null;
            StructureData data = null;
            foreach (StructureRegion region in StructureManager.regions)
            {
                if (region == null || region.drops == null)
                {
                    continue;
                }

                foreach (StructureDrop drop in region.drops)
                {
                    if (drop.instanceID != instanceID)
                    {
                        continue;
                    }

                    target = drop;
                    data = drop.GetServersideData();
                    break;
                }

                if (data != null)
                {
                    break;
                }
            }
            if (!CanEditBuildable(instigator, data != null, data != null ? data.owner : 0))
            {
                shouldAllow = false;
                if (target != null && target.model != null)
                {
                    RestoreTransform(target.model, ref point, ref angle_x, ref angle_y, ref angle_z);
                }
            }
        }

        private static void RestoreTransform(UnityEngine.Transform target, ref UnityEngine.Vector3 point, ref byte angle_x, ref byte angle_y, ref byte angle_z)
        {
            point = target.position;
            UnityEngine.Vector3 eulerAngles = target.rotation.eulerAngles;
            angle_x = MeasurementTool.angleToByte(UnityEngine.Mathf.RoundToInt(eulerAngles.x / 2f) * 2);
            angle_y = MeasurementTool.angleToByte(UnityEngine.Mathf.RoundToInt(eulerAngles.y / 2f) * 2);
            angle_z = MeasurementTool.angleToByte(UnityEngine.Mathf.RoundToInt(eulerAngles.z / 2f) * 2);
        }

        // Fails closed when the buildable cannot be resolved. admin.editor.otherobjects is the explicit override.
        private bool CanEditBuildable(CSteamID instigator, bool found, ulong owner)
        {
            if (found && owner == instigator.m_SteamID)
            {
                return true;
            }

            UnturnedPlayer editor = UnturnedPlayer.FromCSteamID(instigator);
            if (editor != null && HasExplicitPermission(editor, "admin.editor.otherobjects"))
            {
                return true;
            }

            if (editor != null)
            {
                UnturnedChat.Say(editor, Translate("EditorOtherObjectsDenied"), MessageColor);
            }
            return false;
        }

        private static bool HasExplicitPermission(UnturnedPlayer player, string permission)
        {
            List<Permission> permissions = R.Permissions.GetPermissions(player);
            return permissions != null && permissions.Any(grant =>
                grant != null && string.Equals(grant.Name, permission, StringComparison.OrdinalIgnoreCase));
        }

        // Entering seats are assigned in order, so a free driver seat means this player would become the driver.
        private void OnEnterVehicleRequested(Player player, InteractableVehicle vehicle, ref bool shouldAllow)
        {
            if (!shouldAllow || vehicle == null || vehicle.passengers == null || vehicle.passengers.Length == 0)
            {
                return;
            }

            if (vehicle.passengers[0].player == null && !CanDriveVehicle(player, vehicle))
            {
                shouldAllow = false;
            }
        }

        private void OnSwapSeatRequested(Player player, InteractableVehicle vehicle, ref bool shouldAllow, byte fromSeatIndex, ref byte toSeatIndex)
        {
            if (shouldAllow && toSeatIndex == 0 && !CanDriveVehicle(player, vehicle))
            {
                shouldAllow = false;
            }
        }

        private bool CanDriveVehicle(Player player, InteractableVehicle vehicle)
        {
            if (player == null || vehicle == null || vehicle.asset == null || Blacklists == null || Blacklists.VehicleSpawnBlacklist == null)
            {
                return true;
            }

            ushort vehicleId = vehicle.asset.id;
            VehicleSpawnBlacklist entry = Blacklists.VehicleSpawnBlacklist.FirstOrDefault(x => x != null && x.VehicleId == vehicleId);
            if (entry == null)
            {
                return true;
            }

            UnturnedPlayer unturnedPlayer = UnturnedPlayer.FromPlayer(player);
            if (HasBlacklistBypass(unturnedPlayer, entry.Bypass))
            {
                return true;
            }

            UnturnedChat.Say(unturnedPlayer, Translate("VehicleDriveNoBypass"), MessageColor);
            return false;
        }

        
        private void OnPlayerConnected(UnturnedPlayer player)
        {
            UnturnedChat.Say(Translate("PlayerJoined", player.DisplayName), MessageColor);
            Logger.Log($"{player.CharacterName} ({player.Id}) joined the Server!");

            PlaytimeService.CreatePlayerForPlaytime(player.Id);
            PlaytimeService.PlaytimeJoin(player.Id);
            MaxSkillsService.MaxSkills(player);
        }

        private void OnPlayerRevive(UnturnedPlayer player, UnityEngine.Vector3 position, byte angle)
        {
            MaxSkillsService.MaxSkills(player);
        }
        void OnPlayerDisconnected(UnturnedPlayer player)
        {
            CCRPPlugin.Commands.CCRPInvCommand.ReleaseViews(player);
            PlaytimeService.EndSession(player.Id);

            foreach (DutyGroups Group in Main.Instance.ValidGroups)
            {
                RocketPermissionsGroup Target = R.Permissions.GetGroup(Group.GroupID);

                if (Target.Members.Contains(player.CSteamID.ToString()))
                {
                    player.Features.GodMode = false;
                    player.Features.VanishMode = false;
                    player.Admin(false);
                    UnturnedChat.Say(Translate("PlayerLeftOffDuty", player.DisplayName, Target.Prefix), MessageColor);
                    Logger.Log($"{player.CharacterName} ({player.Id}) left the Server and is off duty {Target.Prefix}!");
                    return;
                }
            }
            UnturnedChat.Say(Translate("PlayerLeft", player.DisplayName), MessageColor);
            Logger.Log($"{player.CharacterName} ({player.Id}) left the Server!");
        }
        
        public override TranslationList DefaultTranslations => new TranslationList()
        {
            { "VehicleCleanupEmptyWarning", "All empty vehicles will be cleared in {0} seconds!" },
            { "VehicleCleanupEmptyDone", "Cleared {0} empty vehicles." },
            { "VehicleCleanupAllWarning", "All unused vehicles will be cleared in {0} seconds!" },
            { "VehicleCleanupAllDone", "Cleared {0} unused vehicles." },
            { "WreckSyntax", "Use /wreck <filter> <radius> [username|Steam64], /wreck scan <filter> <radius> [username|Steam64], /wreck confirm <reason>, or /wreck abort." },
            { "WreckItemSyntax", "Use /wreckitem <id[,id...]> <radius> [username|Steam64], /wreckitem scan <id[,id...]> <radius> [username|Steam64], /wreckitem confirm <reason>, or /wreckitem abort." },
            { "WreckInvalidFilter", "Use b, t, d, c, l, w, f, p, r, s, g, i, a, v, or * as the wreck filter." },
            { "WreckInvalidRadius", "Enter a radius between 1 and {0} meters." },
            { "WreckInvalidUser", "The specified player name or Steam64 ID could not be resolved." },
            { "WreckInvalidItem", "Enter one or more positive item IDs separated by commas." },
            { "WreckNone", "No matching wreck targets were found." },
            { "WreckScan", "Found {0} matching targets within {1} meters." },
            { "WreckTooMany", "More than {0} targets matched. Narrow the filters before continuing." },
            { "WreckAlreadyProcessing", "Your wreck request is already processing. Use /wreck abort to stop it." },
            { "WreckQueued", "Queued {0} targets within {1} meters. Use /wreck confirm <reason> to destroy them or /wreck abort to cancel." },
            { "WreckRequestReplaced", "Your previous wreck request was replaced." },
            { "WhoisSyntax", "Use /whois while looking at a structure or barricade." },
            { "WhoisResult", "{0} ({1}) owner: {2} - Steam64: {3}. Use \"last\" as the owner in /wreck to target them." },
            { "WreckBuildableSyntax", "Use /wb while looking at the structure or barricade you want to destroy." },
            { "WreckBuildableNotFound", "No structure or barricade found within {0} meters of where you are looking." },
            { "WreckBuildableDestroyed", "Destroyed {0} ({1})." },
            { "WreckVehicleSyntax", "Use /wv while looking at the vehicle you want to destroy." },
            { "WreckVehicleNotFound", "No vehicle found within {0} meters of where you are looking." },
            { "WreckVehicleDestroyed", "Destroyed vehicle {0} ({1})." },
            { "ItemFeaturesSyntax", "Use /if <autoreload|autorepair|all> <on|off>." },
            { "ItemFeaturesNoItem", "Hold an item or occupy a vehicle weapon seat first." },
            { "ItemFeaturesEnabled", "{0} enabled for this item." },
            { "ItemFeaturesDisabled", "{0} disabled for this item." },
            { "VehicleFeaturesSyntax", "Use /vf <autorefuel|autorepair|all> <on|off>." },
            { "VehicleFeaturesNoVehicle", "You must be in a vehicle first." },
            { "VehicleFeaturesEnabled", "{0} enabled for this vehicle." },
            { "VehicleFeaturesDisabled", "{0} disabled for this vehicle." },
            { "WreckReasonRequired", "A reason is required. Use /{0} confirm <reason>." },
            { "WreckConfirm", "Destroying {0} queued targets. Use /wreck abort to stop further destruction." },
            { "WreckNoRequest", "You do not have a pending or active wreck request." },
            { "WreckAborted", "Wrecking stopped after destroying {0} targets." },
            { "WreckCompleted", "Wrecking complete: destroyed {0}, skipped {1}, failed {2}." },
            { "NoPerms", "You don't have enough permissions to use this command!" },
            { "PlayerNotFound", "Player not found!" },
            { "PlayerJoined", "{0} joined the Server!" },
            { "PlayerLeft", "{0} left the Server!" },
            { "PlayerLeftOffDuty", "{0} left the Server and is off duty {1}!" },
            { "PlayerDeath", "{0} died ({1})." },
            { "PlayerDeathByPlayer", "{0} was killed by {1} from {2} away." },
            { "GiveSuccessWithoutAmount", "You received the item 1x {0} ({1})!"},
            { "GiveSuccessWithAmount", "You received the item {0}x {1} ({2})!" },
            { "GiveItemNotFound", "The item has not been found!" },
            { "GiveAddValidNumber", "Please add a valid number as the amount!" },
            { "GiveAmountLimit", "You can't give yourself that many items!" },
            { "GiveNoBypass", "You don't have the bypass for this item!" },
            { "PickupItemNoBypass", "You don't have permission to pick up this item!" },
            { "BypassGroupSyntax", "Use /bypassgroup create <display name> <RRGGBB>, /bypassgroup name <display name>, or /bypassgroup color <RRGGBB>. Color may include an optional leading #." },
            { "BypassItemSyntax", "Use /bypassitem <item ID>, /bypassitem confirm, or /bypassitem cancel." },
            { "BypassVehicleSyntax", "Use /bypassvehicle <vehicle ID>, /bypassvehicle confirm, or /bypassvehicle cancel." },
            { "BypassInvalidName", "Choose a display name between 1 and 24 characters without angle brackets." },
            { "BypassInvalidColor", "Enter a color as six hexadecimal digits, with or without a leading #, such as 33AAFF or #33AAFF." },
            { "BypassGroupAlreadyCreated", "You have already created your one personal bypass group." },
            { "BypassNoDonationTier", "You need a configured donation group to create or customize a bypass group or claim a free bypass." },
            { "BypassGroupSaveFailed", "Your bypass group could not be saved. Please contact an administrator." },
            { "BypassGroupCreated", "Your personal bypass group has been created." },
            { "BypassGroupRequired", "Create your personal bypass group first with /bypassgroup create <display name> <RRGGBB> (the leading # is optional)." },
            { "BypassPersonalGroupNotFound", "This player does not have a registered personal bypass group." },
            { "BypassGroupMissing", "Your recorded personal bypass group is missing. Please contact an administrator." },
            { "BypassGroupOwnershipInvalid", "Your personal bypass group membership could not be verified. Please contact an administrator." },
            { "BypassGroupNameUpdated", "Your group display name and prefix have been updated." },
            { "BypassGroupColorUpdated", "Your group color has been updated." },
            { "BypassShopSyntax", "Use /bypassshop to list offers or /bypassbuy <item|vehicle> <ID> to buy one." },
            { "BypassCostSyntax", "Use /bypasscost <ID>, or /bypasscost <item|vehicle> <ID> if the ID is offered for both types." },
            { "BypassShopPlayerOnly", "The bypass shop is only available to players." },
            { "BypassShopHeader", "Available bypass shop offers (purchase with /bypassbuy <item|vehicle> <ID>):" },
            { "BypassShopEmpty", "There are no bypass offers available right now." },
            { "BypassShopOfferUnavailable", "That ID is not currently available in the bypass shop." },
            { "BypassShopOfferAmbiguous", "That ID matches conflicting blacklist bypass permissions and cannot be sold." },
            { "BypassShopOfferInvalid", "That bypass shop offer is misconfigured." },
            { "BypassShopOfferPermissionInvalid", "That blacklist entry has no valid bypass permission and cannot be sold." },
            { "BypassShopPermissionDenied", "That bypass permission is denied by the donation bypass security list and cannot be sold." },
            { "BypassShopAlreadyOwned", "You already have the bypass permission. You were not charged." },
            { "BypassShopAlreadyPurchased", "You already own this bypass. No additional charge was taken." },
            { "BypassShopInsufficientFunds", "You do not have enough in-game money to buy that bypass." },
            { "BypassShopEconomyUnavailable", "No supported economy provider is available. Install AdvancedEconomy with UconomyToAdvancedEconomy, or install legacy Uconomy." },
            { "BypassShopEconomyFailure", "The economy transaction could not be verified. Please try again or contact staff." },
            { "BypassShopRefundFailed", "The purchase failed and the refund could not be confirmed. Contact staff before trying again." },
            { "BypassShopPurchaseRecordFailed", "The purchase record could not be saved. Your permission may remain applied and you may have been charged; contact staff before retrying." },
            { "BypassShopPurchased", "Purchased {0} bypass {1} for {2} in-game money." },
            { "BypassPurchasedLocked", "You already own this purchased bypass. It cannot be removed through free-claim commands." },
            { "BypassShopPaidLimitReached", "You have reached the maximum number of paid bypasses allowed." },
            { "BypassShopCost", "The {0} bypass for ID {1} costs {2} in-game money." },
            { "BypassShopCostAmbiguous", "That ID is offered for both an item and vehicle. Use /bypasscost <item|vehicle> <ID>." },
            { "BypassItemNotFound", "No item with that ID exists." },
            { "BypassVehicleNotFound", "No vehicle with that ID exists." },
            { "BypassItemNotEligible", "That item is not eligible for a donation bypass." },
            { "BypassVehicleNotEligible", "That vehicle is not eligible for a donation bypass." },
            { "BypassNotEligible", "That bypass permission is not eligible for a donation bypass." },
            { "BypassAlreadyOwned", "You already have that bypass permission." },
            { "BypassLimitReached", "You have reached your donation tier's bypass limit." },
            { "BypassClaimed", "Bypass for {0} ({1}) has been added to your group." },
            { "BypassItemRemoveConfirmation", "You already have the item bypass for {0} in your personal group. Use /bypassitem confirm within 30 seconds to remove it, or /bypassitem cancel to keep it." },
            { "BypassVehicleRemoveConfirmation", "You already have the vehicle bypass for {0} in your personal group. Use /bypassvehicle confirm within 30 seconds to remove it, or /bypassvehicle cancel to keep it." },
            { "BypassRemovalNoRequest", "There is no active bypass removal confirmation." },
            { "BypassRemovalCancelled", "Bypass removal cancelled." },
            { "BypassRemovalTargetChanged", "That bypass changed while confirmation was pending. Try the command again." },
            { "BypassRemoved", "The {0} bypass for {1} has been removed from your personal group. Another permission group may still grant it." },
            { "BypassesPlayerOnly", "This command can only be used by a player." },
            { "BypassesEmpty", "You have no claimed item or vehicle bypasses in your personal group." },
            { "BypassesList", "Your claimed bypasses: {0}" },
            { "BypassAdminSyntax", "Use /bypassadmin list <Steam64ID>, add <Steam64ID> <item|vehicle> <ID>, remove <Steam64ID> <item|vehicle> <ID>, or delete <Steam64ID> confirm." },
            { "BypassAdminInvalidPlayer", "Enter a valid positive Steam64 ID." },
            { "BypassAdminInvalidAssetId", "Enter a valid positive item or vehicle ID." },
            { "BypassAdminAssetNotFound", "That ID has no configured item or vehicle bypass permission." },
            { "BypassAdminPermissionUnknown", "That bypass permission is not configured in the blacklists." },
            { "BypassAdminAlreadyAdded", "That bypass is already in the player's personal group." },
            { "BypassAdminNotAdded", "That bypass is not in the player's personal group." },
            { "BypassAdminListHeader", "Bypasses in {0}'s personal group:" },
            { "BypassAdminListEmpty", "Player {0} has no claimed bypasses in their personal group." },
            { "BypassAdminAdded", "Added {0} bypass {1} to player {2}'s personal group ({3})." },
            { "BypassAdminRemoved", "Removed {0} bypass {1} from player {2}'s personal group ({3})." },
            { "BypassAdminGroupDeleted", "Deleted player {0}'s personal bypass group. They can create a new one if they qualify." },
            { "BlacklistCommandSyntax", "Use /blacklist add <give|pickup|both|vehicle> <id|item-type> <bypass-permission> or /blacklist remove <give|pickup|both|vehicle> <id|item-type>. Vehicles require an ID." },
            { "BlacklistInvalidCategory", "Choose give, pickup, both, or vehicle." },
            { "BlacklistInvalidId", "Enter a valid positive numeric asset ID." },
            { "BlacklistInvalidType", "Enter a valid Unturned item type name, such as Farm or Grower. Vehicles require an ID." },
            { "BlacklistInvalidBypass", "A bypass permission is required." },
            { "BlacklistAssetNotFound", "No asset with that ID was found for the selected blacklist." },
            { "BlacklistAlreadyExists", "That selector is already in an overlapping blacklist." },
            { "BlacklistEntryNotFound", "That item ID or type is not in the selected blacklist." },
            { "BlacklistSaveFailed", "Could not save Blacklists.json; no change was applied." },
            { "BlacklistEntryAdded", "Added selector {1} to the {0} blacklist with bypass permission {2}." },
            { "BlacklistEntryRemoved", "Removed selector {1} from the {0} blacklist." },
            { "GiveSyntax", "Please use /give [id] [amount]!" },
            { "VehicleSuccess", "You received the vehicle {0} ({1})!" },
            { "VehicleNotFound", "The vehicle has not been found!" },
            { "VehicleNoBypass", "You don't have the bypass to spawn that vehicle!" },
            { "VehicleDriveNoBypass", "You don't have the bypass to drive that vehicle!" },
            { "InvSyntax", "Use /inv <name|Steam64>." },
            { "InvPlayerNotFound", "That player is not online." },
            { "InvSelf", "You cannot open your own inventory with /inv." },
            { "InvOpened", "Opened a view-only snapshot of {0}'s inventory." },
            { "InvSnapshotOnly", "/inv is a view-only snapshot; that item was removed from you." },
            { "VehicleSyntax", "Please use /vehicle [id]!" },
            { "UnbanSuccess", "You have unbanned {0} ({1})!" },
            { "UnbanNoId", "You didn't add a Steam64ID when trying to unban someone!" },
            { "UnbanSyntax", "Use /unban [Steam64ID] [Reason]!" },
            { "BanWithTime", "You have successfully banned {0} ({1}) for {2} ({3})!" },
            { "BanPerm", "You have permanently banned {0} ({1})!" },
            { "BanSyntax", "Please use /ban <name> [reason] [duration]!" },
            { "GodDisableSelf", "You have disabled Godmode for yourself!" },
            { "GodEnableSelf", "You have enabled Godmode for yourself!" },
            { "GodDisableOther", "You have disabled Godmode for {0} ({1})!" },
            { "GodEnabledOther", "You have enabled Godmode for {0} ({1})!" },
            { "GodSyntax", "Please use /god <name>!" },
            { "VanishDisableSelf", "You have disabled Vanish for yourself!" },
            { "VanishEnableSelf", "You have enabled Vanish for yourself!" },
            { "VanishDisableOther", "You have disabled Vanish for {0} ({1})!" },
            { "VanishEnabledOther", "You have enabled Vanish for {0} ({1})!" },
            { "VanishSyntax", "Please use /vanish <name>!" },
            { "KickSuccess", "You have kicked {0} ({1}) [{2}]!" },
            { "KickSyntax", "Please use /kick <name> [reason]!" },
            { "WarnsSuccess", "You have {0} warnings. Latest warning reason: {1}" },
            { "WarnsSuccessOther", "{0} ({1}) has {2} warnings. Latest warning reason: {3}" },
            { "NoActiveWarnReason", "no active warning reason" },
            { "WarnsSyntax", "Please use /warns <name>!" },
            { "WarnSuccess", "You have warned {0} ({1}) [{2}]!" },
            { "WarnSuccessOther", "You have been warned for {0} from {1}!" },
            { "WarnPlayerMessage", "You have received a warn!" },
            { "WarnSyntax", "Please use /warn <name> [reason]!" },
            { "RemoveWarnSuccess", "You have removed a warn from {0} ({1}) [{2}]!" },
            { "RemoveWarnNoWarns", "That player doesn't have any warns!" },
            { "RemoveWarnSyntax", "Please use /removewarn <name>!" },
            { "FreecamEnabled", "You have enabled freecam!" },
            { "FreecamDisabled", "You have disabled freecam!" },
            { "FreecamSyntax", "Please use /freecam without arguments." },
            { "FiremodeSyntax", "Please use /firemode <safety|semi|auto|burst>." },
            { "FiremodeNoGun", "You must be holding a gun to change its firemode." },
            { "FiremodeUnavailable", "The firemode could not be changed right now. Try again when the gun is ready." },
            { "FiremodeSet", "Your held gun is now set to {0} firemode." },
            { "EditorEnabled", "You have enabled the object editor!" },
            { "EditorDisabled", "You have disabled the object editor!" },
            { "EditorSyntax", "Please use /editor without arguments." },
            { "EditorOtherObjectsDenied", "You can only edit your own builds." },
            { "SpectateEnabled", "You have enabled player name and map visibility!" },
            { "SpectateDisabled", "You have disabled player name and map visibility!" },
            { "SpectateSyntax", "Please use /spectate without arguments." },
            { "TeleportToPlayerSuccess", "You have teleported yourself to {0}!" },
            { "TelepoortToCitySuccess", "You have teleported yourself to {0}!" },
            { "TeleportCoordinatesSuccess", "You teleported yourself to {0} {1} {2}!" },
            { "TeleportFailedToFindDest", "Failed to find a destination!" },
            { "TeleportSyntax", "Please use /teleport <name or coordinates>!" },
            { "TphereSuccess", "You teleported {0} to yourself!" },
            { "TohereOtherPlayer", "You got teleported to {0}!" },
            { "TphereSyntax", "Please use /tphere <name>!" },
            { "HomeSyntax", "Please use /home <name>!" },
            { "SetHomeSyntax", "Please use /sethome <name>!" },
            { "DelHomeSyntax", "Please use /delhome <name>!" },
            { "HomeNotFound", "You don't have a home named {0}!" },
            { "HomeTeleported", "You teleported to home {0}!" },
            { "HomeTeleporting", "Teleporting to home {0} in {1} second(s). Don't move!" },
            { "HomeCancelled", "Home teleport cancelled because you moved!" },
            { "HomeSet", "Home {0} has been set!" },
            { "HomeUpdated", "Home {0} has been moved to your current position!" },
            { "HomeLimitReached", "You can only have {0} home(s). Delete one with /delhome <name> first!" },
            { "HomeNoPermission", "You don't have permission to set homes!" },
            { "HomeDeleted", "Home {0} has been deleted!" },
            { "HomeList", "Your homes ({0}/{1}): {2}" },
            { "HomeListEmpty", "You have no homes. Use /sethome <name> to set one!" },
            { "HomeNameInvalid", "Home names must be 1-32 characters long!" },
            { "PlaytimeSuccess", "You have played {0} on the Server!" },
            { "PlaytimeOtherSuccess", "{0} has {1} playtime!" },
            { "PlaytimeSyntax", "Please use /playtime <name>!" },
            { "DutyOn", "{0} is now on Duty {1}!"},
            { "DutyOff", "{0} is now off Duty {1}!"}
        };
    }
}
