using UnityEngine;

public enum BoostKind
{
    None = 0,
    BigTips,        // every serve is worth more
    SimplePalette,  // one fewer color to tell apart
    CleanFloor,     // the floor stays cleaner for longer
    Forgiving,      // mistakes cost less
    AutoSorter,     // the system screens out orders you cannot fill
    ExtraHands,     // shorter days, because somebody else is serving too
}

// A perk won off a boss, good for the rest of that week.
//
// Each one pulls a DIFFERENT lever on purpose. Two boosts that both hand you time would be
// the same boost with two names on it, and the whole point is that beating Selena feels
// unlike beating Taneim. So one touches reward, one touches the number of colors, one
// touches the mess, one touches penalties, and one removes the sorry-we're-out case
// entirely. Between them they cover every pressure the game applies.
//
// Static, and deliberately not reset by a scene load, because a week outlives a day and
// days are scene reloads. GameManager clears it when a fresh run begins.
public static class WeekBoost
{
    public static BoostKind Kind { get; private set; }
    public static int Week { get; private set; }
    public static string Owner { get; private set; } = string.Empty;

    public static void Grant(BoostKind kind, int week, string owner)
    {
        Kind = kind;
        Week = week;
        Owner = owner ?? string.Empty;
    }

    // Called when a run starts. Without it a boost won on one run would still be running on
    // the next, which is the sort of thing that gets noticed as a mystery difficulty spike.
    public static void Clear()
    {
        Kind = BoostKind.None;
        Week = 0;
        Owner = string.Empty;
    }

    public static bool ActiveOn(int day) =>
        Kind != BoostKind.None && day > 0 && DayConfig.WeekFor(day) == Week;

    // How much of a mess a mistake makes, as a multiplier. Read by PukeManager rather than
    // folded into DayConfig, because the mess is not a per-day number: it is a running total
    // that survives from one kid to the next.
    public static float MessScale(int day) =>
        ActiveOn(day) && Kind == BoostKind.CleanFloor ? 0.5f : 1f;

    // How many colors Selena's perk takes off the counter.
    //
    // Applied by SlopLogic, AFTER it clamps the day's request down to the number of pans
    // that actually exist, and deliberately not folded into DayConfig like the others.
    //
    // Folded in early it silently did nothing: the day asks for up to ten colors, the
    // counter only has eight pans, so from day eleven the request was already being clamped
    // ten to eight, and subtracting one first only made it nine to eight. The perk was live,
    // named on the HUD, and changing nothing whatsoever.
    public static int FewerColors(int day) =>
        ActiveOn(day) && Kind == BoostKind.SimplePalette ? 1 : 0;

    // Al's, and the only boost that removes a decision rather than softening a number.
    //
    // With the sorter running, nobody walks up asking for a color the counter has not got,
    // so the sorry-we're-out button goes quiet for the week. That is a different kind of
    // relief from the other four: they all make the same job easier, this one deletes a
    // question. Lives here rather than in DayConfig because the roll happens per kid, in
    // GameManager, not per day.
    public static bool ScreensOrders(int day) =>
        ActiveOn(day) && Kind == BoostKind.AutoSorter;

    // Everything else lands on the day's own numbers, so it applies the moment the day is
    // built and nothing downstream has to know a boost exists.
    public static DayConfig Apply(DayConfig day, int dayNumber)
    {
        if (!ActiveOn(dayNumber)) return day;

        switch (Kind)
        {
            case BoostKind.BigTips:
                day.reward *= 1.6f;
                break;

            case BoostKind.Forgiving:
                day.penalty *= 0.5f;
                break;

            case BoostKind.ExtraHands:
                // The TAs', and the only perk that shortens the day rather than easing it.
                // Everything else changes how a shift plays; this one changes how long it
                // is. Floored at four so a day is still a day.
                day.quota = Mathf.Max(4, day.quota - 2);
                break;

        }

        return day;
    }

    public static string Describe() => Describe(Kind);

    public static string Describe(BoostKind kind)
    {
        switch (kind)
        {
            case BoostKind.BigTips: return "BIG TIPS";
            case BoostKind.SimplePalette: return "SIMPLE PALETTE";
            case BoostKind.CleanFloor: return "CLEAN FLOOR";
            case BoostKind.Forgiving: return "FORGIVING";
            case BoostKind.AutoSorter: return "AUTO SORTER";
            case BoostKind.ExtraHands: return "EXTRA HANDS";
            default: return string.Empty;
        }
    }

    // What it actually does, in the fewest words that still mean something.
    //
    // A name on its own is decoration. "SIMPLE PALETTE" tells a player nothing they can act
    // on, and a perk nobody understands may as well not have been won. This is what goes
    // under the name everywhere it is shown.
    public static string Explain(BoostKind kind)
    {
        switch (kind)
        {
            case BoostKind.BigTips: return "SERVES ARE WORTH MORE TIME";
            case BoostKind.SimplePalette: return "ONE FEWER COLOR ON THE COUNTER";
            case BoostKind.CleanFloor: return "THE FLOOR GETS DIRTY HALF AS FAST";
            case BoostKind.Forgiving: return "MISTAKES COST HALF THE TIME";
            case BoostKind.AutoSorter: return "NOBODY ASKS FOR WHAT YOU HAVE NOT GOT";
            case BoostKind.ExtraHands: return "TWO FEWER KIDS TO CLEAR EACH DAY";
            default: return string.Empty;
        }
    }
}
