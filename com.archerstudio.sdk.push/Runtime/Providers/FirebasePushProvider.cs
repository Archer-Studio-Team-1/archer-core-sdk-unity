#if HAS_FIREBASE_MESSAGING
using System;
using System.Collections.Generic;
using ArcherStudio.SDK.Core;
using Firebase.Extensions;
using Firebase.Messaging;

namespace ArcherStudio.SDK.Push {

    /// <summary>
    /// Push provider backed by Firebase Cloud Messaging.
    /// Only compiled when HAS_FIREBASE_MESSAGING is defined.
    /// </summary>
    public class FirebasePushProvider : IPushProvider {
        private const string Tag = "Push-Firebase";

        public bool IsInitialized { get; private set; }

        public event Action<PushMessage> OnMessageReceived;
        public event Action<string> OnTokenRefreshed;

        public void Initialize(Action<bool> onComplete) {
            FirebaseInitializer.EnsureInitialized(available => {
                try {
                    if (!available) {
                        SDKLogger.Error(Tag, "Firebase dependencies not available.");
                        IsInitialized = false;
                        onComplete?.Invoke(false);
                        return;
                    }

                    FirebaseMessaging.TokenReceived += HandleTokenReceived;
                    FirebaseMessaging.MessageReceived += HandleMessageReceived;

                    IsInitialized = true;
                    SDKLogger.Info(Tag, "Firebase Messaging initialized.");
                    onComplete?.Invoke(true);
                } catch (Exception e) {
                    SDKLogger.Error(Tag, $"Init callback exception: {e.Message}");
                    onComplete?.Invoke(false);
                }
            });
        }

        public void RequestPermission(Action<bool> onComplete) {
            #if UNITY_IOS
            FirebaseMessaging.RequestPermissionAsync().ContinueWithOnMainThread(task => {
                if (task.IsFaulted || task.IsCanceled) {
                    SDKLogger.Warning(Tag, "Permission request failed or was cancelled.");
                    onComplete?.Invoke(false);
                    return;
                }

                SDKLogger.Info(Tag, "iOS notification permission granted.");
                onComplete?.Invoke(true);
            });
            #else
            // Android: notification permission is auto-granted (pre-13) or handled via manifest.
            SDKLogger.Info(Tag, "Permission auto-granted on this platform.");
            onComplete?.Invoke(true);
            #endif
        }

        public void GetToken(Action<string> onComplete) {
            FirebaseMessaging.GetTokenAsync().ContinueWithOnMainThread(task => {
                if (task.IsFaulted || task.IsCanceled) {
                    SDKLogger.Error(Tag, "Failed to get FCM token.");
                    onComplete?.Invoke(null);
                    return;
                }

                var token = task.Result;
                SDKLogger.Debug(Tag, $"FCM token: {token}");
                onComplete?.Invoke(token);
            });
        }

        public void SubscribeToTopic(string topic) {
            FirebaseMessaging.SubscribeAsync(topic).ContinueWithOnMainThread(task => {
                if (task.IsFaulted) {
                    SDKLogger.Error(Tag, $"Failed to subscribe to topic '{topic}'.");
                    return;
                }

                SDKLogger.Info(Tag, $"Subscribed to topic: {topic}");
            });
        }

        public void UnsubscribeFromTopic(string topic) {
            FirebaseMessaging.UnsubscribeAsync(topic).ContinueWithOnMainThread(task => {
                if (task.IsFaulted) {
                    SDKLogger.Error(Tag, $"Failed to unsubscribe from topic '{topic}'.");
                    return;
                }

                SDKLogger.Info(Tag, $"Unsubscribed from topic: {topic}");
            });
        }

        private void HandleTokenReceived(object sender, TokenReceivedEventArgs args) {
            SDKLogger.Debug(Tag, $"Token refreshed: {args.Token}");
            OnTokenRefreshed?.Invoke(args.Token);
        }

        private void HandleMessageReceived(object sender, MessageReceivedEventArgs args) {
            var firebaseMessage = args.Message;

            var data = firebaseMessage.Data != null
                ? new Dictionary<string, string>(firebaseMessage.Data)
                : new Dictionary<string, string>();

            var title = firebaseMessage.Notification?.Title ?? string.Empty;
            var body = firebaseMessage.Notification?.Body ?? string.Empty;

            var pushMessage = new PushMessage(
                title,
                body,
                data
            );

            SDKLogger.Debug(Tag, $"Message received: {pushMessage}");
            OnMessageReceived?.Invoke(pushMessage);
        }
    }
}
#endif
