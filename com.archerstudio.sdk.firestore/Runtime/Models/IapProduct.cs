using System.Collections.Generic;

namespace ArcherStudio.SDK.Firestore {

    public sealed class IapProduct {
        public string ProductId { get; set; }
        public string Kind { get; set; }                  // "consumable"|"non_consumable"|"subscription"
        public string DisplayName { get; set; }
        public double PriceUsdEstimate { get; set; }
        public IapProductGrants Grants { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public sealed class IapProductGrants {
        public IReadOnlyDictionary<string, string> Currencies { get; set; }
        public IReadOnlyList<string> Entitlements { get; set; }
        public string VipTier { get; set; }
        public int VipDurationDays { get; set; }
    }
}
