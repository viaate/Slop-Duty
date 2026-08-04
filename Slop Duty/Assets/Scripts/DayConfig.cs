using UnityEngine;

[System.Serializable]
public struct DayConfig
{
    public float arrivalInterval;
    public float patience;
    public int   slopColors;
    public float reward;
    public float penalty;
    public int   quota;        // kids to resolve before the day rolls over

    public static DayConfig For(int day)
    {
        if (day <= 0) return Sunday;

        int d = day - 1;

        return new DayConfig
        {
            arrivalInterval = Mathf.Max(1.5f,  4.6f  - 0.30f * d),
            patience        = Mathf.Max(4.5f, 15.0f  - 0.85f * d),
            slopColors      = Mathf.Min(10,    3     + d / 2),
            reward          = Mathf.Max(1.2f,  5.0f  - 0.38f * d),
            penalty         = Mathf.Max(7.2f, 10.0f  - 0.28f * d),
            quota           = 10,
        };
    }

    // The tutorial shift. No clock, generous patience, two colours.
    public static readonly DayConfig Sunday = new DayConfig
    {
        arrivalInterval = 4.8f,
        patience        = 26f,
        slopColors      = 2,
        reward          = 0f,
        penalty         = 0f,
        quota           = 7,
    };
}