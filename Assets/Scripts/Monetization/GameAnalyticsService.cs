using System.Collections.Generic;
using System.Text.RegularExpressions;
using CargoSort.Game;
using GameAnalyticsSDK;
using UnityEngine;

/// <summary>Sends the game's analytics events to GameAnalytics (keys in AdsConfig).</summary>
public sealed class GameAnalyticsService : IAnalyticsService
{
    public static void Begin()
    {
        if (Object.FindFirstObjectByType<GameAnalytics>() == null)
        {
            var go = new GameObject("GameAnalytics");
            go.AddComponent<GameAnalytics>();
        }
        GameAnalytics.Initialize();
        Analytics.Service = new GameAnalyticsService();
    }

    public void Progression(string status, string id, IDictionary<string, object> fields)
    {
        var s = status == "start" ? GAProgressionStatus.Start
              : status == "complete" ? GAProgressionStatus.Complete
              : GAProgressionStatus.Fail;
        GameAnalytics.NewProgressionEvent(s, Clean(id), Fields(fields));
    }

    public void Event(string name, IDictionary<string, object> f)
    {
        // Design event ids are "part:part:part" so the dashboard can group them.
        switch (name)
        {
            case "booster": Design($"booster:{Get(f, "type")}:{Get(f, "paid_with")}", f); break;
            case "ad": Design($"ad:{Get(f, "kind")}:{Get(f, "placement")}:{(Get(f, "completed") == "True" ? "done" : "skipped")}", f); break;
            case "purchase": Design($"purchase:{Get(f, "product")}:{(Get(f, "ok") == "True" ? "ok" : "failed")}", f); break;
            case "tutorial": Design($"tutorial:{Get(f, "step")}", f); break;
            case "daily_gift": Design($"daily_gift:{(Get(f, "doubled") == "True" ? "x2" : "x1")}", f, Number(f, "streak")); break;
            case "level_quit": Design($"level_quit:{Get(f, "level")}", f, Number(f, "trucks_shipped")); break;
            case "session_start": Design("session:start", f, Number(f, "days_since_install")); break;
            default: Design(name, f); break;
        }
    }

    private static void Design(string id, IDictionary<string, object> fields, float? value = null)
    {
        var parts = id.Split(':');
        for (int i = 0; i < parts.Length; i++) parts[i] = Clean(parts[i]);
        string eventId = string.Join(":", parts);
        if (value.HasValue) GameAnalytics.NewDesignEvent(eventId, value.Value, Fields(fields));
        else GameAnalytics.NewDesignEvent(eventId, Fields(fields));
    }

    private static string Get(IDictionary<string, object> f, string key) =>
        f != null && f.TryGetValue(key, out var v) && v != null ? v.ToString() : "none";

    private static float? Number(IDictionary<string, object> f, string key) =>
        f != null && f.TryGetValue(key, out var v) && float.TryParse(v?.ToString(), out var n) ? n : (float?)null;

    /// <summary>Custom fields may only hold numbers and strings.</summary>
    private static IDictionary<string, object> Fields(IDictionary<string, object> f)
    {
        var result = new Dictionary<string, object>();
        if (f == null) return result;
        foreach (var kv in f)
        {
            if (kv.Value is int || kv.Value is float || kv.Value is double || kv.Value is long) result[kv.Key] = kv.Value;
            else result[kv.Key] = kv.Value?.ToString() ?? "";
        }
        return result;
    }

    private static string Clean(string part)
    {
        part = Regex.Replace(part ?? "none", @"[^A-Za-z0-9\s\-_\.\(\)\!\?]", "_");
        if (part.Length == 0) part = "none";
        return part.Length > 64 ? part.Substring(0, 64) : part;
    }
}
