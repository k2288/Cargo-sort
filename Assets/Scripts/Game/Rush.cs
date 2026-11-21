using System;
using CargoSort.Core;

namespace CargoSort.Game
{
    /// <summary>Rush Week state on this device (rules in <see cref="RushWeek"/>), and its rewards.</summary>
    public static class Rush
    {
        /// <summary>Rewards for each step of the track (same order as RushWeek.Track).</summary>
        public static readonly Item[][] TrackRewards =
        {
            new[] { Item.Coins(100) },
            new[] { Item.Of(Booster.Undo, 2), Item.Of(Booster.Slot, 1) },
            new[] { Item.Coins(250) },
            new[] { Item.Of(Booster.Crane, 2), Item.Of(Booster.Magnet, 1) },
            new[] { Item.Coins(600), Item.Of(Booster.Crane, 2), Item.Of(Booster.Magnet, 2), Item.Of(Booster.Shuffle, 2) },
        };

        public static bool Open => Progress.Level >= RushWeek.FromLevel;
        public static int Week => RushWeek.WeekOf(Progress.Today);

        private static DateTime WeekStart(int week) => new DateTime(2000, 1, 1).AddDays(RushWeek.FirstDayOf(week));

        public static TimeSpan Left
        {
            get
            {
                var left = WeekStart(Week + 1) - DateTime.Now;
                return left < TimeSpan.Zero ? TimeSpan.Zero : left;
            }
        }

        public static double WeekFraction => 1 - Left.TotalDays / 7.0;

        /// <summary>"2d 5h" when a day or more is left, otherwise hh:mm:ss. Not shaped: pass it into Loc.T.</summary>
        public static string LeftRaw
        {
            get
            {
                var left = Left;
                if (left.TotalHours < 24) return Economy.Clock(left);
                string text = string.Format(Loc.Raw("days_hours"), (int)left.TotalDays, left.Hours);
                return Loc.Persian ? Loc.PersianDigits(text) : text;
            }
        }

        /// <summary>LeftRaw ready to show on its own.</summary>
        public static string LeftText => Loc.Persian ? CargoSort.Core.PersianShaper.Fix(LeftRaw) : LeftRaw;

        /// <summary>A new week resets points and remembers last week's rank and prize (paid on the home screen).</summary>
        public static void Rollover()
        {
            int week = Week, saved = Progress.RushWeek;
            if (saved == week) return;
            if (saved >= 0 && Progress.RushPoints > 0)
            {
                int rank = RushWeek.RankOf(RushWeek.Standings(saved, Progress.RushPoints, 1));
                Progress.RushResultRank = rank;
                Progress.RushResultPrize = RushWeek.Prize(rank);
            }
            Progress.RushWeek = week;
            Progress.RushPoints = 0;
            Progress.RushClaimed = 0;
        }

        public static bool Claimable => Open && RushWeek.AnyClaimable(Progress.RushPoints, Progress.RushClaimed);
    }
}
