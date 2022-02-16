using System;
using System.Collections.Generic;
using System.Text;
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

            return string.Concat(str.AsSpan(0, maxLengthAllowed - "...".Length), "...");
        }

        public static string[] SplitStringByCommaWithCharArray(this string str)
        {
            return str.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
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
                return string.Concat(s.AsSpan(0, p_maxLength), "...");
        }
        public static string ToStringWithShortenedStackTrace(this Exception e, int p_maxLength)
        {
            string s = e?.ToString() ?? string.Empty;
            if (s.Length <= p_maxLength)
                return s;
            else
                return string.Concat(s.AsSpan(0, p_maxLength), "...");
        }

        public static string FormatInvCult(this string p_fmt, params object[] p_args)
        {
            if (p_fmt == null || p_args == null || p_args.Length == 0)
                return p_fmt ?? string.Empty;
            return String.Format(InvCult, p_fmt, p_args);
        }

        public static int Count(this string p_str, char p_char)
        {
            // https://stackoverflow.com/questions/541954/how-would-you-count-occurrences-of-a-string-actually-a-char-within-a-string
            // foreach is faster than for, because of no boundary checking at every iteration.
            // "Just tested it with a for loop and it was actually slower than using foreach. Could be because of bounds-checking? (Time was 1.65 sec vs 2.05 on 5 mil iterations.)"
            int count = 0;
            foreach (char c in p_str)
            {
                if (c == p_char)
                    count++;
            }
            return count;
        }

        // thousands of tickers are too long on a page. Break them into new lines for ever 10-20 tickers.
        public static void AppendLongListByLine(this StringBuilder p_sb, IEnumerable<string> p_strs, string p_cellSep, int p_maxPerLine, string p_lineSep)
        {
            int i = 0;
            foreach (var s in p_strs)
            {
                if (i != 0 && i % p_maxPerLine == 0)
                    p_sb.Append(p_lineSep);

                if (i == 0)
                    p_sb.Append(s);
                else
                    p_sb.Append(p_cellSep + s); // write "," in front of cell, but only if it not the first cell
                i++;
            }
        }
    }
}