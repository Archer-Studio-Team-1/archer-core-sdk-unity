using System.Collections.Generic;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Shared extension helpers for reading typed values out of the loose
    /// IReadOnlyDictionary&lt;string, object&gt; payloads returned by Firebase Functions
    /// and Firestore document snapshots.
    /// </summary>
    internal static class DictExtensions {

        public static T TryGet<T>(this IReadOnlyDictionary<string, object> dict,
                                  string key, T fallback = default) {
            if (dict == null) return fallback;
            if (!dict.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is T t) return t;
            // Best-effort numeric widening (Firestore returns long; UI/models may want int).
            if (typeof(T) == typeof(int) && v is long lv) return (T)(object)(int)lv;
            if (typeof(T) == typeof(long) && v is int iv) return (T)(object)(long)iv;
            if (typeof(T) == typeof(double) && v is long lv2) return (T)(object)(double)lv2;
            return fallback;
        }

        public static T TryGet<T>(this IDictionary<string, object> dict,
                                  string key, T fallback = default) {
            if (dict == null) return fallback;
            if (!dict.TryGetValue(key, out var v) || v == null) return fallback;
            if (v is T t) return t;
            if (typeof(T) == typeof(int) && v is long lv) return (T)(object)(int)lv;
            if (typeof(T) == typeof(long) && v is int iv) return (T)(object)(long)iv;
            if (typeof(T) == typeof(double) && v is long lv2) return (T)(object)(double)lv2;
            return fallback;
        }
    }
}
