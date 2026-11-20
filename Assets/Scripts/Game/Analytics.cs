using System.Collections.Generic;
using UnityEngine;

namespace CargoSort.Game
{
    /// <summary>Where analytics events go (GameAnalytics, Metrix, ...). Plugged in from Assets/Scripts/Monetization.</summary>
    public interface IAnalyticsService
    {
        /// <summary>status: "start", "complete" or "fail"; id like "level_012" or "challenge".</summary>
        void Progression(string status, string id, IDictionary<string, object> fields);

        /// <summary>Any other event, e.g. "booster" with type and how it was paid.</summary>
        void Event(string name, IDictionary<string, object> fields);
    }

    /// <summary>Logs events to the Unity console (used when no analytics SDK is set up).</summary>
    public sealed class DebugAnalytics : IAnalyticsService
    {
        public void Progression(string status, string id, IDictionary<string, object> fields) =>
            Debug.Log("[Analytics] " + status + " " + id + Format(fields));

        public void Event(string name, IDictionary<string, object> fields) =>
            Debug.Log("[Analytics] " + name + Format(fields));

        private static string Format(IDictionary<string, object> fields)
        {
            if (fields == null || fields.Count == 0) return "";
            var parts = new List<string>();
            foreach (var kv in fields) parts.Add(kv.Key + "=" + kv.Value);
            return " {" + string.Join(", ", parts) + "}";
        }
    }

    /// <summary>
    /// The events we need to tune the game: where players quit, which levels they fail,
    /// what boosters and ads they use, and whether they come back.
    /// </summary>
    public static class Analytics
    {
        public static IAnalyticsService Service = new DebugAnalytics();

        private static int level;
        private static float startTime;
        private static int moves, boostersUsed;

        public static string LevelId(int n) => n <= 0 ? "challenge" : "level_" + n.ToString("000");

        public static void SessionStart()
        {
            int sessions = PlayerPrefs.GetInt("an_sessions", 0) + 1;
            PlayerPrefs.SetInt("an_sessions", sessions);
            int firstDay = PlayerPrefs.GetInt("an_first_day", 0);
            if (firstDay == 0) { firstDay = Progress.Today; PlayerPrefs.SetInt("an_first_day", firstDay); }
            Send("session_start", new Dictionary<string, object>
            {
                { "session", sessions },
                { "days_since_install", Progress.Today - firstDay },
                { "level", Progress.Level },
                { "language", Progress.Language },
            });
        }

        public static void LevelStart(int n)
        {
            level = n;
            startTime = Time.realtimeSinceStartup;
            moves = 0;
            boostersUsed = 0;
            string key = "an_attempts_" + LevelId(n);
            int attempt = PlayerPrefs.GetInt(key, 0) + 1;
            PlayerPrefs.SetInt(key, attempt);
            SendProgress("start", n, new Dictionary<string, object> { { "attempt", attempt } });
        }

        public static void Move() => moves++;

        public static void LevelWin(int n, int dockSize) => SendProgress("complete", n, Summary(n, dockSize));

        public static void LevelFail(int n, int shipped, int total, int dockSize)
        {
            var f = Summary(n, dockSize);
            f["trucks_shipped"] = shipped;
            f["trucks_total"] = total;
            SendProgress("fail", n, f);
        }

        /// <summary>The player restarted or left a level before finishing it.</summary>
        public static void LevelQuit(int n, int shipped, int total)
        {
            if (moves == 0) return;
            Send("level_quit", new Dictionary<string, object>
            {
                { "level", LevelId(n) }, { "moves", moves }, { "trucks_shipped", shipped }, { "trucks_total", total },
                { "seconds", Mathf.RoundToInt(Time.realtimeSinceStartup - startTime) },
            });
        }

        /// <summary>type: undo, slot, crane, magnet, shuffle, revive. paidWith: free, owned, coins, ad.</summary>
        public static void Booster(string type, string paidWith)
        {
            boostersUsed++;
            Send("booster", new Dictionary<string, object> { { "type", type }, { "paid_with", paidWith }, { "level", LevelId(level) } });
        }

        public static void Ad(string kind, string placement, bool completed) =>
            Send("ad", new Dictionary<string, object> { { "kind", kind }, { "placement", placement }, { "completed", completed }, { "level", LevelId(level) } });

        public static void Purchase(string product, bool ok) =>
            Send("purchase", new Dictionary<string, object> { { "product", product }, { "ok", ok }, { "level", LevelId(level) } });

        public static void Tutorial(string step) => Send("tutorial", new Dictionary<string, object> { { "step", step } });

        public static void DailyGift(int streak, bool doubled) =>
            Send("daily_gift", new Dictionary<string, object> { { "streak", streak }, { "doubled", doubled } });

        /// <summary>offer: starter, piggy, chest, coins_1000, ...; action: shown, bought, opened.</summary>
        public static void Offer(string offer, string action) =>
            Send("offer", new Dictionary<string, object> { { "offer", offer }, { "action", action }, { "level", LevelId(level) } });

        public static void SkinBought(string kind, string key, int price) =>
            Send("skin", new Dictionary<string, object> { { "kind", kind }, { "key", key }, { "price", price } });

        public static void Coins(string source, int amount) =>
            Send("coins", new Dictionary<string, object> { { "source", source }, { "amount", amount }, { "balance", Progress.Coins } });

        private static Dictionary<string, object> Summary(int n, int dockSize) => new Dictionary<string, object>
        {
            { "moves", moves },
            { "seconds", Mathf.RoundToInt(Time.realtimeSinceStartup - startTime) },
            { "boosters", boostersUsed },
            { "dock_size", dockSize },
            { "attempt", PlayerPrefs.GetInt("an_attempts_" + LevelId(n), 1) },
        };

        private static void SendProgress(string status, int n, IDictionary<string, object> fields)
        {
            try { Service?.Progression(status, LevelId(n), fields); }
            catch (System.Exception e) { Debug.LogWarning("[Analytics] " + e.Message); }
        }

        private static void Send(string name, IDictionary<string, object> fields)
        {
            try { Service?.Event(name, fields); }
            catch (System.Exception e) { Debug.LogWarning("[Analytics] " + e.Message); }
        }
    }
}
