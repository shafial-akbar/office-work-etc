using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Etc.Shared.Helpers
{
    public static class DateTimeHelper
    {
        // সম্ভাব্য সব ধরনের সাধারণ ও স্পেশাল ডেট-টাইম ফরম্যাটের লিস্ট
        private static readonly string[] SupportedFormats = new[]
        {
            // --- Date with Time (24-Hour) ---
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm",
            "yyyy/MM/dd HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss.fff",
            "yyyy/MM/dd HH:mm",
            "dd-MM-yyyy HH:mm:ss",
            "dd-MM-yyyy HH:mm",
            "dd/MM/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm",
            "MM/dd/yyyy HH:mm:ss",
            "MM-dd-yyyy HH:mm:ss",

            // --- Date with Time (12-Hour AM/PM) ---
            "yyyy-MM-dd hh:mm:ss tt",
            "yyyy-MM-dd hh:mm tt",
            "yyyy/MM/dd hh:mm:ss tt",
            "yyyy/MM/dd hh:mm tt",
            "dd-MM-yyyy hh:mm:ss tt",
            "dd-MM-yyyy hh:mm tt",
            "dd/MM/yyyy hh:mm:ss tt",
            "dd/MM/yyyy hh:mm tt",
            "MM/dd/yyyy hh:mm:ss tt",
            "MM-dd-yyyy hh:mm:ss tt",

            // --- Date Only (Without Time) ---
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "dd-MM-yyyy",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "MM-dd-yyyy",
            "yyyyMMdd",

            // --- ISO 8601 Standards ---
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "yyyy-MM-ddTHH:mm:ss zzz",
            "yyyy-MM-ddTHH:mm:ss.fff zzz"
        };

        /// <summary>
        /// যেকোনো ফরম্যাটের ডেট স্ট্রিংকে 안전ভাবে DateTime-এ কনভার্ট করে।
        /// পার্স করতে না পারলে ডিফল্ট ভ্যালু (যেমন: DateTime.UtcNow) রিটার্ন করবে।
        /// </summary>
        public static DateTime ParseToDateTime(string dateStr, DateTime? fallbackDate = null)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
            {
                return fallbackDate ?? DateTime.UtcNow;
            }

            // ১. নির্দিষ্ট লিস্টের ফরম্যাটগুলো দিয়ে ParseExact চেষ্টা করা
            if (DateTime.TryParseExact(
                    dateStr.Trim(),
                    SupportedFormats,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeLocal,
                    out var parsedDateExact))
            {
                return parsedDateExact;
            }

            // ২. লিস্টের বাইরেও কোনো স্ট্যান্ডার্ড সিস্টেমেিক ফরম্যাট থাকলে তা ধরার জন্য General TryParse
            if (DateTime.TryParse(dateStr.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateGeneral))
            {
                return parsedDateGeneral;
            }

            // ৩. কনভার্ট করতে না পারলে Fallback / Default রিটার্ন করবে
            return fallbackDate ?? DateTime.UtcNow;
        }
    }
}
