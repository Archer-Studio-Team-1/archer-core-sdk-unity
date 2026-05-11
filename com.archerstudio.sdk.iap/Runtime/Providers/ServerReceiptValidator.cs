#if HAS_UNITY_IAP
using System;
using System.Text;
using ArcherStudio.SDK.Core;
using ArcherStudio.SDK.Core.UI;
using UnityEngine;
using UnityEngine.Networking;

namespace ArcherStudio.SDK.IAP {

    /// <summary>
    /// Server-side receipt validator that calls Firebase Functions endpoint.
    /// Sends receipt/purchase token to server for server-to-server verification
    /// with Google Play API / Apple App Store Server API.
    ///
    /// FAIL-CLOSE policy: if server is unreachable after retries, purchase is REJECTED.
    /// This prevents bypass via network blocking.
    /// </summary>
    public class ServerReceiptValidator : IReceiptValidator {
        private const string Tag = "IAP.ServerValidator";
        private const int MaxRetries = 2;
        private static readonly float[] RetryDelays = { 1f, 3f };

        private readonly string _serverUrl;
        private readonly string _apiKey;
        private readonly float _timeoutSeconds;
        private readonly bool _showLoadingUI;
        private SDKLoadingOverlay _loadingOverlay;

        public ServerReceiptValidator(string serverUrl, string apiKey = null, float timeoutSeconds = 15f, bool showLoadingUI = true) {
            _serverUrl = serverUrl;
            _apiKey = apiKey;
            _timeoutSeconds = timeoutSeconds;
            _showLoadingUI = showLoadingUI;
        }

        public void Validate(string receipt, string productId, Action<ReceiptValidationResult> onComplete) {
            if (string.IsNullOrEmpty(_serverUrl)) {
                SDKLogger.Warning(Tag, "ValidationServerUrl is empty. Skipping server validation.");
                onComplete?.Invoke(new ReceiptValidationResult(true, productId, null));
                return;
            }

            var payload = BuildPayload(receipt, productId);
            if (payload == null) {
                SDKLogger.Warning(Tag, "Could not build validation payload. Rejecting purchase.");
                onComplete?.Invoke(new ReceiptValidationResult(false, productId, "Failed to build validation payload"));
                return;
            }

            if (_showLoadingUI) {
                _loadingOverlay = SDKLoadingOverlay.Show(
                    timeoutOverride: _timeoutSeconds + MaxRetries * 5f);
            }

            IAPCoroutineRunner.Run(
                SendWithRetry(payload, productId, 0, result => {
                    _loadingOverlay?.Dismiss();
                    _loadingOverlay = null;
                    onComplete?.Invoke(result);
                }));
        }

        private System.Collections.IEnumerator SendWithRetry(
            string jsonPayload, string productId, int attempt, Action<ReceiptValidationResult> onComplete) {

            SDKLogger.Debug(Tag, $"Validation request for {productId} (attempt {attempt + 1}/{MaxRetries + 1})...");

            var request = new UnityWebRequest(_serverUrl, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = (int)_timeoutSeconds;

            if (!string.IsNullOrEmpty(_apiKey)) {
                request.SetRequestHeader("x-api-key", _apiKey);
            }

            #if HAS_SDK_APPCHECK
            // Attach App Check token if available (synchronous cache read)
            string appCheckToken = null;
            bool tokenReady = false;

            var appCheck = AppCheck.AppCheckManager.Instance;
            if (appCheck != null) {
                appCheck.GetToken(token => {
                    appCheckToken = token;
                    tokenReady = true;
                });

                float waited = 0f;
                while (!tokenReady && waited < 3f) {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                }
            }

            if (!string.IsNullOrEmpty(appCheckToken)) {
                request.SetRequestHeader("X-Firebase-AppCheck", appCheckToken);
            }
            #endif

            yield return request.SendWebRequest();

            var responseCode = request.responseCode;
            var isNetworkError = request.result == UnityWebRequest.Result.ConnectionError;
            var isTimeout = request.result == UnityWebRequest.Result.ConnectionError &&
                            request.error != null && request.error.Contains("timeout");

            // 401/403: fail-close immediately (unauthorized / App Check rejected)
            if (responseCode == 401 || responseCode == 403) {
                SDKLogger.Warning(Tag,
                    $"Server rejected request for {productId}: HTTP {responseCode}. " +
                    "Purchase rejected (unauthorized).");
                onComplete?.Invoke(new ReceiptValidationResult(false, productId, $"Server auth failed: HTTP {responseCode}"));
                request.Dispose();
                yield break;
            }

            // 400: fail-close (bad request = invalid receipt)
            if (responseCode == 400) {
                var errorMsg = TryParseError(request.downloadHandler?.text) ?? "Bad request";
                SDKLogger.Warning(Tag, $"Server returned 400 for {productId}: {errorMsg}");
                onComplete?.Invoke(new ReceiptValidationResult(false, productId, errorMsg));
                request.Dispose();
                yield break;
            }

            // 429: rate limited — retry
            if (responseCode == 429) {
                SDKLogger.Warning(Tag, $"Rate limited for {productId}.");
                request.Dispose();
                if (attempt < MaxRetries) {
                    var delay = attempt < RetryDelays.Length ? RetryDelays[attempt] : RetryDelays[RetryDelays.Length - 1];
                    yield return new WaitForSecondsRealtime(delay);
                    yield return SendWithRetry(jsonPayload, productId, attempt + 1, onComplete);
                } else {
                    onComplete?.Invoke(new ReceiptValidationResult(false, productId, "Rate limited after retries",
                        isRetryable: true));
                }
                yield break;
            }

            // Network error or 5xx: retry, then fail-close
            if (isNetworkError || responseCode >= 500) {
                SDKLogger.Warning(Tag,
                    $"Request failed for {productId}: " +
                    $"{(isNetworkError ? request.error : $"HTTP {responseCode}")} " +
                    $"(attempt {attempt + 1}/{MaxRetries + 1})");
                request.Dispose();

                if (attempt < MaxRetries) {
                    var delay = attempt < RetryDelays.Length ? RetryDelays[attempt] : RetryDelays[RetryDelays.Length - 1];
                    yield return new WaitForSecondsRealtime(delay);
                    yield return SendWithRetry(jsonPayload, productId, attempt + 1, onComplete);
                } else {
                    SDKLogger.Error(Tag,
                        $"Server validation failed after {MaxRetries + 1} attempts for {productId}. " +
                        "Purchase REJECTED (fail-close policy).");
                    onComplete?.Invoke(new ReceiptValidationResult(false, productId, "Server unreachable after retries",
                        isRetryable: true));
                }
                yield break;
            }

            // 200 OK: parse response
            if (request.result == UnityWebRequest.Result.Success) {
                try {
                    var response = JsonUtility.FromJson<ValidationResponse>(request.downloadHandler.text);

                    if (response.valid) {
                        SDKLogger.Info(Tag, $"Server validated purchase: {productId}" +
                            (response.duplicate ? " (duplicate)" : "") +
                            (response.isTestPurchase ? " (test)" : ""));
                        onComplete?.Invoke(new ReceiptValidationResult(
                            true, productId, null, response.isTestPurchase));
                    } else {
                        SDKLogger.Warning(Tag,
                            $"Server rejected purchase {productId}: {response.error}");
                        onComplete?.Invoke(new ReceiptValidationResult(
                            false, productId, response.error ?? "Server validation failed"));
                    }
                } catch (Exception e) {
                    SDKLogger.Error(Tag, $"Failed to parse validation response: {e.Message}");
                    onComplete?.Invoke(new ReceiptValidationResult(false, productId, "Invalid server response"));
                }
            } else {
                SDKLogger.Warning(Tag, $"Unexpected result for {productId}: {request.result}");
                onComplete?.Invoke(new ReceiptValidationResult(false, productId, $"Unexpected: {request.result}"));
            }

            request.Dispose();
        }

        private static string TryParseError(string responseBody) {
            if (string.IsNullOrEmpty(responseBody)) return null;
            try {
                var response = JsonUtility.FromJson<ValidationResponse>(responseBody);
                return response.error;
            } catch {
                return null;
            }
        }

        private string BuildPayload(string receipt, string productId) {
            try {
                #if UNITY_ANDROID
                return BuildGooglePayload(receipt, productId);
                #elif UNITY_IOS
                return BuildApplePayload(receipt, productId);
                #else
                SDKLogger.Warning(Tag, "Server validation not supported on this platform.");
                return null;
                #endif
            } catch (Exception e) {
                SDKLogger.Error(Tag, $"Error building payload: {e.Message}");
                return null;
            }
        }

        #if UNITY_ANDROID
        private string BuildGooglePayload(string receipt, string productId) {
            var outerReceipt = JsonUtility.FromJson<UnityIAPReceipt>(receipt);
            if (outerReceipt == null || string.IsNullOrEmpty(outerReceipt.Payload)) {
                SDKLogger.Warning(Tag, "Invalid receipt format.");
                return null;
            }

            var gpPayload = JsonUtility.FromJson<GooglePlayPayload>(outerReceipt.Payload);
            if (gpPayload == null || string.IsNullOrEmpty(gpPayload.json)) {
                SDKLogger.Warning(Tag, "Invalid Google Play payload.");
                return null;
            }

            var purchaseData = JsonUtility.FromJson<GooglePlayPurchaseData>(gpPayload.json);
            if (purchaseData == null || string.IsNullOrEmpty(purchaseData.purchaseToken)) {
                SDKLogger.Warning(Tag, "No purchaseToken in receipt.");
                return null;
            }

            var request = new ValidationRequest {
                platform = "google",
                productId = productId,
                purchaseToken = purchaseData.purchaseToken,
                packageName = Application.identifier,
            };

            return JsonUtility.ToJson(request);
        }
        #endif

        #if UNITY_IOS
        private string BuildApplePayload(string receipt, string productId) {
            var request = new ValidationRequest {
                platform = "apple",
                productId = productId,
                receipt = receipt,
            };

            return JsonUtility.ToJson(request);
        }
        #endif

        // ─── Subscription Status Query ───

        public void QuerySubscriptionStatus(string purchaseToken, string transactionId,
            string productId, Action<SubscriptionStatusResult> onComplete) {

            if (string.IsNullOrEmpty(_serverUrl)) {
                onComplete?.Invoke(SubscriptionStatusResult.Failed("ValidationServerUrl is empty."));
                return;
            }

            var subscriptionUrl = _serverUrl.Replace("validatePurchase", "validateSubscription");

            var payload = new SubscriptionQueryRequest {
                productId = productId,
                #if UNITY_ANDROID
                platform = "google",
                purchaseToken = purchaseToken,
                packageName = Application.identifier,
                #elif UNITY_IOS
                platform = "apple",
                transactionId = transactionId,
                #else
                platform = "unknown",
                #endif
            };

            var json = JsonUtility.ToJson(payload);
            IAPCoroutineRunner.Run(SendSubscriptionQuery(subscriptionUrl, json, productId, onComplete));
        }

        private System.Collections.IEnumerator SendSubscriptionQuery(
            string url, string jsonPayload, string productId,
            Action<SubscriptionStatusResult> onComplete) {

            var request = new UnityWebRequest(url, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = (int)_timeoutSeconds;

            if (!string.IsNullOrEmpty(_apiKey)) {
                request.SetRequestHeader("x-api-key", _apiKey);
            }

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200) {
                try {
                    var response = JsonUtility.FromJson<SubscriptionQueryResponse>(request.downloadHandler.text);
                    var status = MapSubscriptionStatus(response.status);

                    System.DateTime? expirationDate = null;
                    System.DateTime? purchaseDate = null;

                    if (!string.IsNullOrEmpty(response.expirationDate)) {
                        if (System.DateTime.TryParse(response.expirationDate, null,
                                System.Globalization.DateTimeStyles.RoundtripKind, out var exp))
                            expirationDate = exp;
                    }

                    if (!string.IsNullOrEmpty(response.purchaseDate)) {
                        if (System.DateTime.TryParse(response.purchaseDate, null,
                                System.Globalization.DateTimeStyles.RoundtripKind, out var pur))
                            purchaseDate = pur;
                    }

                    onComplete?.Invoke(new SubscriptionStatusResult(
                        response.valid,
                        status,
                        expirationDate,
                        purchaseDate,
                        null,
                        response.autoRenewing,
                        response.isFreeTrial,
                        response.error));

                } catch (System.Exception e) {
                    SDKLogger.Error(Tag, $"Failed to parse subscription status: {e.Message}");
                    onComplete?.Invoke(SubscriptionStatusResult.Failed($"Parse error: {e.Message}"));
                }
            } else {
                SDKLogger.Warning(Tag,
                    $"Subscription status query failed: {request.result} (HTTP {request.responseCode})");
                onComplete?.Invoke(SubscriptionStatusResult.Failed(
                    $"HTTP {request.responseCode}: {request.error}"));
            }

            request.Dispose();
        }

        private static SubscriptionStatus MapSubscriptionStatus(string status) {
            switch (status) {
                case "active": return SubscriptionStatus.Active;
                case "cancelled": return SubscriptionStatus.Cancelled;
                case "grace_period": return SubscriptionStatus.GracePeriod;
                case "account_hold": return SubscriptionStatus.AccountHold;
                case "paused": return SubscriptionStatus.Paused;
                case "expired": return SubscriptionStatus.Expired;
                default: return SubscriptionStatus.Unknown;
            }
        }

        // ─── JSON Models ───

        [Serializable]
        private class ValidationRequest {
            public string platform;
            public string productId;
            public string purchaseToken;
            public string transactionId;
            public string receipt;
            public string packageName;
        }

        [Serializable]
        private class ValidationResponse {
            public bool valid;
            public string productId;
            public string transactionId;
            public string error;
            public bool duplicate;
            public bool isTestPurchase;
        }

        [Serializable]
        private class UnityIAPReceipt {
            public string Store;
            public string Payload;
        }

        [Serializable]
        private class GooglePlayPayload {
            public string json;
            public string signature;
        }

        [Serializable]
        private class GooglePlayPurchaseData {
            public string purchaseToken;
            public string packageName;
            public string orderId;
        }

        [Serializable]
        private class SubscriptionQueryRequest {
            public string platform;
            public string productId;
            public string purchaseToken;
            public string transactionId;
            public string packageName;
        }

        [Serializable]
        private class SubscriptionQueryResponse {
            public bool valid;
            public string productId;
            public string expirationDate;
            public string purchaseDate;
            public bool autoRenewing;
            public bool cancelled;
            public bool isFreeTrial;
            public string status;
            public string state;
            public string error;
        }
    }
}
#endif
