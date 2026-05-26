using System;
using System.Collections.Generic;
using System.Linq;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Converts JSON ⇄ Dictionary&lt;string, object&gt; (the shape Firestore expects).
    ///
    /// Polymorphic safety: any object that needs discriminator-based dispatch (e.g.
    /// IDK's AbilityBaseData hierarchy) should include a `_kind` field at the local
    /// JSON layer. This converter never strips or rewrites that field — it round-trips
    /// the discriminator transparently so the caller's deserialiser can pick the right
    /// concrete type.
    ///
    /// Why not Newtonsoft TypeNameHandling.Auto?
    /// - $type embedding writes fully-qualified .NET type names → leaks assembly info
    ///   into Firestore → security risk + breaks if you rename a class.
    /// - Discriminator strings are stable, game-owned, and small.
    ///
    /// This module also normalizes JSON (sorted keys, consistent number formatting)
    /// so checksum compare across serialize cycles is stable.
    /// </summary>
    public static class PolymorphicJsonConverter {

        /// <summary>
        /// Deep-converts a parsed object tree (Dictionary / List / primitives) into
        /// the dictionary shape Firestore Admin/Client SDK consumes.
        /// </summary>
        public static IReadOnlyDictionary<string, object> ToFirestoreDict(IDictionary<string, object> source) {
            if (source == null) return new Dictionary<string, object>();
            var output = new Dictionary<string, object>(source.Count);
            foreach (var kv in source) {
                output[kv.Key] = NormalizeValue(kv.Value);
            }
            return output;
        }

        /// <summary>
        /// Inverse: Firestore-returned data → caller-friendly Dictionary&lt;string, object&gt;
        /// preserving the polymorphic discriminator.
        /// </summary>
        public static IDictionary<string, object> FromFirestoreDict(IReadOnlyDictionary<string, object> source) {
            if (source == null) return new Dictionary<string, object>();
            var output = new Dictionary<string, object>(source.Count);
            foreach (var kv in source) {
                output[kv.Key] = DenormalizeValue(kv.Value);
            }
            return output;
        }

        /// <summary>
        /// Produces a deterministic JSON-like string: sorted keys, normalized
        /// numbers (no trailing zeros, no scientific notation for typical ranges).
        /// Used for checksum stability, NOT for transport.
        /// </summary>
        public static string NormalizeJson(IDictionary<string, object> source) {
            var sb = new System.Text.StringBuilder();
            WriteValue(sb, source);
            return sb.ToString();
        }

        // ─── Internals ───

        private static object NormalizeValue(object v) {
            switch (v) {
                case null:
                    return null;
                case IDictionary<string, object> map:
                    return ToFirestoreDict(map);
                case IEnumerable<object> list when !(v is string):
                    var result = new List<object>();
                    foreach (var item in list) result.Add(NormalizeValue(item));
                    return result;
                case float f:
                    return (double)f;          // Firestore stores Number as double
                case decimal d:
                    return (double)d;
                case int i:
                    return (long)i;            // Firestore returns long; align early
                case short s:
                    return (long)s;
                default:
                    return v;
            }
        }

        private static object DenormalizeValue(object v) {
            switch (v) {
                case null:
                    return null;
                case IReadOnlyDictionary<string, object> ro:
                    return FromFirestoreDict(ro);
                case IDictionary<string, object> map:
                    return FromFirestoreDict((IReadOnlyDictionary<string, object>)map);
                case IEnumerable<object> list when !(v is string):
                    var result = new List<object>();
                    foreach (var item in list) result.Add(DenormalizeValue(item));
                    return result;
                default:
                    return v;
            }
        }

        private static void WriteValue(System.Text.StringBuilder sb, object value) {
            switch (value) {
                case null:
                    sb.Append("null");
                    return;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    return;
                case string s:
                    WriteString(sb, s);
                    return;
                case long l:
                    sb.Append(l.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    return;
                case int i:
                    sb.Append(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    return;
                case double d:
                    sb.Append(d.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    return;
                case float f:
                    sb.Append(((double)f).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                    return;
                case IDictionary<string, object> map:
                    sb.Append('{');
                    var keys = map.Keys.ToList();
                    keys.Sort(StringComparer.Ordinal);
                    for (int i = 0; i < keys.Count; i++) {
                        if (i > 0) sb.Append(',');
                        WriteString(sb, keys[i]);
                        sb.Append(':');
                        WriteValue(sb, map[keys[i]]);
                    }
                    sb.Append('}');
                    return;
                case IEnumerable<object> list:
                    sb.Append('[');
                    bool first = true;
                    foreach (var item in list) {
                        if (!first) sb.Append(',');
                        first = false;
                        WriteValue(sb, item);
                    }
                    sb.Append(']');
                    return;
                default:
                    WriteString(sb, value.ToString());
                    return;
            }
        }

        private static void WriteString(System.Text.StringBuilder sb, string s) {
            sb.Append('"');
            foreach (var c in s) {
                switch (c) {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
