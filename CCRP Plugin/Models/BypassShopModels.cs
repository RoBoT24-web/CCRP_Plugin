using System;
using System.Collections.Generic;

namespace CCRP_Plugin.Models
{
    public class BypassShopCatalog
    {
        public List<BypassShopOffer> Offers { get; set; }

        public BypassShopCatalog()
        {
            Offers = new List<BypassShopOffer>();
        }
    }

    public class BypassShopOffer
    {
        public string Type { get; set; }
        public ushort AssetId { get; set; }
        public decimal Cost { get; set; }
    }

    public class BypassPurchaseRegistry
    {
        public List<BypassPurchase> Purchases { get; set; }

        public BypassPurchaseRegistry()
        {
            Purchases = new List<BypassPurchase>();
        }
    }

    public class BypassPurchase
    {
        public string PlayerId { get; set; }
        public string Type { get; set; }
        public ushort AssetId { get; set; }
        public string BypassPermission { get; set; }
        public decimal Cost { get; set; }
        public DateTime PurchasedAtUtc { get; set; }
    }
}
