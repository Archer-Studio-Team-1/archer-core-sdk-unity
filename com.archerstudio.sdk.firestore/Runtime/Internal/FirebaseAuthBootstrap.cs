#if HAS_FIREBASE_FIRESTORE && HAS_FIREBASE_AUTH
using System.Reflection;
using ArcherStudio.SDK.Core;
using Firebase.Auth;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Builds Firebase Auth credentials without hard-referencing the Play Games provider type,
    /// so this assembly compiles even on iOS / Editor where the Play Games package is absent.
    /// </summary>
    internal static class FirebaseAuthBootstrap {

        private const string Tag = "Firestore";

        public static Credential BuildPlayGamesCredential(string serverAuthCode) {
            // Firebase.Auth.PlayGamesAuthProvider.GetCredential(string)
            var type = System.Type.GetType("Firebase.Auth.PlayGamesAuthProvider, Firebase.Auth");
            if (type == null) {
                SDKLogger.Debug(Tag, "PlayGamesAuthProvider not present. Anonymous fallback.");
                return null;
            }
            var method = type.GetMethod("GetCredential", BindingFlags.Public | BindingFlags.Static,
                                         null, new[] { typeof(string) }, null);
            if (method == null) return null;
            return method.Invoke(null, new object[] { serverAuthCode }) as Credential;
        }
    }
}
#endif
