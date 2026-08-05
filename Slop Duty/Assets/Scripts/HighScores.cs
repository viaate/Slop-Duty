using UnityEngine;

// Local records, kept in PlayerPrefs. Survives closing the game, including in a WebGL
// build on itch.io, where PlayerPrefs lives in the browser's IndexedDB per domain.
public static class HighScores
{
    private const string DaysKey = "SlopDuty.BestDays";
    private const string ServedKey = "SlopDuty.BestServed";
    private const string ScoreKey = "SlopDuty.BestScore";
    private const string NameKey = "SlopDuty.PlayerName";
    private const string TutorialKey = "SlopDuty.TutorialDone";

    // Set once the player has actually finished a Sunday, not merely started one. Quitting
    // halfway through the training shift should not count as having been taught.
    public static bool TutorialDone
    {
        get => PlayerPrefs.GetInt(TutorialKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(TutorialKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static int BestDays => PlayerPrefs.GetInt(DaysKey, 0);
    public static int BestServed => PlayerPrefs.GetInt(ServedKey, 0);
    public static int BestScore => PlayerPrefs.GetInt(ScoreKey, 0);

    public static string PlayerName
    {
        get => PlayerPrefs.GetString(NameKey, "");
        set
        {
            PlayerPrefs.SetString(NameKey, value);
            PlayerPrefs.Save();
        }
    }

    // Days cleared dominates, kids served breaks ties. A long careful shift should beat
    // a short frantic one, but two players who reach Friday are still separated by how
    // many kids they actually got through.
    public static int ScoreOf(RunStats s) => (s.daysCleared * 1000) + s.served;

    // True if this run set a new record.
    public static bool Submit(RunStats s)
    {
        int score = ScoreOf(s);
        if (score <= BestScore) return false;

        PlayerPrefs.SetInt(ScoreKey, score);
        PlayerPrefs.SetInt(DaysKey, s.daysCleared);
        PlayerPrefs.SetInt(ServedKey, s.served);
        PlayerPrefs.Save();

        return true;
    }

    public static void Clear()
    {
        PlayerPrefs.DeleteKey(ScoreKey);
        PlayerPrefs.DeleteKey(DaysKey);
        PlayerPrefs.DeleteKey(ServedKey);
        PlayerPrefs.Save();
    }
}
