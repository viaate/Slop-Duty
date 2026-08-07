using UnityEngine;

// What is in one pan, and what one kid is asking for: either a single color, or a double,
// which is two colors sitting side by side in the same pan.
//
// Doubles exist because the palette runs out of room before the difficulty does. Past
// roughly six colors the hue wheel is crowded enough that a seventh shade is barely
// tellable from its neighbours, so asking for more of them makes the game unfair rather
// than harder. A double asks a different question instead. Not "which shade is this",
// which is eyesight, but "which pair is this", which is attention, and it is built out of
// colors the player can already tell apart.
public readonly struct SlopMix
{
    public readonly Color a;
    public readonly Color b;
    public readonly bool isDouble;

    private SlopMix(Color a, Color b, bool isDouble)
    {
        this.a = a;
        this.b = b;
        this.isDouble = isDouble;
    }

    // b mirrors a on a single so that anything reading .b without checking gets a sensible
    // color rather than a transparent black.
    public static SlopMix Single(Color color) => new SlopMix(color, color, false);

    // Sorted on the way in, so a pair is the same pair whichever order it was built in.
    //
    // Without this, red-then-blue and blue-then-red are two different values that draw as
    // mirror images of each other, and the player would have to notice which color is on
    // which side to tell them apart. That is a memory test, not a matching test, and it
    // would make a double genuinely unfair rather than merely harder. Ordering here means
    // one pair always looks the same and can be compared with a plain field check.
    public static SlopMix Double(Color x, Color y)
        => Before(x, y) ? new SlopMix(x, y, true) : new SlopMix(y, x, true);

    // Any consistent order will do, since this decides nothing but which half is which.
    private static bool Before(Color x, Color y)
    {
        if (!Mathf.Approximately(x.r, y.r)) return x.r < y.r;
        if (!Mathf.Approximately(x.g, y.g)) return x.g < y.g;

        return x.b < y.b;
    }

    // A single never matches a double, even one built from the same color twice. That is
    // the point of the mechanic: on a counter holding red, blue, and red-and-blue, the kid
    // who wanted plain red is not served by the double.
    public bool Matches(SlopMix other)
        => isDouble == other.isDouble && a == other.a && (!isDouble || b == other.b);

    public bool Uses(Color color) => a == color || (isDouble && b == color);

    // Fed to the spare-shape picker for colorblind mode, which needs one stable number per
    // request. Both halves go in, so two doubles sharing a color still get separate shapes.
    public int Key() => isDouble
        ? a.GetHashCode() ^ (b.GetHashCode() * 397)
        : a.GetHashCode();

    public override string ToString() => isDouble ? $"{a} + {b}" : a.ToString();
}
