using UnityEngine;

public enum ColorVision
{
    Normal = 0,
    Protanopia = 1,     // red cones missing
    Deuteranopia = 2,   // green cones missing
    Tritanopia = 3,     // blue cones missing
}

// Turns a color into what the chosen kind of color blindness actually sees.
//
// This exists so the game can be tested against a player's eyes rather than against
// assumptions about them. SlopLogic already picks its palette by measuring how far apart
// two colors are; feeding it simulated colors instead of literal ones means the whole
// existing machinery, the best-of-48 search and the repair pass, starts avoiding pairs
// THIS player cannot tell apart. Nothing else in the generator had to change.
//
// The alternative, banning red or banning green outright, would have been both cruder and
// wronger: which hues collide depends on the type, and a hue that is safe in isolation can
// still collide with a specific neighbour. Measuring beats guessing.
public static class Colorblind
{
    // Viénot, Brachi and Chiron's projection matrices, the standard approximation used for
    // this. They operate on LINEAR light, not on the gamma-encoded values in a Color, which
    // is why everything below converts on the way in and back on the way out. Skipping that
    // step is the usual reason a simulation comes out looking washed out.
    private static readonly float[] Protan =
    {
        0.152286f,  1.052583f, -0.204868f,
        0.114503f,  0.786281f,  0.099216f,
       -0.003882f, -0.048116f,  1.051998f,
    };

    private static readonly float[] Deutan =
    {
        0.367322f,  0.860646f, -0.227968f,
        0.280085f,  0.672501f,  0.047413f,
       -0.011820f,  0.042940f,  0.968881f,
    };

    private static readonly float[] Tritan =
    {
        1.255528f, -0.076749f, -0.178779f,
       -0.078411f,  0.930809f,  0.147602f,
        0.004733f,  0.691367f,  0.303900f,
    };

    public static Color Simulate(Color c, ColorVision vision)
    {
        float[] m = MatrixFor(vision);
        if (m == null) return c;

        float r = ToLinear(c.r);
        float g = ToLinear(c.g);
        float b = ToLinear(c.b);

        float lr = (m[0] * r) + (m[1] * g) + (m[2] * b);
        float lg = (m[3] * r) + (m[4] * g) + (m[5] * b);
        float lb = (m[6] * r) + (m[7] * g) + (m[8] * b);

        return new Color(ToGamma(lr), ToGamma(lg), ToGamma(lb), c.a);
    }

    private static float[] MatrixFor(ColorVision vision)
    {
        switch (vision)
        {
            case ColorVision.Protanopia: return Protan;
            case ColorVision.Deuteranopia: return Deutan;
            case ColorVision.Tritanopia: return Tritan;
            default: return null;
        }
    }

    // Plain language first, because almost nobody knows which of the three they are by its
    // clinical name but most people know they struggle with red and green.
    public static string LabelFor(ColorVision vision)
    {
        switch (vision)
        {
            case ColorVision.Protanopia: return "RED WEAK";
            case ColorVision.Deuteranopia: return "GREEN WEAK";
            case ColorVision.Tritanopia: return "BLUE WEAK";
            default: return "OFF";
        }
    }

    public static string NoteFor(ColorVision vision)
    {
        switch (vision)
        {
            case ColorVision.Protanopia: return "PROTANOPIA";
            case ColorVision.Deuteranopia: return "DEUTERANOPIA";
            case ColorVision.Tritanopia: return "TRITANOPIA";
            default: return "FULL COLOR RANGE, NO SHAPES";
        }
    }

    private static float ToLinear(float v) =>
        v <= 0.04045f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);

    private static float ToGamma(float v)
    {
        v = Mathf.Clamp01(v);
        return v <= 0.0031308f ? v * 12.92f : (1.055f * Mathf.Pow(v, 1f / 2.4f)) - 0.055f;
    }
}
