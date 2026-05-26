using System;
using System.Security.Cryptography;
using System.Text;

namespace ArcherStudio.SDK.Firestore {

    /// <summary>
    /// Stable SHA256 over normalized JSON. Used by MigrationRunner to verify that
    /// cloud read-back matches the local snapshot we just uploaded.
    ///
    /// IMPORTANT: caller MUST pass already-normalized JSON (sorted keys, consistent
    /// number formatting). Use NormalizeJson() to produce it.
    /// </summary>
    public static class ChecksumHelper {

        public static string Sha256Hex(string normalizedJson) {
            if (string.IsNullOrEmpty(normalizedJson)) return string.Empty;
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(normalizedJson));
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// Minimal text normalization: strip BOM, normalize line endings, trim. For
        /// shape normalization (key sort, type coercion) see PolymorphicJsonConverter.NormalizeJson.
        /// </summary>
        public static string PreNormalize(string raw) {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            if (raw.Length > 0 && raw[0] == '﻿') raw = raw.Substring(1);
            return raw.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        }
    }
}
