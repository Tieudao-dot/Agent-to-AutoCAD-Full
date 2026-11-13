using System;

namespace ClaudeCAD.Plugin.Core
{
    /// <summary>
    /// BẢN THỬ NỘI BỘ (branch full/ban-thu-noi-bo only, decided 13/11/2026; never merged anywhere): an installer for the
    /// owner alone, to try 2D + 3D in AutoCAD before the Full edition has its own activation service. The plugin built
    /// with the compile-time constant BAN_THU_NOI_BO skips activation, opens every tool (plan Full), never calls an
    /// activation service or the update link, and stops opening the assistant from 00:00 on 11/12/2026, Vietnam time.
    ///
    /// There is deliberately no switch at run time (file, registry, environment variable): a build without the constant
    /// cannot be turned into this one. This class is plain data + pure functions so the date logic is unit-tested.
    /// </summary>
    public static class InternalBuild
    {
        /// <summary>The name people see everywhere in this build (palette title, ⚙, self-test, Windows app list).</summary>
        public const string Label = "Agent to AutoCAD Full: BẢN THỬ NỘI BỘ (hết hạn 10/12/2026)";

        /// <summary>The last day the build works (Vietnam date, dd/MM/yyyy).</summary>
        public const string LastDayText = "10/12/2026";

        /// <summary>Vietnam is UTC+7 all year (no daylight saving).</summary>
        public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

        /// <summary>00:00 on 11/12/2026 Vietnam time = 17:00 on 10/12/2026 UTC: from then on the build has expired.</summary>
        public static readonly DateTime ExpiresUtc = new DateTime(2026, 12, 11, 0, 0, 0, DateTimeKind.Utc) - VietnamOffset;

        public const string ExpiredMessage = "Bản thử nội bộ đã hết hạn ngày " + LastDayText + ". Cài bản chính thức để dùng tiếp.";

        /// <summary>
        /// The time the expiry is judged by. A clock set back (LicenseLogic.ClockRolledBack: more than a day earlier
        /// than a time this machine already showed) does not bring the build back: the latest time seen counts.
        /// Inside the day of slack the later of the two counts as well, so stepping the clock back a few hours past
        /// midnight does not help either.
        /// </summary>
        public static DateTime EffectiveUtc(DateTime utcNow, DateTime? latestSeenUtc)
        {
            var now = AsUtc(utcNow);
            if (!latestSeenUtc.HasValue) return now;
            var seen = AsUtc(latestSeenUtc.Value);
            if (LicenseLogic.ClockRolledBack(now, seen)) return seen;
            return seen > now ? seen : now;
        }

        /// <summary>True from 00:00 on 11/12/2026 Vietnam time (or when the machine already showed a time after it).</summary>
        public static bool IsExpired(DateTime utcNow, DateTime? latestSeenUtc) => EffectiveUtc(utcNow, latestSeenUtc) >= ExpiresUtc;

        /// <summary>Whole days of use left, counting today (Vietnam date): 1 on 10/12/2026, 0 once expired.</summary>
        public static int DaysLeft(DateTime utcNow, DateTime? latestSeenUtc)
        {
            var eff = EffectiveUtc(utcNow, latestSeenUtc);
            if (eff >= ExpiresUtc) return 0;
            var today = (eff + VietnamOffset).Date;
            var lastDay = (ExpiresUtc + VietnamOffset).Date.AddDays(-1);
            return (int)(lastDay - today).TotalDays + 1;
        }

        /// <summary>The line on the ⚙ / self-test "Kích hoạt" row.</summary>
        public static string StatusText(DateTime utcNow, DateTime? latestSeenUtc) =>
            IsExpired(utcNow, latestSeenUtc)
                ? "bản thử nội bộ — đã hết hạn (" + LastDayText + ")"
                : "bản thử nội bộ — bỏ qua kích hoạt, gói Full, dùng được tới hết ngày " + LastDayText + " (còn " + DaysLeft(utcNow, latestSeenUtc) + " ngày)";

        private static DateTime AsUtc(DateTime t) =>
            t.Kind == DateTimeKind.Utc ? t : t.Kind == DateTimeKind.Local ? t.ToUniversalTime() : DateTime.SpecifyKind(t, DateTimeKind.Utc);
    }
}
