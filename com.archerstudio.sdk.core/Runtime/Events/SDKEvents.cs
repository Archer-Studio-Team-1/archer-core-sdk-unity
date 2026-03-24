namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Published when user consent status changes.
    /// All IConsentAware modules should subscribe to this.
    /// </summary>
    public readonly struct ConsentChangedEvent : ISDKEvent {
        public ConsentStatus Status { get; }

        public ConsentChangedEvent(ConsentStatus status) {
            Status = status;
        }
    }

    /// <summary>
    /// Published when all SDK modules have finished initialization.
    /// Game code can subscribe to know when the SDK is ready.
    /// </summary>
    public readonly struct SDKReadyEvent : ISDKEvent {
        public bool Success { get; }
        public int FailedModuleCount { get; }

        public SDKReadyEvent(bool success, int failedModuleCount) {
            Success = success;
            FailedModuleCount = failedModuleCount;
        }
    }

    /// <summary>
    /// Published when a single module completes initialization.
    /// </summary>
    public readonly struct ModuleInitializedEvent : ISDKEvent {
        public string ModuleId { get; }
        public bool Success { get; }

        public ModuleInitializedEvent(string moduleId, bool success) {
            ModuleId = moduleId;
            Success = success;
        }
    }

    /// <summary>
    /// Published when the bootstrap scene starts transitioning to the next scene.
    /// Game code can subscribe to perform last-minute setup.
    /// </summary>
    public readonly struct BootstrapCompleteEvent : ISDKEvent {
        public float ElapsedSeconds { get; }
        public int TotalModules { get; }
        public int FailedModules { get; }

        public BootstrapCompleteEvent(float elapsedSeconds, int totalModules, int failedModules) {
            ElapsedSeconds = elapsedSeconds;
            TotalModules = totalModules;
            FailedModules = failedModules;
        }
    }
}
