#if HAS_FIREBASE_FIRESTORE && HAS_FIREBASE_AUTH
using System;
using System.Collections.Generic;
using System.Reflection;
using ArcherStudio.SDK.Core;
using Firebase.Extensions;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Thin reflection wrapper around Firebase.Functions so this assembly does not
    /// fail to compile when the Functions package is absent. When HAS_FIREBASE_FUNCTIONS
    /// is defined we still load via reflection — the asmdef "references" entry only
    /// resolves at runtime, not at compile time of this file.
    /// </summary>
    internal static class FirebaseFunctionsBridge {

        private const string Tag = "Firestore";

        public static object GetInstance(string region) {
            // FirebaseFunctions.GetInstance(region)
            var type = Type.GetType("Firebase.Functions.FirebaseFunctions, Firebase.Functions");
            if (type == null) return null;
            var method = type.GetMethod("GetInstance", BindingFlags.Public | BindingFlags.Static,
                                         null, new[] { typeof(string) }, null);
            return method?.Invoke(null, new object[] { region });
        }

        public static void CallAsync(object functions, string name, string region,
                                     IReadOnlyDictionary<string, object> payload,
                                     Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete) {
            try {
                var callable = functions.GetType()
                    .GetMethod("GetHttpsCallable", new[] { typeof(string) })
                    ?.Invoke(functions, new object[] { name });
                if (callable == null) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        FirestoreErrorCode.Unavailable, "GetHttpsCallable not found"));
                    return;
                }
                var dict = payload as IDictionary<string, object> ?? new Dictionary<string, object>();
                var task = (System.Threading.Tasks.Task)callable.GetType()
                    .GetMethod("CallAsync", new[] { typeof(object) })
                    ?.Invoke(callable, new object[] { dict });
                if (task == null) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        FirestoreErrorCode.Unavailable, "CallAsync not found"));
                    return;
                }
                task.ContinueWithOnMainThread(t => {
                    if (t.IsFaulted) {
                        SDKLogger.Warning(Tag, $"Function {name} failed: {t.Exception?.Message}");
                        onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                            FirestoreErrorCode.InternalError, t.Exception?.Message));
                        return;
                    }
                    var resultProp = t.GetType().GetProperty("Result");
                    var httpsResult = resultProp?.GetValue(t);
                    var data = httpsResult?.GetType().GetProperty("Data")?.GetValue(httpsResult)
                               as IDictionary<string, object>;
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Succeeded(
                        (IReadOnlyDictionary<string, object>)data ?? new Dictionary<string, object>()));
                });
            } catch (Exception ex) {
                SDKLogger.Error(Tag, $"Functions reflection failed: {ex.Message}");
                onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                    FirestoreErrorCode.InternalError, ex.Message));
            }
        }
    }
}
#endif
