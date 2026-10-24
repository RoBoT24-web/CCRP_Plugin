using System.Collections.Generic;

namespace CCRP_Plugin.Models
{
    public class BypassGroupRegistry
    {
        public List<string> CreatedPlayerIds { get; set; }
        public List<string> ShopCreatedPlayerIds { get; set; }

        public BypassGroupRegistry()
        {
            CreatedPlayerIds = new List<string>();
            ShopCreatedPlayerIds = new List<string>();
        }
    }
}
