using UnityEngine;

namespace ArcherStudio.SDK.CloudSave {

    /// <summary>
    /// Configuration for the CloudSave module.
    /// Create via: Assets > Create > ArcherStudio > SDK > Cloud Save Config.
    /// Place in a Resources folder as "CloudSaveConfig".
    /// </summary>
    [CreateAssetMenu(fileName = "CloudSaveConfig", menuName = "ArcherStudio/SDK/Cloud Save Config")]
    public class CloudSaveConfig : ScriptableObject {

        [Header("Firebase Authentication")]
        [Tooltip("Web Client ID từ Firebase Console > Authentication > Sign-in method > Google Play Games. " +
                 "Required for Firebase Auth via GPGS server-side access code.")]
        public string WebClientId;
    }
}
