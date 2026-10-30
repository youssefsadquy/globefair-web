using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AppFirebase
{
    /// <summary>
    /// Minimal, dependency-free JSON parser/serializer used only to move data across the
    /// WebGL C#&lt;-&gt;JavaScript bridge (Firestore documents have arbitrary field names/types,
    /// which UnityEngine.JsonUtility cannot represent - it needs a fixed, known schema).
    ///
    /// Parses to/from the same "plain object graph" shape Firestore itself uses:
    /// Dictionary&lt;string, object&gt; for objects, List&lt;object&gt; for arrays,
    /// string/double/bool/null for scalars. AOT-safe: no reflection.
    /// </summary>
    public static class AppMiniJson
    {
        public static string Serialize(object obj)
        {
            var sb = new StringBuilder();
            WriteValue(sb, obj);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case AppFieldValue.DeleteSentinel:
                    sb.Append("\"__delete__\"");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case AppTimestamp ts:
                    // Encoded as {"__ts__": <unixMillis>} so the JS side can rebuild a Firestore Timestamp/Date.
                    sb.Append("{\"__ts__\":");
                    sb.Append(ts.ToUnixMilliseconds().ToString(CultureInfo.InvariantCulture));
                    sb.Append('}');
                    break;
                case IDictionary<string, object> dict:
                    sb.Append('{');
                    bool firstEntry = true;
                    foreach (var kvp in dict)
                    {
                        if (!firstEntry) sb.Append(',');
                        firstEntry = false;
                        WriteString(sb, kvp.Key);
                        sb.Append(':');
                        WriteValue(sb, kvp.Value);
                    }
                    sb.Append('}');
                    break;
                case System.Collections.IEnumerable enumerable:
                    sb.Append('[');
                    bool firstItem = true;
                    foreach (var item in enumerable)
                    {
                        if (!firstItem) sb.Append(',');
                        firstItem = false;
                        WriteValue(sb, item);
                    }
                    sb.Append(']');
                    break;
                case float f:
                    sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture));
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                default:
                    // long, int, etc.
                    sb.Append(Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("R", CultureInfo.InvariantCulture));
                    break;
            }
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
        }

        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            int index = 0;
            return ParseValue(json, ref index);
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWhitespace(s, ref i);
            char c = s[i];
            switch (c)
            {
                case '{': return ParseObject(s, ref i);
                case '[': return ParseArray(s, ref i);
                case '"': return ParseString(s, ref i);
                case 't':
                    i += 4; // true
                    return true;
                case 'f':
                    i += 5; // false
                    return false;
                case 'n':
                    i += 4; // null
                    return null;
                default:
                    return ParseNumber(s, ref i);
            }
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var dict = new Dictionary<string, object>();
            i++; // {
            SkipWhitespace(s, ref i);
            if (s[i] == '}') { i++; return dict; }
            while (true)
            {
                SkipWhitespace(s, ref i);
                string key = ParseString(s, ref i);
                SkipWhitespace(s, ref i);
                i++; // :
                object value = ParseValue(s, ref i);

                // Reconstruct our timestamp encoding {"__ts__": millis} back into AppTimestamp.
                if (value is Dictionary<string, object> maybeTs && maybeTs.Count == 1 && maybeTs.TryGetValue("__ts__", out object msVal))
                {
                    value = AppTimestamp.FromUnixMilliseconds(Convert.ToDouble(msVal, CultureInfo.InvariantCulture));
                }

                dict[key] = value;
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == '}') { i++; break; }
            }
            return dict;
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var list = new List<object>();
            i++; // [
            SkipWhitespace(s, ref i);
            if (s[i] == ']') { i++; return list; }
            while (true)
            {
                object value = ParseValue(s, ref i);
                list.Add(value);
                SkipWhitespace(s, ref i);
                if (s[i] == ',') { i++; continue; }
                if (s[i] == ']') { i++; break; }
            }
            return list;
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (s[i] != '"')
            {
                char c = s[i];
                if (c == '\\')
                {
                    i++;
                    char esc = s[i];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            string hex = s.Substring(i + 1, 4);
                            sb.Append((char)Convert.ToInt32(hex, 16));
                            i += 4;
                            break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
                i++;
            }
            i++; // closing quote
            return sb.ToString();
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
            {
                i++;
            }
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        private static void SkipWhitespace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        public static Dictionary<string, object> AsDict(object o) => o as Dictionary<string, object> ?? new Dictionary<string, object>();
        public static List<object> AsList(object o) => o as List<object> ?? new List<object>();
    }
}
