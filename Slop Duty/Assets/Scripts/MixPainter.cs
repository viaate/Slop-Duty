using System.Collections.Generic;
using UnityEngine;

// Draws a SlopMix onto one sprite renderer. Every place a slop appears goes through here:
// the pans, the kids' thought bubbles, the scoop on the cursor and the boss's bubble.
//
// A single is just a recolor, exactly as before. A double paints the WHOLE shape in the
// first color and lays the right-hand half over the top of it in the second.
//
// Overlaying a half onto the whole, rather than drawing a left half beside a right half,
// is deliberate. Measured pixel by pixel, the two half sprites tile the whole exactly:
// nothing overlaps, nothing is left bare, and neither strays outside the shape. So both
// approaches produce the same picture, but this one has two advantages. The host renderer
// keeps showing the full silhouette, which matters because SymbolBadge sizes and centres
// itself against host.sprite.bounds and would otherwise shrink onto the left half. And any
// soft pixel down the seam blends into the first color underneath instead of into the
// background, so there is no hairline gap between the halves at any zoom.
public static class MixPainter
{
    private const string ChildName = "Slop Second Half";

    private static readonly HashSet<int> warned = new HashSet<int>();

    public static void Paint(SpriteRenderer host, Sprite whole, Sprite rightHalf, SlopMix mix)
    {
        if (host == null || whole == null) return;

        Apply(host, whole, mix.a);

        SpriteRenderer second = Second(host, mix.isDouble);

        // Nothing to do for a single beyond making sure a pan that used to be a double is
        // not still wearing its other half.
        if (!mix.isDouble)
        {
            if (second != null) second.enabled = false;
            return;
        }

        if (rightHalf == null)
        {
            // Without the half sprite a double would quietly draw as a plain single, which
            // looks like the matching logic is broken rather than like an empty slot in the
            // inspector. Say so once instead.
            if (warned.Add(host.GetInstanceID()))
            {
                Debug.LogWarning($"{host.name}: a double slop was asked for but the matching " +
                                 "half sprite is not assigned on SlopLogic, so it is drawing " +
                                 "as a single. Set Counter Right Half and Bubble Right Half.", host);
            }

            if (second != null) second.enabled = false;
            return;
        }

        second.enabled = true;

        // Re-read every paint rather than only on creation, because the host's own sorting
        // can change after the child exists: pans are laid out and re-sorted per day, and a
        // boss seat sets its orders once its bubble is already built.
        second.sortingLayerID = host.sortingLayerID;
        second.sortingOrder = host.sortingOrder + 1;

        Apply(second, rightHalf, mix.b);
    }

    // Repaint the pixels where the texture allows it, tint where it does not. Same choice
    // the pans and bubbles were already making, kept in one place now that four callers
    // need it. SpriteRecolor caches on (sprite, color), so calling this every frame from
    // the cursor costs a dictionary lookup rather than a new texture.
    private static void Apply(SpriteRenderer target, Sprite source, Color color)
    {
        Sprite painted = SpriteRecolor.For(source, color);

        if (painted != null)
        {
            // Tint stays neutral on top of a repaint. Renderer color multiplies, so tinting
            // a repainted sprite would darken it.
            target.sprite = painted;
            target.color = Color.white;
            return;
        }

        target.sprite = source;
        target.color = color;
    }

    // Found by name rather than cached in a field, because the callers are a mix of
    // components and static helpers and several of them repaint objects they do not own.
    // Only created when a double actually needs it, so single-color days never pay for it.
    private static SpriteRenderer Second(SpriteRenderer host, bool needed)
    {
        Transform found = host.transform.Find(ChildName);
        if (found != null) return found.GetComponent<SpriteRenderer>();

        if (!needed) return null;

        GameObject go = new GameObject(ChildName);
        go.transform.SetParent(host.transform, false);

        // Sits exactly on the host. The half sprite is drawn on the same 32 by 32 canvas as
        // the whole one, so it is already in the right place with no offset at all.
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        return go.AddComponent<SpriteRenderer>();
    }
}
