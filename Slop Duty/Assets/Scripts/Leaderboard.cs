using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

// Global high scores through dreamlo, chosen because it needs no SDK, no accounts and no
// build-time setup: two codes from dreamlo.com and it works. That also means it has no
// security whatsoever, since anyone who reads your build can post any score they like.
// Fine for a class project or a jam, not fine for anything competitive.
//
// LEAVE THE CODES EMPTY AND NOTHING HAPPENS. The game runs exactly as before with local
// scores only, so this cannot break the build if you never get round to it.
public class Leaderboard : MonoBehaviour
{
    public static Leaderboard Instance;

    [Serializable]
    public struct Entry
    {
        public string name;
        public int score;
        public int days;
    }

    [Header("From dreamlo.com. Leave empty to disable global scores entirely.")]
    [SerializeField] private string publicCode = "";
    [SerializeField] private string privateCode = "";
    [SerializeField, Range(3, 25)] private int topCount = 10;

    [Tooltip("Logs every request and response to the console. Leave on until scores are " +
             "confirmed working, since a silent failure here looks identical to an empty board.")]
    [SerializeField] private bool logRequests = true;

    [Tooltip("dreamlo boards are HTTP only unless SSL is enabled on your account. Turning " +
             "this off makes it work in the editor, but a WebGL build on itch.io is served " +
             "over HTTPS and browsers block plain HTTP requests from an HTTPS page, so it " +
             "will silently fail once published. Test tool only.")]
    [SerializeField] private bool useHttps = true;

    private string Scheme => useHttps ? "https" : "http";

    public bool Configured => !string.IsNullOrEmpty(publicCode) && !string.IsNullOrEmpty(privateCode);

    public bool Busy { get; private set; }
    public string LastError { get; private set; }
    public List<Entry> Top { get; private set; } = new List<Entry>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SubmitAndRefresh(string playerName, RunStats stats)
    {
        if (!Configured) return;

        // Screened before the request, not after, because dreamlo has no moderation and
        // no delete-from-the-game path. Once a name is on that board it is there until
        // somebody removes it by hand through the private URL.
        string offending = ProfanityFilter.Matched(playerName);
        if (offending != null)
        {
            LastError = "PICK A DIFFERENT NAME";
            Busy = false;

            // The specific word stays out of the player-facing message on purpose,
            // since naming it just tells them what to spell around.
            if (logRequests) Debug.Log($"[Leaderboard] name rejected by filter: '{offending}'");
            return;
        }

        StartCoroutine(SubmitRoutine(playerName, HighScores.ScoreOf(stats), stats.daysCleared));
    }

    public void Refresh()
    {
        if (!Configured) return;

        StartCoroutine(FetchRoutine());
    }

    private IEnumerator SubmitRoutine(string playerName, int score, int days)
    {
        Busy = true;
        LastError = null;

        string safeName = UnityWebRequest.EscapeURL(Sanitize(playerName));

        // score goes in the score slot, days cleared rides along in the free text field
        // so the board can show "reached Thursday" next to the number.
        string url = $"{Scheme}://dreamlo.com/lb/{privateCode}/add/{safeName}/{score}/0/{days}";

        if (logRequests)
            Debug.Log($"[Leaderboard] submitting '{safeName}' score {score}, days {days}");

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                LastError = $"SCOREBOARD OFFLINE\n{req.responseCode} {req.error}";
                Debug.LogWarning($"[Leaderboard] submit failed: {LastError}", this);
                Busy = false;
                yield break;
            }

            // dreamlo answers a successful add with an empty body and reports real problems
            // in the body with a 200, so the status code alone is not enough to trust.
            string reply = req.downloadHandler.text;

            if (logRequests)
                Debug.Log($"[Leaderboard] submit response body: '{reply}'");

            if (IsServerError(reply))
            {
                LastError = Describe(reply);
                Debug.LogWarning($"[Leaderboard] submit rejected: {reply}", this);
                Busy = false;
                yield break;
            }
        }

        yield return FetchRoutine();
    }

    private IEnumerator FetchRoutine()
    {
        Busy = true;
        LastError = null;

        // Pipe format rather than JSON on purpose. dreamlo's JSON returns a single object
        // instead of an array when there is exactly one entry, which JsonUtility cannot
        // parse, so a brand new board would break the moment it had its first score.
        string url = $"{Scheme}://dreamlo.com/lb/{publicCode}/pipe/0/{topCount}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                LastError = $"SCOREBOARD OFFLINE\n{req.responseCode} {req.error}";
                Debug.LogWarning($"[Leaderboard] fetch failed: {LastError}", this);
                Busy = false;
                yield break;
            }

            string body = req.downloadHandler.text;

            if (logRequests)
                Debug.Log($"[Leaderboard] fetch response, {body.Length} chars:\n{body}");

            // dreamlo returns its errors as a 200 with an ERROR body, so this has to be
            // checked before parsing or a rejection reads as an empty scoreboard.
            if (IsServerError(body))
            {
                LastError = Describe(body);
                Debug.LogWarning($"[Leaderboard] fetch rejected: {body}", this);
                Busy = false;
                yield break;
            }

            Top = Parse(body);
        }

        Busy = false;
    }

    private static bool IsServerError(string body)
    {
        return !string.IsNullOrEmpty(body) && body.TrimStart().StartsWith("ERROR", System.StringComparison.OrdinalIgnoreCase);
    }

    // Turns dreamlo's own error text into something that fits on a game over screen.
    private static string Describe(string body)
    {
        if (body.IndexOf("SSL", System.StringComparison.OrdinalIgnoreCase) >= 0)
            return "SSL NOT ENABLED ON THIS BOARD";

        return $"SCOREBOARD ERROR\n{body.Trim()}";
    }

    // Each line is name|score|seconds|text|date
    private static List<Entry> Parse(string body)
    {
        List<Entry> list = new List<Entry>();
        if (string.IsNullOrWhiteSpace(body)) return list;

        string[] lines = body.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length; i++)
        {
            string[] parts = lines[i].Split('|');
            if (parts.Length < 2) continue;

            int.TryParse(parts[1], out int score);

            int days = 0;
            if (parts.Length >= 4) int.TryParse(parts[3], out days);

            list.Add(new Entry { name = parts[0], score = score, days = days });
        }

        return list;
    }

    // dreamlo splits its URLs on slashes, so a name containing one would corrupt the
    // request. Trimmed short too, since long names wreck the board layout.
    private static string Sanitize(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "ANON";

        string cleaned = raw.Replace("/", "").Replace("\\", "").Replace("|", "").Trim();
        if (cleaned.Length == 0) return "ANON";

        return cleaned.Length > 12 ? cleaned.Substring(0, 12) : cleaned;
    }
}
