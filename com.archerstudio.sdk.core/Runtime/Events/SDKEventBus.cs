using System;
using System.Collections.Generic;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Marker interface for all SDK events.
    /// Events should be readonly structs to avoid GC allocation.
    /// </summary>
    public interface ISDKEvent { }

    /// <summary>
    /// Lightweight, type-safe event bus for inter-module communication.
    /// Uses static generics for zero-lookup-cost dispatch.
    ///
    /// Usage:
    ///   SDKEventBus.Subscribe&lt;ConsentChangedEvent&gt;(OnConsentChanged);
    ///   SDKEventBus.Publish(new ConsentChangedEvent(status));
    ///   SDKEventBus.Unsubscribe&lt;ConsentChangedEvent&gt;(OnConsentChanged);
    /// </summary>
    public static class SDKEventBus {

        /// <summary>
        /// Per-type handler storage. Static generic class ensures one list per event type.
        /// </summary>
        private static class Handlers<T> where T : struct, ISDKEvent {
            internal static readonly List<Action<T>> List = new List<Action<T>>();
        }

        public static void Subscribe<T>(Action<T> handler) where T : struct, ISDKEvent {
            if (handler == null) return;
            var list = Handlers<T>.List;
            if (!list.Contains(handler)) {
                list.Add(handler);
            }
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct, ISDKEvent {
            if (handler == null) return;
            Handlers<T>.List.Remove(handler);
        }

        /// <summary>
        /// Publish an event to all subscribers. Iterates in reverse to allow
        /// safe unsubscription during handler execution.
        /// </summary>
        public static void Publish<T>(T evt) where T : struct, ISDKEvent {
            var list = Handlers<T>.List;
            for (int i = list.Count - 1; i >= 0; i--) {
                try {
                    list[i]?.Invoke(evt);
                } catch (Exception e) {
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        /// <summary>
        /// Remove all subscribers for a specific event type.
        /// </summary>
        public static void Clear<T>() where T : struct, ISDKEvent {
            Handlers<T>.List.Clear();
        }
    }
}
