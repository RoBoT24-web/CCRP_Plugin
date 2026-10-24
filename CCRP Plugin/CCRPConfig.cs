using Rocket.API;
using CCRP_Plugin.Models;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;

namespace CCRPPlugin
{
    public class CCRPConfig : IRocketPluginConfiguration
    {
        [XmlElement(Order = 0)]
        public WarningSettings Warnings { get; set; }

        [XmlArray("DutyGroups", Order = 1)]
        [XmlArrayItem("Group")]
        public List<DutyGroups> Groups;

        [XmlElement(Order = 2)]
        public GeneralSettings General { get; set; }

        [XmlElement(Order = 3)]
        public WebhookSettings Webhooks { get; set; }

        [XmlElement(Order = 4)]
        public VehicleCleanupSettings VehicleCleanup { get; set; }

        [XmlElement(Order = 5)]
        public DonationBypassSettings DonationBypasses { get; set; }

        [XmlElement(Order = 6)]
        public MaxSkillsSettings MaxSkills { get; set; }

        [XmlElement(Order = 7)]
        public PrivateMessageSettings PrivateMessages { get; set; }

        [XmlElement(Order = 8)]
        public VoteSettings Votes { get; set; }

        [XmlElement(Order = 9)]
        public AutoAnnouncerSettings AutoAnnouncer { get; set; }

        [XmlAnyElement(Order = 10)]
        public XmlElement[] LegacyElements { get; set; }

        [XmlIgnore]
        public List<CCRP_Plugin.Models.GiveSoftBlacklist> LegacyGiveItemBlacklist { get; set; }

        [XmlIgnore]
        public List<CCRP_Plugin.Models.VehicleSpawnBlacklist> LegacyVehicleSpawnBlacklist { get; set; }

        [XmlIgnore]
        public List<DutyGroups> LegacyGroups { get; set; }

        [XmlIgnore]
        public List<WarnPunishment> WarnPunishments
        {
            get { return Warnings == null ? null : Warnings.Punishments; }
            set
            {
                if (Warnings == null)
                {
                    Warnings = new WarningSettings();
                }
                Warnings.Punishments = value;
            }
        }

        [XmlIgnore]
        public string MessageColor
        {
            get { return General == null ? null : General.MessageColor; }
            set
            {
                if (General == null)
                {
                    General = new GeneralSettings();
                }
                General.MessageColor = value;
            }
        }

        public void LoadDefaults()
        {
            Warnings = new WarningSettings { Punishments = CreateDefaultWarnPunishments() };
            General = new GeneralSettings();

            Webhooks = new WebhookSettings();
            VehicleCleanup = new VehicleCleanupSettings();
            DonationBypasses = new DonationBypassSettings { DeniedPermissions = new List<string> { "bypass.invis" } };
            MaxSkills = new MaxSkillsSettings();
            PrivateMessages = new PrivateMessageSettings();
            Votes = new VoteSettings { Definitions = VoteSettings.CreateDefaults() };
            AutoAnnouncer = new AutoAnnouncerSettings();

            Groups = new List<DutyGroups>()
            {
                new DutyGroups("trialstaff", "duty.trialstaff"),
                new DutyGroups("staff", "duty.staff"),
                new DutyGroups("seniorstaff", "duty.seniorstaff"),
                new DutyGroups("permdeveloper", "duty.permdeveloper"),
                new DutyGroups("developer", "duty.developer"),
                new DutyGroups("headdeveloper", "duty.headdeveloper"),
                new DutyGroups("headadmin", "duty.headadmin"),
                new DutyGroups("hos", "duty.hos"),
                new DutyGroups("manager", "duty.manager"),
                new DutyGroups("coowner", "duty.coowner"),
                new DutyGroups("owner", "duty.owner"),
            };
        }

        public static List<WarnPunishment> CreateDefaultWarnPunishments()
        {
            return new List<WarnPunishment>
            {
                new WarnPunishment { WarnCount = 3, Action = WarnPunishmentAction.Kick, Reason = "You have reached 3 warnings!" },
                new WarnPunishment { WarnCount = 4, Action = WarnPunishmentAction.Ban, DurationSeconds = 600, Reason = "You have reached 4 warnings!" },
                new WarnPunishment { WarnCount = 5, Action = WarnPunishmentAction.Ban, DurationSeconds = 3600, Reason = "You have reached 5 warnings!" },
                new WarnPunishment { WarnCount = 6, Action = WarnPunishmentAction.Ban, DurationSeconds = 7200, Reason = "You have reached 6 warnings!" },
                new WarnPunishment { WarnCount = 7, Action = WarnPunishmentAction.Ban, DurationSeconds = 86400, Reason = "You have reached 7 warnings!" },
                new WarnPunishment { WarnCount = 8, Action = WarnPunishmentAction.Ban, DurationSeconds = 604800, Reason = "You have reached 8 warnings!" },
                new WarnPunishment { WarnCount = 9, Action = WarnPunishmentAction.Ban, Permanent = true, Reason = "You have reached 9 warnings!" }
            };
        }
        
    }

    public class DonationBypassSettings
    {
        [XmlArray(Order = 0)]
        [XmlArrayItem("Tier")]
        public List<DonationBypassTier> Tiers { get; set; }

        [XmlArray(Order = 1)]
        [XmlArrayItem("Permission")]
        public List<string> DeniedPermissions { get; set; }

        [XmlElement(Order = 2)]
        public int MaxPaidBypasses { get; set; }

        public DonationBypassSettings()
        {
            Tiers = new List<DonationBypassTier>();
            // Must stay empty: XmlSerializer appends loaded items to a pre-filled list, duplicating defaults on every load.
            DeniedPermissions = new List<string>();
            MaxPaidBypasses = 5;
        }
    }

    public class DonationBypassTier
    {
        [XmlAttribute]
        public string GroupId { get; set; }

        [XmlAttribute]
        public int MaxBypasses { get; set; }

        public DonationBypassTier()
        {
            GroupId = string.Empty;
            MaxBypasses = 1;
        }

        public DonationBypassTier(string groupId, int maxBypasses)
        {
            GroupId = groupId;
            MaxBypasses = maxBypasses;
        }
    }

    public class DutyGroups
    {
        public string GroupID;
        public string Permission;
        public DutyGroups() { GroupID = "Administrator"; Permission = "admin"; }
        public DutyGroups(string ID, string Perm)
        {
            GroupID = ID;
            Permission = Perm;
        }
    }

    public class WebhookSettings
    {
        [XmlElement(Order = 0)]
        public string Kick { get; set; }

        [XmlElement(Order = 1)]
        public string Ban { get; set; }

        [XmlElement(Order = 2)]
        public string Warn { get; set; }

        [XmlElement(Order = 3)]
        public string BuilderMode { get; set; }

        [XmlElement(Order = 4)]
        public string Teleport { get; set; }

        [XmlElement(Order = 5)]
        public string Wreck { get; set; }

        [XmlElement(Order = 6)]
        public string Bypass { get; set; }

        public WebhookSettings()
        {
            Kick = "Discord Kick Webhook URL";
            Ban = "Discord Ban Webhook URL";
            Warn = "Discord Warn Webhook URL";
            BuilderMode = "Discord Buildermode Webhook URL";
            Teleport = "Teleport Webhook URL";
            Wreck = "Discord Wreck Webhook URL";
            Bypass = "Discord Bypass Webhook URL";
        }
    }

    public class MaxSkillsSettings
    {
        [XmlAttribute]
        public bool Enabled { get; set; }

        // Skill names to leave untouched, e.g. OVERKILL, SHARPSHOOTER, DEXTERITY, CARDIO, EXERCISE, DIVING, PARKOUR,
        // SNEAKYBEAKY, VITALITY, IMMUNITY, TOUGHNESS, STRENGTH, WARMBLOODED, SURVIVAL,
        // HEALING, CRAFTING, OUTDOORS, COOKING, FISHING, AGRICULTURE, MECHANIC, ENGINEER.
        [XmlArray(Order = 0)]
        [XmlArrayItem("Skill")]
        public List<string> IgnoredSkills { get; set; }

        public MaxSkillsSettings()
        {
            Enabled = true;
            IgnoredSkills = new List<string>();
        }
    }

    public class VehicleCleanupSettings
    {
        public const double MinimumIntervalMinutes = 1;

        [XmlElement(Order = 0)]
        public VehicleCleanupTask EmptyVehicles { get; set; }

        [XmlElement(Order = 1)]
        public VehicleCleanupTask AllVehicles { get; set; }

        public VehicleCleanupSettings()
        {
            EmptyVehicles = new VehicleCleanupTask();
            AllVehicles = new VehicleCleanupTask { IntervalMinutes = 120 };
        }
    }

    public class VehicleCleanupTask
    {
        [XmlAttribute]
        public bool Enabled { get; set; }

        // Minutes between wipes. Warnings are sent 30s and 10s before each wipe.
        [XmlAttribute]
        public double IntervalMinutes { get; set; }

        // AllVehicles only: a vehicle must be unmoved and unoccupied this long to be wiped.
        [XmlAttribute]
        public double MinIdleMinutes { get; set; }

        public VehicleCleanupTask()
        {
            MinIdleMinutes = 30;
            Enabled = true;
            IntervalMinutes = 30;
        }
    }

    public class WarningSettings
    {
        [XmlArray(Order = 0)]
        [XmlArrayItem("Punishment")]
        public List<WarnPunishment> Punishments { get; set; }
    }

    public class GeneralSettings
    {
        [XmlElement(Order = 0)]
        public string MessageColor { get; set; }

        public GeneralSettings()
        {
            MessageColor = "cyan";
        }
    }

    public enum WarnPunishmentAction
    {
        [XmlEnum("Kick")]
        Kick,
        [XmlEnum("Ban")]
        Ban
    }

    public class WarnPunishment
    {
        [XmlAttribute]
        public int WarnCount { get; set; }

        [XmlAttribute]
        public WarnPunishmentAction Action { get; set; }

        [XmlAttribute]
        public int DurationSeconds { get; set; }

        [XmlAttribute]
        public bool Permanent { get; set; }

        public string Reason { get; set; }
    }
}
