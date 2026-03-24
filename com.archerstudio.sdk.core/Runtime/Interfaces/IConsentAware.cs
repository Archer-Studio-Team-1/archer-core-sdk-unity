namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Marker interface for components that react to consent changes.
    /// </summary>
    public interface IConsentAware {
        void OnConsentChanged(ConsentStatus consent);
    }
}
