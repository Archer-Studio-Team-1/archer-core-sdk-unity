using System;
#if ARCHER_BADGE_USE_ZSTRING
using Cysharp.Text;
#else
using System.Text;
#endif

namespace ArcherStudio
{
    /// <summary>
    /// String builder abstraction used internally by the BadgeSystem SDK.
    /// When the symbol <c>ARCHER_BADGE_USE_ZSTRING</c> is defined (auto-injected via
    /// asmdef <c>versionDefines</c> when the <c>com.cysharp.zstring</c> package is
    /// present in the project), it wraps <see cref="Utf16ValueStringBuilder"/> for
    /// allocation-free string building. Otherwise it falls back to
    /// <see cref="System.Text.StringBuilder"/> so the SDK still compiles and runs
    /// without ZString.
    /// </summary>
    public struct BadgeStringBuilder : IDisposable
    {
#if ARCHER_BADGE_USE_ZSTRING
        private Utf16ValueStringBuilder _inner;
#else
        private StringBuilder _inner;
#endif

        public static BadgeStringBuilder Create()
        {
            return new BadgeStringBuilder
            {
#if ARCHER_BADGE_USE_ZSTRING
                _inner = ZString.CreateStringBuilder()
#else
                _inner = new StringBuilder()
#endif
            };
        }

        public int Length => _inner.Length;

        public void Append(string value) => _inner.Append(value);

        public void Append(char value) => _inner.Append(value);

        public void Append(ReadOnlySpan<char> value) => _inner.Append(value);

        public void Remove(int startIndex, int length) => _inner.Remove(startIndex, length);

        public override string ToString() => _inner.ToString();

        public void Dispose()
        {
#if ARCHER_BADGE_USE_ZSTRING
            _inner.Dispose();
#endif
        }
    }

    internal static class BadgeStringHelper
    {
        public static string Concat(string a, char separator, string b)
        {
#if ARCHER_BADGE_USE_ZSTRING
            return ZString.Concat(a, separator, b);
#else
            return string.Concat(a, separator.ToString(), b);
#endif
        }
    }
}
