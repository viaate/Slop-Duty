using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Draws a SlopMix onto one renderer. Every place a slop appears goes through here: the pans,
// the kids' thought bubbles, the scoop on the cursor and the boss's bubble.
//
// A single is just a recolor. A double paints the WHOLE shape in the first color and lays
// the right-hand half over the top of it in the second.
//
// Overlaying a half onto the whole, rather than drawing a left half beside a right half, is
// deliberate. Measured pixel by pixel, the two half sprites tile the whole exactly: nothing
// overlaps, nothing is left bare, and neither strays outside the shape. So both approaches
// produce the same picture, but this one has two advantages. The host keeps showing the full
// silhouette, which matters because SymbolBadge sizes and centres itself against
// host.sprite.bounds and would otherwise shrink onto the left half. And any soft pixel down
// the seam blends into the first color underneath instead of into the background, so there is
// no hairline gap between the halves at any zoom.
//
// The second color lives on a CHILD object with its own renderer, which has one consequence
// that has already caused a bug: switching off the host renderer does NOT hide the child.
// Anything hiding a slop must call Hide below rather than touching enabled itself.
public static class MixPainter
{
    private const string ChildName = "Slop Second Half";

    private static readonly HashSet<int> warned = new HashSet<int>();

    // --- world sprites: pans, kids, boss bubbles ---

    public static void Paint(SpriteRenderer host, Sprite whole, Sprite rightHalf, SlopMix mix)
    {
        if (host == null || whole == null) return;

        Recolor(whole, mix.a, out Sprite art, out Color tint);
        host.sprite = art;
        host.color = tint;

        if (!WantsSecond(host, rightHalf, mix))
        {
            ClearSecondHalf(host);
            return;
        }

        SpriteRenderer second = SecondSprite(host);
        if (second == null) return;

        second.enabled = true;

        // Re-read every paint rather than only on creation, because the host's own sorting
        // can change after the child exists: pans are laid out and re-sorted per day, and a
        // boss seat sets its orders once its bubble is already built.
        second.sortingLayerID = host.sortingLayerID;
        second.sortingOrder = host.sortingOrder + 1;

        Recolor(rightHalf, mix.b, out Sprite halfArt, out Color halfTint);
        second.sprite = halfArt;
        second.color = halfTint;
    }

    // The only correct way to hide a slop.
    //
    // Renderer.enabled is per component: it does not touch child objects. Hiding a double by
    // switching off the host alone leaves the second color drawing on its own, which reads
    // as half a slop floating in mid air. Showing again needs no counterpart, because every
    // caller repaints on the way back up and Paint sets the child for the new mix.
    public static void Hide(SpriteRenderer host)
    {
        if (host == null) return;

        host.enabled = false;
        ClearSecondHalf(host);
    }

    // For callers that are about to draw on the host themselves and only need the leftover
    // second color gone, such as the tint fallbacks that cannot show two colors at all.
    public static void ClearSecondHalf(SpriteRenderer host)
    {
        if (host == null) return;

        Transform found = host.transform.Find(ChildName);
        if (found == null) return;

        SpriteRenderer second = found.GetComponent<SpriteRenderer>();
        if (second != null) second.enabled = false;
    }

    private static SpriteRenderer SecondSprite(SpriteRenderer host)
    {
        Transform found = host.transform.Find(ChildName);
        if (found != null) return found.GetComponent<SpriteRenderer>();

        GameObject go = new GameObject(ChildName);
        go.transform.SetParent(host.transform, false);

        // Sits exactly on the host. The half sprite is drawn on the same 32 by 32 canvas as
        // the whole one, so it is already in the right place with no offset at all.
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        return go.AddComponent<SpriteRenderer>();
    }

    // --- UI images: the scoop that follows the cursor ---
    //
    // The cursor scoop has to draw over the WE'RE OUT button, and that button is on a Screen
    // Space Overlay canvas. Overlay is composited after the camera has finished, so no world
    // sprite can ever sort above it at any sorting order. The scoop is therefore a UI image
    // on a canvas of its own, and it needs its own version of all of the above.

    public static void Paint(Image host, Sprite whole, Sprite rightHalf, SlopMix mix)
    {
        if (host == null || whole == null) return;

        Recolor(whole, mix.a, out Sprite art, out Color tint);
        host.sprite = art;
        host.color = tint;

        if (!WantsSecond(host, rightHalf, mix))
        {
            ClearSecondHalf(host);
            return;
        }

        Image second = SecondImage(host);
        if (second == null) return;

        second.enabled = true;

        Recolor(rightHalf, mix.b, out Sprite halfArt, out Color halfTint);
        second.sprite = halfArt;
        second.color = halfTint;
    }

    public static void ClearSecondHalf(Image host)
    {
        if (host == null) return;

        Transform found = host.transform.Find(ChildName);
        if (found == null) return;

        Image second = found.GetComponent<Image>();
        if (second != null) second.enabled = false;
    }

    private static Image SecondImage(Image host)
    {
        Transform found = host.transform.Find(ChildName);
        if (found != null) return found.GetComponent<Image>();

        GameObject go = new GameObject(ChildName, typeof(RectTransform));
        go.transform.SetParent(host.transform, false);

        // Stretched over the whole of the host rather than sized, since the half sprite
        // shares the whole one's canvas. No sorting to set either: in uGUI a child always
        // draws in front of its parent's own image.
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.AddComponent<Image>();

        // Never a click target. This thing sits under the pointer every single frame, and a
        // raycast target there would swallow the clicks meant for the pans and the button.
        image.raycastTarget = false;

        return image;
    }

    // --- shared ---

    // Repaint the pixels where the texture allows it, tint where it does not. Same choice the
    // pans and bubbles were already making, kept in one place now that five callers need it.
    // SpriteRecolor caches on (sprite, color), so calling this every frame from the cursor
    // costs a dictionary lookup rather than a new texture.
    private static void Recolor(Sprite source, Color color, out Sprite sprite, out Color tint)
    {
        Sprite painted = SpriteRecolor.For(source, color);

        if (painted != null)
        {
            // Tint stays neutral on top of a repaint. Renderer and Image color both
            // multiply, so tinting a repainted sprite would darken it.
            sprite = painted;
            tint = Color.white;
            return;
        }

        sprite = source;
        tint = color;
    }

    private static bool WantsSecond(Object host, Sprite rightHalf, SlopMix mix)
    {
        if (!mix.isDouble) return false;
        if (rightHalf != null) return true;

        // Without the half sprite a double would quietly draw as a plain single, which looks
        // like the matching logic is broken rather than like an empty slot in the inspector.
        // Say so once instead.
        if (warned.Add(host.GetInstanceID()))
        {
            Debug.LogWarning($"{host.name}: a double slop was asked for but the matching half " +
                             "sprite is not assigned on SlopLogic, so it is drawing as a single. " +
                             "Set Counter Right Half and Bubble Right Half.", host);
        }

        return false;
    }
}
