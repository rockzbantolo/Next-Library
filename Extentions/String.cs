using System;
using System.Text;

namespace Next.Extentions
{
    public static class String
    {
        /// <summary>
        /// Checks if string is null, empty, or whitespace.
        /// </summary>
        /// <param name="s">The string to check</param>
        /// <returns>true if empty or whitespace</returns>
        /// <example>
        /// <code>
        /// if (name.IsEmpty()) { /* handle empty */ }
        /// </code>
        /// </example>
        public static bool IsEmpty(this string s) => string.IsNullOrWhiteSpace(s);

        /// <summary>
        /// Opposite of IsEmpty() — cleaner than !string.IsNullOrWhiteSpace()
        /// </summary>
        public static bool NotEmpty(this string s) => !string.IsNullOrWhiteSpace(s);

        /// <summary>
        /// Truncates string to max length with ellipsis.
        /// </summary>
        /// <param name="s">Source string</param>
        /// <param name="maxLength">Maximum length before truncation</param>
        /// <param name="ellipsis">Text to append (default: "...")</param>
        /// <example>
        /// <code>
        /// lblTitle.Text = longText.Truncate(50); // "Very long title that goes on and..."
        /// </code>
        /// </example>
        public static string Truncate(this string s, int maxLength, string ellipsis = "...")
            => s == null ? "" : (s.Length <= maxLength ? s : s.Substring(0, maxLength) + ellipsis);

        /// <summary>
        /// Converts string to Base64 (UTF-8).
        /// </summary>
        /// <example>
        /// <code>
        /// string token = userId.ToBase64(); // "MTIzNA=="
        /// </code>
        /// </example>
        public static string ToBase64(this string s, Encoding encoding = null)
            => Convert.ToBase64String((encoding ?? Encoding.UTF8).GetBytes(s ?? ""));

        /// <summary>
        /// Decodes Base64 back to string.
        /// </summary>
        public static string FromBase64(this string s, Encoding encoding = null)
            => (encoding ?? Encoding.UTF8).GetString(Convert.FromBase64String(s ?? ""));
    }
}
