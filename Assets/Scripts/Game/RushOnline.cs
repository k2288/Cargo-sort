using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace CargoSort.Game
{
    /// <summary>
    /// Online Rush Week groups (the server in /server). Leave ServerUrl empty to play with computer rivals
    /// only. When the server can't be reached the game falls back to its own computer rivals, so it never
    /// blocks play.
    /// </summary>
    public static class RushOnline
    {
        /// <summary>Your server's address, e.g. "https://cargo.example.ir" (no slash at the end). Empty = offline.</summary>
        public const string ServerUrl = "http://nexo.ir:8070";

        public static bool Enabled => !string.IsNullOrEmpty(ServerUrl);

        [Serializable] private class JoinRequest { public string name; public string lang; }
        [Serializable] private class JoinAnswer { public string token; public string name; }
        [Serializable] private class ScoreRequest { public string token; public int week; public int points; public string lang; }
        [Serializable] private class NameRequest { public string token; public string name; }
        [Serializable] private class NameAnswer { public string name; }
        [Serializable] private class ResultAnswer { public int week; public int rank; public int points; }

        [Serializable] public class Row { public string name; public int points; public bool me; public bool bot; }
        [Serializable] public class Group { public int week; public long endsInSec; public int rank; public int points; public Row[] rows; }

        /// <summary>The player's group this week, from the last successful sync (null = use computer rivals).</summary>
        public static Group Current { get; private set; }
        public static event Action Changed;
        private static bool syncing;

        private static string Token
        {
            get => PlayerPrefs.GetString("rush_token", "");
            set { PlayerPrefs.SetString("rush_token", value); PlayerPrefs.Save(); }
        }

        /// <summary>The player's name on the leaderboard ("" until the first sync).</summary>
        public static string Name
        {
            get => PlayerPrefs.GetString("rush_name", "");
            private set { PlayerPrefs.SetString("rush_name", value ?? ""); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// Changes the leaderboard name. done(true) when the server accepted it; done(false) when the name is not allowed
        /// (2-16 letters, digits, spaces, _ or -, no blocked words) or the server can't be reached.
        /// </summary>
        public static IEnumerator Rename(string name, Action<bool> done)
        {
            if (!Enabled) { done(false); yield break; }
            bool ok = false;
            if (!string.IsNullOrEmpty(Token))
            {
                yield return Post("/v1/name", JsonUtility.ToJson(new NameRequest { token = Token, name = name }), (code, text) =>
                {
                    if (code == 200) { Name = JsonUtility.FromJson<NameAnswer>(text).name; ok = true; }
                    else if (code == 401) Token = ""; // the server forgot us: join again below with the new name
                });
            }
            if (!ok && string.IsNullOrEmpty(Token))
            {
                yield return Post("/v1/join", JsonUtility.ToJson(new JoinRequest { name = name, lang = Progress.Language }), (code, text) =>
                {
                    if (code != 200) return;
                    var j = JsonUtility.FromJson<JoinAnswer>(text);
                    if (j == null || string.IsNullOrEmpty(j.token)) return;
                    Token = j.token;
                    Name = j.name;
                    ok = true;
                });
            }
            if (ok)
            {
                // Show the new name at once; the next sync confirms it.
                if (Current?.rows != null) foreach (var row in Current.rows) if (row.me) row.name = Name;
                Changed?.Invoke();
            }
            done(ok);
        }

        public static bool HasGroup(int week) => Current != null && Current.week == week && Current.rows != null && Current.rows.Length > 0;

        /// <summary>Sends this week's points and reads the group. Safe to call often; one sync runs at a time.</summary>
        public static IEnumerator Sync(int week, int points)
        {
            if (!Enabled || syncing) yield break;
            syncing = true;
            try
            {
                if (string.IsNullOrEmpty(Token))
                {
                    string joined = null, joinedName = "";
                    yield return Post("/v1/join", JsonUtility.ToJson(new JoinRequest { name = "", lang = Progress.Language }), (code, text) =>
                    {
                        if (code != 200) return;
                        var answer = JsonUtility.FromJson<JoinAnswer>(text);
                        joined = answer.token;
                        joinedName = answer.name;
                    });
                    if (string.IsNullOrEmpty(joined)) yield break;
                    Token = joined;
                    Name = joinedName;
                }
                var body = JsonUtility.ToJson(new ScoreRequest { token = Token, week = week, points = points, lang = Progress.Language });
                yield return Post("/v1/score", body, (code, text) =>
                {
                    if (code == 200)
                    {
                        var g = JsonUtility.FromJson<Group>(text);
                        if (g != null && g.week == week)
                        {
                            Current = g;
                            foreach (var row in g.rows) if (row.me && !string.IsNullOrEmpty(row.name)) Name = row.name;
                            Progress.RushOnlineWeek = week;
                        }
                    }
                    else if (code == 401) Token = ""; // the server forgot us: join again next time
                });
            }
            finally
            {
                syncing = false;
                Changed?.Invoke();
            }
        }

        /// <summary>Final rank in a finished week; done(-1) when the server can't answer.</summary>
        public static IEnumerator FetchResult(int week, Action<int> done)
        {
            if (!Enabled || string.IsNullOrEmpty(Token)) { done(-1); yield break; }
            using (var req = UnityWebRequest.Get($"{ServerUrl}/v1/result?token={UnityWebRequest.EscapeURL(Token)}&week={week}"))
            {
                req.timeout = 10;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success && req.responseCode == 200)
                {
                    var r = JsonUtility.FromJson<ResultAnswer>(req.downloadHandler.text);
                    done(r != null && r.rank > 0 ? r.rank : -1);
                }
                else done(-1);
            }
        }

        private static IEnumerator Post(string path, string json, Action<long, string> done)
        {
            using (var req = new UnityWebRequest(ServerUrl + path, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 10;
                yield return req.SendWebRequest();
                bool reached = req.result == UnityWebRequest.Result.Success || req.result == UnityWebRequest.Result.ProtocolError;
                done(reached ? req.responseCode : 0, req.downloadHandler?.text ?? "");
            }
        }
    }
}
