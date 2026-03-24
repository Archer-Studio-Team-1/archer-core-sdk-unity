using System;
using System.Collections.Generic;
using System.Threading;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Base interface for all SDK modules. Each module (Ads, Tracking, IAP, etc.)
    /// implements this to participate in the SDK initialization lifecycle.
    /// </summary>
    public interface ISDKModule : IDisposable {
        /// <summary>
        /// Unique identifier for this module (e.g., "core", "ads", "tracking").
        /// </summary>
        string ModuleId { get; }

        /// <summary>
        /// Lower value = earlier initialization. Consent=0, Analytics=10, Tracking=20, Ads=50.
        /// </summary>
        int InitializationPriority { get; }

        /// <summary>
        /// List of ModuleIds this module depends on. These must be initialized first.
        /// </summary>
        IReadOnlyList<string> Dependencies { get; }

        /// <summary>
        /// Current state of this module.
        /// </summary>
        ModuleState State { get; }

        /// <summary>
        /// Initialize this module asynchronously. Called by SDKInitializer in dependency order.
        /// </summary>
        void InitializeAsync(SDKCoreConfig coreConfig, Action<bool> onComplete);

        /// <summary>
        /// Called when user consent status changes. Modules should adjust behavior accordingly.
        /// </summary>
        void OnConsentChanged(ConsentStatus consent);
    }

    public enum ModuleState {
        NotInitialized,
        Initializing,
        Ready,
        Failed,
        Disposed
    }
}
