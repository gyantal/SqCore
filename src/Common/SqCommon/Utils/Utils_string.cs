using System;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace SqCommon
{

    public static partial class Utils
    {
        public static bool IsDigit(char p_char)
        {
            return (uint)(p_char - '0') <= 9u;
        }

        public static string TruncateLongString(this string str, int maxLengthAllowed)
        {
            if (string.IsNullOrEmpty(str) || str.Length <= maxLengthAllowed)
                return str;
            // add "..." at the end only if it was truncated
      
            return str.Substring(0, maxLengthAllowed - "...".Length) + "...";
        }

        public static string[] SplitStringByCommaWithCharArray(this string str)
        {
            return str.Split(new char[] {','}, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string[] SplitStringByCommaWithRegex(this string str)
        {
            return Regex.Split(str, @"(,\s)+");
        }

        public static string ToStringWithShortenedStackTrace(this string s, int p_maxLength)
        {
            if (s.Length <= p_maxLength)
                return s;
            else
                return s.Substring(0, p_maxLength) + "...";
        }
        public static string ToStringWithShortenedStackTrace(this Exception e, int p_maxLength)
        {
            string s = (e == null ? null : e.ToString()) ?? String.Empty;
            if (s.Length <= p_maxLength)
                return s;
            else
                return s.Substring(0, p_maxLength) + "...";
        }

        public static string FormatInvCult(this string p_fmt, params object[] p_args)
        {
            if (p_fmt == null || p_args == null || p_args.Length == 0)
                return p_fmt ?? String.Empty;
            return String.Format(InvCult, p_fmt, p_args);
        }

        static JsonSerializerOptions g_camelJsonSerializeOpt = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        // in Javascript, class field members should use CamelCase: https://www.robinwieruch.de/javascript-naming-conventions  "AnyParam" turns to "anyParam"
        public static string CamelCaseSerialize<TValue>(TValue obj)
        {
            return JsonSerializer.Serialize(obj, g_camelJsonSerializeOpt);
        }
    }

}