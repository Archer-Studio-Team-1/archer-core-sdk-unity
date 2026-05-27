// Adapted from MiniJSON by Calvin Rien (public domain / MIT)
// https://gist.github.com/darktable/1411710 — trimmed to Deserialize only.
//
// We ship our own copy so the SDK does not depend on Firebase.Functions Unity
// package (which would otherwise pull MiniJson transitively). Used solely to
// parse callable Cloud Functions HTTPS responses inside CallableHttpClient.
//
// PolymorphicJsonConverter still owns serialization; this file is parser-only.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ArcherStudio.SDK.Firestore {

    internal static class MiniJson {

        public static object Deserialize(string json) {
            if (json == null) return null;
            return Parser.Parse(json);
        }

        private sealed class Parser : IDisposable {

            private const string WordBreak = "{}[],:\"";

            private StringReader _json;

            private Parser(string jsonString) { _json = new StringReader(jsonString); }

            public static object Parse(string jsonString) {
                using (var parser = new Parser(jsonString)) {
                    return parser.ParseValue();
                }
            }

            public void Dispose() {
                _json?.Dispose();
                _json = null;
            }

            private Dictionary<string, object> ParseObject() {
                var table = new Dictionary<string, object>();
                _json.Read(); // {
                while (true) {
                    switch (NextToken) {
                        case Token.None: return null;
                        case Token.Comma: continue;
                        case Token.CurlyClose: return table;
                        default:
                            string name = ParseString();
                            if (name == null) return null;
                            if (NextToken != Token.Colon) return null;
                            _json.Read(); // :
                            table[name] = ParseValue();
                            break;
                    }
                }
            }

            private List<object> ParseArray() {
                var array = new List<object>();
                _json.Read(); // [
                bool parsing = true;
                while (parsing) {
                    Token nextToken = NextToken;
                    switch (nextToken) {
                        case Token.None: return null;
                        case Token.Comma: continue;
                        case Token.SquaredClose: parsing = false; break;
                        default: array.Add(ParseByToken(nextToken)); break;
                    }
                }
                return array;
            }

            private object ParseValue() => ParseByToken(NextToken);

            private object ParseByToken(Token token) {
                switch (token) {
                    case Token.String: return ParseString();
                    case Token.Number: return ParseNumber();
                    case Token.CurlyOpen: return ParseObject();
                    case Token.SquaredOpen: return ParseArray();
                    case Token.True: return true;
                    case Token.False: return false;
                    case Token.Null: return null;
                    default: return null;
                }
            }

            private string ParseString() {
                var s = new StringBuilder();
                _json.Read(); // opening quote
                bool parsing = true;
                while (parsing) {
                    if (_json.Peek() == -1) break;
                    char c = NextChar;
                    switch (c) {
                        case '"': parsing = false; break;
                        case '\\':
                            if (_json.Peek() == -1) { parsing = false; break; }
                            c = NextChar;
                            switch (c) {
                                case '"':
                                case '\\':
                                case '/': s.Append(c); break;
                                case 'b': s.Append('\b'); break;
                                case 'f': s.Append('\f'); break;
                                case 'n': s.Append('\n'); break;
                                case 'r': s.Append('\r'); break;
                                case 't': s.Append('\t'); break;
                                case 'u':
                                    var hex = new char[4];
                                    for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }
                            break;
                        default: s.Append(c); break;
                    }
                }
                return s.ToString();
            }

            private object ParseNumber() {
                string number = NextWord;
                if (number.IndexOf('.') == -1 && number.IndexOf('e') == -1 && number.IndexOf('E') == -1) {
                    if (long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return l;
                }
                if (double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
                return 0;
            }

            private void EatWhitespace() {
                while (_json.Peek() != -1 && char.IsWhiteSpace((char)_json.Peek())) _json.Read();
            }

            private char NextChar => Convert.ToChar(_json.Read());

            private string NextWord {
                get {
                    var word = new StringBuilder();
                    while (_json.Peek() != -1 && !IsWordBreak((char)_json.Peek())) word.Append(NextChar);
                    return word.ToString();
                }
            }

            private static bool IsWordBreak(char c) => char.IsWhiteSpace(c) || WordBreak.IndexOf(c) != -1;

            private Token NextToken {
                get {
                    EatWhitespace();
                    if (_json.Peek() == -1) return Token.None;
                    switch ((char)_json.Peek()) {
                        case '{': return Token.CurlyOpen;
                        case '}': _json.Read(); return Token.CurlyClose;
                        case '[': return Token.SquaredOpen;
                        case ']': _json.Read(); return Token.SquaredClose;
                        case ',': _json.Read(); return Token.Comma;
                        case '"': return Token.String;
                        case ':': return Token.Colon;
                        case '0': case '1': case '2': case '3': case '4':
                        case '5': case '6': case '7': case '8': case '9':
                        case '-': return Token.Number;
                    }
                    switch (NextWord) {
                        case "false": return Token.False;
                        case "true": return Token.True;
                        case "null": return Token.Null;
                    }
                    return Token.None;
                }
            }

            private enum Token { None, CurlyOpen, CurlyClose, SquaredOpen, SquaredClose, Colon, Comma, String, Number, True, False, Null }
        }
    }
}
