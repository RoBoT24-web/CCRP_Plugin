using System;

namespace CCRP_Plugin.Models
{
    public class Playtime
    {
        public string Steam64ID { get; set; }
        public int PlayTime { get; set; }
        public DateTime LastPlaytime { get; set; }
    }
}
