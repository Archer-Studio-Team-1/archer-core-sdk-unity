using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Reads IAP catalog from Firestore. Cached client-side per FirestoreConfig.IapCatalogCacheTtlMs.
    /// </summary>
    public interface IIapCatalogService {

        /// <summary>Fetch active products. Cached.</summary>
        void GetCatalogAsync(Action<FirestoreResult<IReadOnlyList<IapProduct>>> onComplete);

        /// <summary>Look up a single product. Hits cache first.</summary>
        void GetProductAsync(string productId, Action<FirestoreResult<IapProduct>> onComplete);

        /// <summary>Bypass cache and re-fetch from Firestore.</summary>
        void InvalidateCache();
    }
}
