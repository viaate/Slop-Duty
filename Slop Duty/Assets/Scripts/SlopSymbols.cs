using System.Collections.Generic;
using UnityEngine;

// A set of small pixel shapes, one per slop color, so a color blind player can match by
// shape instead of by hue.
//
// Drawn in code rather than imported, for the same reason the serve sounds are: there is
// no asset to make, nothing to wire up, and no slot anybody can leave empty. Each shape is
// a solid dark form with a light outline round it, which is what lets the same badge stay
// readable sitting on a pan of any color.
//
// There are deliberately MORE shapes than there are pans. See SlopLogic.SymbolFor: a kid
// asking for something the counter has not got still needs a shape, or the absence of one
// would announce the answer before the player had looked at anything.
public static class SlopSymbols
{
    public const int Count = 12;

    private const int Size = 16;

    private static readonly Color Ink = new Color(0.07f, 0.07f, 0.06f, 1f);
    private static readonly Color Halo = new Color(0.97f, 0.97f, 0.93f, 1f);

    private static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();

    public static Sprite For(int index)
    {
        index = ((index % Count) + Count) % Count;

        if (cache.TryGetValue(index, out Sprite hit) && hit != null) return hit;

        Sprite made = Build(index);
        cache[index] = made;

        return made;
    }

    private static Sprite Build(int index)
    {
        bool[] solid = new bool[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // Sampled at pixel centres and mapped to -1..1, so shapes come out
                // symmetrical on an even sized grid instead of leaning one pixel.
                float nx = ((x + 0.5f) / Size * 2f) - 1f;
                float ny = ((y + 0.5f) / Size * 2f) - 1f;

                solid[(y * Size) + x] = Inside(index, nx, ny);
            }
        }

        Color[] pixels = new Color[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                int i = (y * Size) + x;

                if (solid[i]) { pixels[i] = Ink; continue; }

                // One pixel of light around the form. Without it a dark shape vanishes on
                // a dark slop, which is exactly the case this whole feature is for.
                pixels[i] = Touches(solid, x, y) ? Halo : Color.clear;
            }
        }

        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 32f,
                             0, SpriteMeshType.FullRect);
    }

    private static bool Touches(bool[] solid, int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx < 0 || ny < 0 || nx >= Size || ny >= Size) continue;
                if (solid[(ny * Size) + nx]) return true;
            }
        }

        return false;
    }

    // Chosen to stay apart at badge size, where fine detail disappears. Silhouette does the
    // work: round, pointed, square, crossed, barred. Two shapes that differ only in a
    // detail would be as useless here as two greens.
    private static bool Inside(int index, float x, float y)
    {
        float ax = Mathf.Abs(x);
        float ay = Mathf.Abs(y);

        switch (index)
        {
            case 0: return (x * x) + (y * y) <= 0.58f;                      // disc
            case 1: return y >= -0.78f && ax <= (0.78f - y) * 0.5f;         // triangle up
            case 2: return ax <= 0.68f && ay <= 0.68f;                      // square
            case 3: return (ax <= 0.24f && ay <= 0.82f)
                        || (ay <= 0.24f && ax <= 0.82f);                    // plus
            case 4: return ax + ay <= 0.9f;                                 // diamond
            case 5: return Mathf.Abs(ax - ay) <= 0.26f
                        && Mathf.Max(ax, ay) <= 0.78f;                      // cross
            case 6: return y <= 0.78f && ax <= (0.78f + y) * 0.5f;          // triangle down
            case 7: return (x * x) + (y * y) <= 0.62f
                        && (x * x) + (y * y) >= 0.2f;                       // ring
            case 8: return Mathf.Max(ax, ay) <= 0.72f
                        && Mathf.Max(ax, ay) >= 0.42f;                      // hollow square
            case 9: return ay <= 0.26f && ax <= 0.82f;                      // bar
            case 10: return ax <= 0.26f && ay <= 0.82f;                     // upright bar
            default: return ay <= 0.82f && ax <= 0.82f
                        && Mathf.Abs(ay - 0.42f) <= 0.22f;                  // double bar
        }
    }
}
