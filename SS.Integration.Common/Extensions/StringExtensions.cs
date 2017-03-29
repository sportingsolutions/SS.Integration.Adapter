using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SS.Integration.Common.Extensions
{
    public static class StringExtensions
    {
        public static string UpperCaseFirst(this string s)
        {
            if (String.IsNullOrEmpty(s))
                return String.Empty;
            if (s.Length > 1)
                return char.ToUpper(s[0]) + s.Substring(1);
            return s.ToUpper();
        }
        public static string[] Split(this string str, string s)
        {
            return str.Split(new[] { s }, StringSplitOptions.None);
        }

        public static bool IsNullOrWhiteSpace(this string str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        public static bool IsAllNullOrWhiteSpace(this string[] str)
        {
            return str.All(x => x.IsNullOrWhiteSpace());
        }
        public static bool IsAnyNullOrWhiteSpace(this string[] str)
        {
            return str.Any(x => x.IsNullOrWhiteSpace());
        }

        public static string Simply(this string str)
        {
            return str?.Where(char.IsLetterOrDigit).Concat().ToLower();
        }

        public static string TakeAfter(this string source, string substring)
        {
            if (source == null || substring == null)
                return null;
            var ix = source.IndexOf(substring);
            if (ix != -1)
                return source.Substring(ix + substring.Length);
            return null;
        }

        public static string TakeBefore(this string source, string substring)
        {
            if (source == null || substring == null)
                return null;
            var ix = source.IndexOf(substring);
            if (ix != -1)
                return source.Substring(0, ix);
            return null;
        }

        public static string RemoveMultipleWhiteSpaсes(this string str)
        {
            return str.Replace("  ", " ");
        }

        public static string Concat(this IEnumerable<char> chars)
        {
            return string.Concat(chars);
        }

    }
}
