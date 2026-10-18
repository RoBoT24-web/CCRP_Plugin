using System.Collections.Generic;

namespace CCRP_Plugin.Models
{
    public class BypassGroupRegistry
    {
        public List<string> CreatedPlayerIds { get; set; }

        public BypassGroupRegistry()
        {
            CreatedPlayerIds = new List<string>();
        }
    }
}
