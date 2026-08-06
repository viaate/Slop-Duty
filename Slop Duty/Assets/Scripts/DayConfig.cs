using UnityEngine;

[System.Serializable]
public struct DayConfig
{
    public float arrivalInterval;
    public float patience;
    public int slopColors;
    public float reward;
    public float penalty;
    public int quota;        // kids to resolve before the day rolls over

    // Kids never run out of patience and never walk off. Sunday only.
    public bool endlessPatience;

    // Difficulty moves on three levers only, exactly as the brief says: how fast kids
    // arrive, how long they wait, and how many colors are on the counter.
    //
    // The number that decides whether the game can be lost at all is reward minus
    // arrival, which is the clock a PERFECT player nets per kid. While it is positive,
    // skill buys time forever and no fail state can trigger. These numbers cross to
    // negative on day 3, so days 1 and 2 are the only forgiving ones.
    //
    //   day 1  +0.20     day 4  -0.10     day 7  -0.40
    //   day 2  +0.10     day 5  -0.20     day 9  -0.60
    //   day 3   0.00     day 6  -0.30     day 11 -0.40 (both levers on their floors)
    public static DayConfig For(int day)
    {
        if (day <= 0) return Sunday;

        int d = day - 1;

        return new DayConfig
        {
            arrivalInterval = Mathf.Max(1.2f, 3.20f - (0.20f * d)),

            // Flat and short, with Monday doubled as a gentle landing off Sunday.
            //
            // A kid can be served from the moment they spawn, so the real window is the
            // walk in plus this, and in testing you could clear everyone before they even
            // stopped walking. Long patience was doing nothing.
            //
            // Patience is no longer a difficulty lever either. The floor does that job now,
            // squeezing this number down as the mess grows.
            patience = day == 1 ? 3.0f : 1.5f,

            slopColors = Mathf.Min(10, 3 + Mathf.FloorToInt(d * 0.7f)),
            reward = Mathf.Max(0.8f, 3.40f - (0.30f * d)),

            // Lowered from 8.0. Punishment weight moved off the clock and onto the floor,
            // so mistakes cost you tempo rather than instantly costing you the run.
            penalty = Mathf.Max(4.0f, 5.00f - (0.10f * d)),

            quota = 8,
        };
    }

    private static readonly string[] Weekdays =
    {
        "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY",
    };

    // Day 0 is the Sunday training shift. After that it is school days only, five to a
    // week, so the run reads as term time rather than a number going up.
    public static string NameFor(int day)
    {
        if (day <= 0) return "SUNDAY";
        return Weekdays[(day - 1) % Weekdays.Length];
    }

    public static int WeekFor(int day)
    {
        if (day <= 0) return 1;
        return ((day - 1) / Weekdays.Length) + 1;
    }

    // The tutorial shift. No clock, no patience, two colors.
    //
    // Sunday cannot be failed and cannot time out. GameManager never starts the clock on
    // day 0, and endlessPatience stops the countdown over each kid's head, so the shift
    // sits there indefinitely: you can read every tooltip twice, walk away, come back, and
    // nothing will have moved. The only way off Sunday is serving the quota, which is a
    // choice the player makes rather than one the clock makes for them.
    public static readonly DayConfig Sunday = new DayConfig
    {
        arrivalInterval = 4.5f,

        // Never actually counts down, see endlessPatience. Kept as a real number so the
        // bar over their head has something sane to draw and so anything reading patience
        // for other reasons does not have to special case day 0.
        patience = 20f,
        endlessPatience = true,

        slopColors = 2,
        reward = 0f,
        penalty = 0f,
        quota = 6,
    };
}
