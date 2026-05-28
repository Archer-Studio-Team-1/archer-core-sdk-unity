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
            return BuildSingleArgCredential(
                "Firebase.Auth.PlayGamesAuthProvider, Firebase.Auth",
                serverAuthCode);
        }

        public static Credential BuildFacebookCredential(string accessToken) {
            // Firebase.Auth.FacebookAuthProvider.GetCredential(string accessToken)
            return BuildSingleArgCredential(
                "Firebase.Auth.FacebookAuthProvider, Firebase.Auth",
                accessToken);
        }

        public static Credential BuildGoogleCredential(string idToken, string accessToken) {
            // Firebase.Auth.GoogleAuthProvider.GetCredential(string idToken, string accessToken)
            var type = System.Type.GetType("Firebase.Auth.GoogleAuthProvider, Firebase.Auth");
            if (type == null) {
                SDKLogger.Debug(Tag, "GoogleAuthProvider not present.");
                return null;
            }
            var method = type.GetMethod("GetCredential", BindingFlags.Public | BindingFlags.Static,
                                         null, new[] { typeof(string), typeof(string) }, null);
            if (method == null) {
                SDKLogger.Warning(Tag, "GoogleAuthProvider.GetCredential(string,string) not found.");
                return null;
            }
            return method.Invoke(null, new object[] { idToken, accessToken }) as Credential;
        }

        private static Credential BuildSingleArgCredential(string typeAssemblyQualifiedName, string arg) {
            var type = System.Type.GetType(typeAssemblyQualifiedName);
            if (type == null) {
                SDKLogger.Debug(Tag, $"{typeAssemblyQualifiedName} not present.");
                return null;
            }
            var method = type.GetMethod("GetCredential", BindingFlags.Public | BindingFlags.Static,
                                         null, new[] { typeof(string) }, null);
            if (method == null) {
                SDKLogger.Warning(Tag, $"{type.FullName}.GetCredential(string) not found.");
                return null;
            }
            return method.Invoke(null, new object[] { arg }) as Credential;
        }
    }
}
#endif
