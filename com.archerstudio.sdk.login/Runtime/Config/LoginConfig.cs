using UnityEngine;

namespace ArcherStudio.SDK.Login {

    [CreateAssetMenu(fileName = "LoginConfig", menuName = "ArcherStudio/SDK/Login Config")]
    public class LoginConfig : ScriptableObject {

        [Header("Android / GPGS")]
        [Tooltip("Android OAuth 2.0 Client ID từ Google Play Console > Games > Setup > Linked apps.")]
        public string AndroidClientId;
    }
}
