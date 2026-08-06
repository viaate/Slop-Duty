using System.Collections.Generic;
using UnityEngine;

// Repaints a sprite at runtime by replacing its hue and saturation while keeping each
// pixel's own brightness, so one green slop blob can become any color on the counter
// without anyone redrawing the art.
//
// This exists because SpriteRenderer.color MULTIPLIES rather than replaces. Green
// (0, 1, 0) times red (1, 0, 0) is black, so plain tinting only works on white or grey
// source art. Keeping each pixel's brightness means shading and outlines survive.
public static class SpriteRecolor
{
    private static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
    private static readonly HashSet<int> warned = new HashSet<int>();

    // Returns null when this sprite cannot be repainted. Callers should fall back to
    // plain tinting rather than showing nothing.
    public static Sprite For(Sprite source, Color target)
    {
        if (source == null) return null;

        int key = Key(source, target);
        if (cache.TryGetValue(key, out Sprite hit) && hit != null) return hit;

        Sprite made = Build(source, target);
        if (made == null) return null;

        cache[key] = made;
        return made;
    }

    // Quantised, so two nearly identical colors do not each build their own texture.
    private static int Key(Sprite s, Color c)
    {
        int r = Mathf.RoundToInt(Mathf.Clamp01(c.r) * 31f);
        int g = Mathf.RoundToInt(Mathf.Clamp01(c.g) * 31f);
        int b = Mathf.RoundToInt(Mathf.Clamp01(c.b) * 31f);

        return (s.GetInstanceID() << 15) ^ (r << 10) ^ (g << 5) ^ b;
    }

    // Once per sprite, not once per pan per day, which was several warnings a second.
    private static void WarnOnce(Sprite source)
    {
        if (!warned.Add(source.GetInstanceID())) return;

        Debug.LogWarning(
            $"Sprite '{source.name}' cannot be repainted because its texture is not readable, " +
            "so the slop is being tinted instead. If that name is 'Square' it is Unity's built-in " +
            "placeholder, which has no import settings: assign your own art to SlopType.prefab. " +
            "If it is your own PNG, tick Read/Write Enabled on it in the Project window.");
    }

    private static Sprite Build(Sprite source, Color target)
    {
        Texture2D src = source.texture;

        if (src == null || !src.isReadable)
        {
            WarnOnce(source);
            return null;
        }

        Color.RGBToHSV(target, out float targetHue, out float targetSat, out float targetVal);

        // source.rect, not source.textureRect. On a Tight mesh sprite textureRect can
        // shrink to hug the opaque pixels, and rebuilding from that puts the pivot on the
        // centre of the artwork rather than the centre of the canvas. The sprite then
        // renders visibly offset, but only at runtime, because the editor is still showing
        // the untouched original.
        Rect area = source.rect;
        int x = Mathf.FloorToInt(area.x);
        int y = Mathf.FloorToInt(area.y);
        int w = Mathf.FloorToInt(area.width);
        int h = Mathf.FloorToInt(area.height);

        Color[] pixels = src.GetPixels(x, y, w, h);

        // Brightest pixel in the source, found first so the whole sprite can be rescaled
        // against it below.
        float brightest = 0f;

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a <= 0f) continue;

            Color.RGBToHSV(pixels[i], out _, out _, out float v);
            if (v > brightest) brightest = v;
        }

        if (brightest <= 0.001f) brightest = 1f;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color p = pixels[i];
            if (p.a <= 0f) continue;

            Color.RGBToHSV(p, out _, out float pixelSat, out float pixelVal);

            // Pixels that were already near-grey (outlines, highlights) keep their own
            // low saturation, so they do not turn into flat blocks of the new color.
            float sat = pixelSat <= 0.08f ? pixelSat : targetSat;

            // Brightness is rescaled so the sprite's brightest pixel lands exactly on the
            // target's brightness and everything else follows in proportion. Shading and
            // relative contrast survive, but the slop as a whole is now as light or dark
            // as the palette asked for.
            //
            // This line used to be `pixelVal`, which silently discarded the target's
            // brightness entirely. Every slop then rendered with the identical brightness
            // profile of the source art and differed only in hue, so two greens looked the
            // same and a decoy that had escaped by going dark came out looking like a real
            // pan. Lightness is roughly half of what makes two colors tell apart, and none
            // of it was reaching the screen.
            float value = Mathf.Clamp01(pixelVal / brightest * targetVal);

            Color rebuilt = Color.HSVToRGB(targetHue, sat, value);
            rebuilt.a = p.a;
            pixels[i] = rebuilt;
        }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = src.filterMode,
            wrapMode = src.wrapMode,
        };

        tex.SetPixels(pixels);
        tex.Apply();

        // Carry the original pivot across rather than assuming centre, so custom pivots
        // set in the Sprite Editor survive. source.pivot is in pixels from the rect's
        // bottom left, and Sprite.Create wants it normalized.
        Vector2 pivot = new Vector2(source.pivot.x / area.width, source.pivot.y / area.height);

        // FullRect, not the default Tight. A tight mesh regenerated from repainted pixels
        // can hug a different outline than the original did, which is another way the art
        // ends up sitting slightly differently to what the editor showed.
        return Sprite.Create(tex, new Rect(0f, 0f, w, h), pivot, source.pixelsPerUnit,
                             0, SpriteMeshType.FullRect, source.border);
    }
}
