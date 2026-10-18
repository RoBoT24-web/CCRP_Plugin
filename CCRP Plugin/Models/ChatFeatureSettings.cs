using System.Collections.Generic;
using System.Xml.Serialization;

namespace CCRP_Plugin.Models
{
    public class PrivateMessageSettings
    {
        [XmlAttribute]
        public bool Enabled { get; set; }

        [XmlElement(Order = 0)]
        public string FormatFrom { get; set; }

        [XmlElement(Order = 1)]
        public string FormatTo { get; set; }

        [XmlElement(Order = 2)]
        public string FormatSpy { get; set; }

        [XmlElement(Order = 3)]
        public string ConsoleDisplayName { get; set; }

        public PrivateMessageSettings()
        {
            Enabled = true;
            FormatFrom = "(From {0}): {1}";
            FormatTo = "(To {0}): {1}";
            FormatSpy = "<gray>Spy: {0} -> {1}: {2}";
            ConsoleDisplayName = "*console*";
        }
    }

    public class VoteSettings
    {
        [XmlAttribute]
        public bool Enabled { get; set; }

        [XmlElement("Vote", Order = 0)]
        public List<VoteDefinition> Definitions { get; set; }

        public VoteSettings()
        {
            Enabled = true;
            Definitions = new List<VoteDefinition>();
        }

        public static List<VoteDefinition> CreateDefaults()
        {
            return new List<VoteDefinition>
            {
                new VoteDefinition
                {
                    Name = "Day",
                    Alias = "d",
                    Command = "/day",
                    MinimumArguments = 0,
                    Color = "#fff333",
                    Icon = string.Empty,
                    Enabled = true,
                    MinimumPlayers = 1,
                    RequiredPercent = 50,
                    Timer = 60,
                    CooldownTime = 600,
                    RequirePermission = false
                },
                new VoteDefinition
                {
                    Name = "Night",
                    Alias = "n",
                    Command = "/night",
                    MinimumArguments = 0,
                    Color = "#fff333",
                    Icon = string.Empty,
                    Enabled = true,
                    MinimumPlayers = 1,
                    RequiredPercent = 50,
                    Timer = 60,
                    CooldownTime = 600,
                    RequirePermission = false
                }
            };
        }
    }

    public class VoteDefinition
    {
        [XmlAttribute]
        public string Name { get; set; }

        [XmlAttribute]
        public string Alias { get; set; }

        [XmlAttribute]
        public string Command { get; set; }

        [XmlAttribute]
        public int MinimumArguments { get; set; }

        [XmlAttribute]
        public string Color { get; set; }

        [XmlAttribute]
        public string Icon { get; set; }

        [XmlAttribute]
        public bool Enabled { get; set; }

        [XmlAttribute]
        public int MinimumPlayers { get; set; }

        [XmlAttribute]
        public int RequiredPercent { get; set; }

        [XmlAttribute]
        public int Timer { get; set; }

        [XmlAttribute]
        public int CooldownTime { get; set; }

        [XmlAttribute]
        public bool RequirePermission { get; set; }
    }

    public class AutoAnnouncerSettings
    {
        [XmlAttribute]
        public bool Enabled { get; set; }

        [XmlAttribute]
        public int Interval { get; set; }

        [XmlAttribute]
        public int LastIndex { get; set; }

        [XmlElement("Message", Order = 0)]
        public List<AnnouncerMessage> Messages { get; set; }

        public AutoAnnouncerSettings()
        {
            Enabled = true;
            Interval = 720;
            LastIndex = 0;
            Messages = new List<AnnouncerMessage>();
        }
    }

    public class AnnouncerMessage
    {
        [XmlElement(Order = 0)]
        public string Text { get; set; }

        [XmlElement(Order = 1)]
        public string Icon { get; set; }
    }
}
