using System;

namespace Next.Extentions
{
    public static class DateAndTime
    {
        /// <summary>
        /// Converts DateTime to Unix timestamp (seconds since 1970).
        /// </summary>
        /// <example>DateTime.Now.ToUnixTime() → 1735689420</example>
        public static long ToUnixTime(this DateTime dt)
            => ((DateTimeOffset)dt).ToUnixTimeSeconds();

        /// <summary>
        /// Returns midnight (00:00:00) of the same day.
        /// </summary>
        public static DateTime StartOfDay(this DateTime dt) => dt.Date;

        /// <summary>
        /// Returns "3 days ago", "just now", "2 months ago", etc.
        /// Perfect for chat apps, logs, notifications.
        /// </summary>
        /// <example>message.SentAt.Ago() → "5 minutes ago"</example>
        public static string Ago(this DateTime dt)
        {
            var span = DateTime.Now - dt;
            if (span.TotalDays > 365) return $"{span.TotalDays / 365:F0} years ago";
            if (span.TotalDays > 30) return $"{span.TotalDays / 30:F0} months ago";
            if (span.TotalDays > 7) return $"{span.TotalDays / 7:F0} weeks ago";
            if (span.TotalDays >= 1) return span.TotalDays < 2 ? "yesterday" : $"{span.TotalDays:F0} days ago";
            if (span.TotalHours >= 1) return $"{span.TotalHours:F0} hours ago";
            if (span.TotalMinutes >= 1) return $"{span.TotalMinutes:F0} minutes ago";
            return span.TotalSeconds < 10 ? "just now" : $"{span.TotalSeconds:F0} seconds ago";
        }
    }
}
