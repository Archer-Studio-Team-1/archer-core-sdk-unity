#if HAS_FIREBASE_FIRESTORE && HAS_FIREBASE_AUTH
using System;
using System.Collections.Generic;
using System.Text;
using ArcherStudio.SDK.Core;
using Firebase.Extensions;
using UnityEngine;
using UnityEngine.Networking;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Replaces Firebase.Functions Unity SDK. Calls callable Cloud Functions
    /// via raw HTTPS using the documented callable wire protocol:
    /// <code>
    ///   POST https://{region}-{projectId}.cloudfunctions.net/{name}
    ///   Authorization: Bearer {firebaseIdToken}
    ///   Content-Type: application/json
    ///   Body: { "data": {...} }
    ///   Response (2xx): { "result": {...} }
    ///   Response (4xx/5xx): { "error": { "status": "...", "message": "...", "details": ... } }
    /// </code>
    ///
    /// Why HTTP instead of Firebase.Functions Unity SDK:
    ///   - Firebase.Functions 13.x has a packaging bug (Firebase.App.Internal asmdef missing)
    ///   - SDK pulls ~2-3 MB of native gRPC libraries we don't need elsewhere
    ///   - Plain HTTPS is trivial to debug (Charles Proxy / Wireshark) and to mock in tests
    ///   - Server-side Cloud Functions stay unchanged (still onCall handlers)
    /// </summary>
    internal static class CallableHttpClient {

        private const string Tag = "Firestore";
        private const int TimeoutSec = 30;
        private const string DefaultRegion = "us-central1";

        public static void CallAsync(FirestoreConfig config, string name,
                                     IReadOnlyDictionary<string, object> payload,
                                     Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete) {
            string projectId = Firebase.FirebaseApp.DefaultInstance?.Options?.ProjectId;
            if (string.IsNullOrEmpty(projectId)) {
                onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                    FirestoreErrorCode.Unavailable, "Firebase project id unavailable"));
                return;
            }
            string region = string.IsNullOrEmpty(config?.FunctionsRegion) ? DefaultRegion : config.FunctionsRegion;
            string url = $"https://{region}-{projectId}.cloudfunctions.net/{name}";

            var user = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;
            if (user == null) {
                onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                    FirestoreErrorCode.NotAuthenticated, "no firebase user"));
                return;
            }

            user.TokenAsync(false).ContinueWithOnMainThread(tokenTask => {
                if (tokenTask.IsFaulted || tokenTask.IsCanceled ||
                    string.IsNullOrEmpty(tokenTask.Result)) {
                    string err = tokenTask.Exception?.Flatten().InnerException?.Message ?? "id token fetch failed";
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        FirestoreErrorCode.NotAuthenticated, err));
                    return;
                }
                // Best-effort App Check token. If enforcement is on and the token
                // is missing the Function rejects with PERMISSION_DENIED — we still
                // send the request so the server gets to log the rejection.
                TryFetchAppCheckToken(appCheckToken =>
                    SendRequest(url, tokenTask.Result, appCheckToken, payload, name, config, onComplete));
            });
        }

        private static void TryFetchAppCheckToken(Action<string> onComplete) {
#if HAS_FIREBASE_APP_CHECK
            try {
                var appCheck = Firebase.AppCheck.FirebaseAppCheck.DefaultInstance;
                if (appCheck == null) { onComplete(null); return; }
                appCheck.GetAppCheckTokenAsync(forceRefresh: false).ContinueWithOnMainThread(t => {
                    if (t.IsFaulted || t.IsCanceled) {
                        onComplete(null);
                        return;
                    }
                    // Typed-task cast — ContinueWithOnMainThread surfaces the base Task.
                    var typed = t as System.Threading.Tasks.Task<Firebase.AppCheck.AppCheckToken>;
                    onComplete(typed != null ? typed.Result.Token : null);
                });
            } catch {
                onComplete(null);
            }
#else
            onComplete(null);
#endif
        }

        private static void SendRequest(string url, string idToken, string appCheckToken,
                                        IReadOnlyDictionary<string, object> payload, string name,
                                        FirestoreConfig config,
                                        Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete) {
            var envelope = new Dictionary<string, object> {
                { "data", payload as IDictionary<string, object> ?? new Dictionary<string, object>() }
            };
            string body = PolymorphicJsonConverter.NormalizeJson(envelope);

            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + idToken);
            if (!string.IsNullOrEmpty(appCheckToken)) {
                req.SetRequestHeader("X-Firebase-AppCheck", appCheckToken);
            }
            req.timeout = TimeoutSec;

            if (config != null && config.VerboseLogging) {
                SDKLogger.Info(Tag, $"Callable POST {url} body.len={body.Length}");
            }

            var op = req.SendWebRequest();
            op.completed += _ => HandleResponse(req, name, config, onComplete);
        }

        private static void HandleResponse(UnityWebRequest req, string name, FirestoreConfig config,
                                           Action<FirestoreResult<IReadOnlyDictionary<string, object>>> onComplete) {
            try {
                long status = req.responseCode;
                string text = req.downloadHandler?.text ?? string.Empty;

                if (config != null && config.VerboseLogging) {
                    SDKLogger.Info(Tag, $"Callable {name} → HTTP {status}, body.len={text.Length}");
                }

                if (req.result == UnityWebRequest.Result.ConnectionError) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        FirestoreErrorCode.NetworkError, $"connection error: {req.error}"));
                    return;
                }

                if (string.IsNullOrEmpty(text)) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        MapHttpStatus(status), $"empty response (HTTP {status})"));
                    return;
                }

                object parsed;
                try { parsed = MiniJson.Deserialize(text); }
                catch (Exception jx) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        FirestoreErrorCode.InternalError, $"json parse error: {jx.Message}"));
                    return;
                }
                if (!(parsed is IDictionary<string, object> root)) {
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        FirestoreErrorCode.InternalError, "response not a JSON object"));
                    return;
                }

                // Error envelope: { "error": { "status": ..., "message": ... } }
                if (root.TryGetValue("error", out var errObj) && errObj is IDictionary<string, object> err) {
                    string statusStr = err.TryGetValue("status", out var s) ? s?.ToString() : null;
                    string messageStr = err.TryGetValue("message", out var m) ? m?.ToString() : null;
                    onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                        MapCallableStatus(statusStr, status),
                        messageStr ?? $"function returned error ({statusStr})"));
                    return;
                }

                // Success envelope: { "result": <any> }
                if (root.TryGetValue("result", out var resultObj)) {
                    if (resultObj is IDictionary<string, object> dict) {
                        onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Succeeded(
                            new Dictionary<string, object>(dict)));
                    } else if (resultObj == null) {
                        onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Succeeded(
                            new Dictionary<string, object>()));
                    } else {
                        // Scalar / array → wrap so callers always see a dict
                        onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Succeeded(
                            new Dictionary<string, object> { { "value", resultObj } }));
                    }
                    return;
                }

                onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                    FirestoreErrorCode.InternalError, "response missing 'result' and 'error'"));
            } catch (Exception ex) {
                SDKLogger.Error(Tag, $"Callable {name} handler crashed: {ex}");
                onComplete?.Invoke(FirestoreResult<IReadOnlyDictionary<string, object>>.Failed(
                    FirestoreErrorCode.InternalError, ex.Message));
            } finally {
                req.Dispose();
            }
        }

        private static FirestoreErrorCode MapHttpStatus(long status) {
            if (status == 401 || status == 403) return FirestoreErrorCode.NotAuthenticated;
            if (status == 404) return FirestoreErrorCode.NotFound;
            if (status == 408) return FirestoreErrorCode.Cancelled;
            if (status == 429) return FirestoreErrorCode.QuotaExceeded;
            if (status >= 500) return FirestoreErrorCode.Unavailable;
            if (status >= 400) return FirestoreErrorCode.InvalidArgument;
            return FirestoreErrorCode.InternalError;
        }

        // Callable canonical error codes, see firebase.google.com/docs/reference/functions/...
        private static FirestoreErrorCode MapCallableStatus(string statusName, long httpStatus) {
            switch (statusName) {
                case "CANCELLED":
                case "DEADLINE_EXCEEDED":      return FirestoreErrorCode.Cancelled;
                case "INVALID_ARGUMENT":
                case "ALREADY_EXISTS":
                case "FAILED_PRECONDITION":
                case "OUT_OF_RANGE":            return FirestoreErrorCode.InvalidArgument;
                case "NOT_FOUND":               return FirestoreErrorCode.NotFound;
                case "PERMISSION_DENIED":       return FirestoreErrorCode.PermissionDenied;
                case "RESOURCE_EXHAUSTED":      return FirestoreErrorCode.QuotaExceeded;
                case "UNAUTHENTICATED":         return FirestoreErrorCode.NotAuthenticated;
                case "UNAVAILABLE":             return FirestoreErrorCode.Unavailable;
                case "INTERNAL":
                case "UNKNOWN":
                case "DATA_LOSS":               return FirestoreErrorCode.InternalError;
                default:                        return MapHttpStatus(httpStatus);
            }
        }
    }
}
#endif
