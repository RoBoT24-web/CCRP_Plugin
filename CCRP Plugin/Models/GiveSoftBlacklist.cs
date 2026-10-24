using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace CCRP_Plugin.Models
{
    public class GiveSoftBlacklist
    {
        [XmlText]
        public ushort Id { get; set; }

        [XmlAttribute("BypassPermission")]
        public string Bypass { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        [XmlAttribute("ItemType")]
        public string ItemType { get; set; }

        [JsonIgnore]
        [XmlIgnore]
        public bool IsItemTypeEntry => !string.IsNullOrWhiteSpace(ItemType);
    }
}
