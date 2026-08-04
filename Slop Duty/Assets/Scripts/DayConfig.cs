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

    // Difficulty moves on three levers only, exactly as the brief says: how fast kids
    // arrive, how long they wait, and how many colours are on the counter.
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
            patience = Mathf.Max(3.5f, 9.00f - (0.50f * d)),
            slopColors = Mathf.Min(10, 3 + Mathf.FloorToInt(d * 0.7f)),
            reward = Mathf.Max(0.8f, 3.40f - (0.30f * d)),
            penalty = Mathf.Max(6.0f, 8.00f - (0.15f * d)),
            quota = 8,
        };
    }

    // The tutorial shift. No clock, generous patience, two colours.
    public static readonly DayConfig Sunday = new DayConfig
    {
        arrivalInterval = 4.5f,
        patience = 20f,
        slopColors = 2,
        reward = 0f,
        penalty = 0f,
        quota = 6,
    };
}
