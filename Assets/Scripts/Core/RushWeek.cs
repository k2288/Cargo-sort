using System;
using System.Collections.Generic;

namespace CargoSort.Core
{
    /// <summary>
    /// Rush Week, the weekly event: winning levels gives points (1 per truck, x2 on a 3+ win streak), points
    /// unlock a 5-step reward track, and the player is ranked in a group of 30. Weeks start on Saturday
    /// (day 0 = 2000-01-01 was a Saturday). Until an online group is available, the other 29 are computer
    /// rivals (shown as such) whose scores grow through the week toward a target fixed by the week.
    /// Same rules as the web preview ("Rush Week").
    /// </summary>
    public static class RushWeek
    {
        public const int FromLevel = 10;
        public const int GroupSize = 30;

        /// <summary>Points needed for each step of the reward track.</summary>
        public static readonly int[] Track = { 40, 100, 180, 280, 400 };

        /// <summary>Coins for a final rank: 1st 500, 2nd 300, 3rd 200, 4th-10th 100.</summary>
        public static int Prize(int rank) => rank == 1 ? 500 : rank == 2 ? 300 : rank == 3 ? 200 : rank >= 1 && rank <= 10 ? 100 : 0;

        public static int WeekOf(int day) => day < 0 ? -1 - (-day - 1) / 7 : day / 7;
        public static int FirstDayOf(int week) => week * 7;
        public static int PointsForWin(int trucks, int streak) => trucks * (streak >= 3 ? 2 : 1);

        /// <summary>Rival names (English, Persian).</summary>
        public static readonly string[,] Names =
        {
            { "Sara", "سارا" }, { "Ali", "علی" }, { "Reza", "رضا" }, { "Maryam", "مریم" }, { "Amir", "امیر" },
            { "Neda", "ندا" }, { "Hamid", "حمید" }, { "Leila", "لیلا" }, { "Omid", "امید" }, { "Zahra", "زهرا" },
            { "Kian", "کیان" }, { "Nika", "نیکا" }, { "Arash", "آرش" }, { "Shirin", "شیرین" }, { "Babak", "بابک" },
            { "Mina", "مینا" }, { "Pouya", "پویا" }, { "Yasaman", "یاسمن" }, { "Dariush", "داریوش" }, { "Elnaz", "الناز" },
            { "Farhad", "فرهاد" }, { "Golnar", "گلنار" }, { "Hooman", "هومن" }, { "Parisa", "پریسا" }, { "Saman", "سامان" },
            { "Tara", "تارا" }, { "Vahid", "وحید" }, { "Roya", "رویا" }, { "Navid", "نوید" }, { "Bita", "بیتا" },
        };

        public struct Rival
        {
            public int Name;      // row in Names
            public int Target;    // score at the end of the week
            public double Speed;  // < 1 scores early in the week, > 1 late
        }

        public struct Row
        {
            public int Name;      // row in Names, or -1 for the player
            public int Points;
            public bool IsPlayer => Name < 0;
        }

        /// <summary>The 29 computer rivals of a week (the same on every device for that week).</summary>
        public static Rival[] Rivals(int week)
        {
            var rng = new Random(week * 7907 + 17);
            int n = Names.GetLength(0);
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
            var rivals = new Rival[GroupSize - 1];
            for (int i = 0; i < rivals.Length; i++)
                rivals[i] = new Rival { Name = order[i], Target = (int)Math.Round(40 + Math.Pow(rng.NextDouble(), 1.5) * 760), Speed = 0.6 + rng.NextDouble() * 0.8 };
            return rivals;
        }

        /// <summary>A rival's score when a fraction (0..1) of the week has passed.</summary>
        public static int RivalScore(Rival r, double weekFraction) =>
            (int)Math.Floor(r.Target * Math.Pow(Math.Min(1, Math.Max(0, weekFraction)), r.Speed));

        /// <summary>The group sorted by points (on a tie the player is first).</summary>
        public static List<Row> Standings(int week, int myPoints, double weekFraction)
        {
            var rows = new List<Row>();
            foreach (var r in Rivals(week)) rows.Add(new Row { Name = r.Name, Points = RivalScore(r, weekFraction) });
            rows.Add(new Row { Name = -1, Points = myPoints });
            rows.Sort((a, b) => b.Points != a.Points ? b.Points.CompareTo(a.Points) : a.IsPlayer ? -1 : b.IsPlayer ? 1 : 0);
            return rows;
        }

        public static int RankOf(List<Row> rows)
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i].IsPlayer) return i + 1;
            return rows.Count;
        }

        public static bool CanClaim(int points, int claimedMask, int step) =>
            step >= 0 && step < Track.Length && points >= Track[step] && ((claimedMask >> step) & 1) == 0;

        public static bool AnyClaimable(int points, int claimedMask)
        {
            for (int i = 0; i < Track.Length; i++) if (CanClaim(points, claimedMask, i)) return true;
            return false;
        }
    }
}
