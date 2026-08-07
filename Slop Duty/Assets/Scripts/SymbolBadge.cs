using UnityEngine;

// Puts a pixel shape on top of a sprite, so a slop can be told apart by its form when its
// color is not doing that job.
//
// A child renderer rather than a second sprite baked into the art, because the badge has
// to appear and disappear with a setting and the pans are repainted every day. Sized
// against whatever it is sitting on, so the same call works for a pan on the counter and
// for the much smaller bowl in a kid's thought bubble.
public static class SymbolBadge
{
    private const string ChildName = "Symbol Badge";

    // symbol below zero hides it, which is what SlopLogic.SymbolFor returns when the
    // setting is off. Kept rather than destroyed, so toggling costs nothing.
    public static void Apply(SpriteRenderer host, int symbol, float widthFraction)
    {
        if (host == null) return;

        Transform found = host.transform.Find(ChildName);

        if (symbol < 0)
        {
            if (found != null) found.gameObject.SetActive(false);
            return;
        }

        SpriteRenderer badge = Ensure(host, found);
        if (badge == null) return;

        badge.gameObject.SetActive(true);
        badge.sprite = SlopSymbols.For(symbol);
        badge.color = Color.white;

        // One in front of whatever it is marking. Same layer, so it cannot end up behind a
        // student who happens to sort above the counter.
        badge.sortingLayerID = host.sortingLayerID;

        // Two above the host, not one. A double slop lays its second color over the host at
        // exactly one above, and the shape has to stay readable on top of both halves. At
        // the same order as that half the two would be in an undefined draw order and the
        // badge would flicker behind the second color on some pans and not others.
        badge.sortingOrder = host.sortingOrder + 2;

        Fit(host, badge, widthFraction);
    }

    // Same reason MixPainter.Hide exists: the badge is a child renderer and does not follow
    // the host's enabled flag, so hiding a slop by switching off its renderer would leave the
    // shape hanging in the air.
    public static void Hide(SpriteRenderer host) => Apply(host, -1, 0f);

    private static SpriteRenderer Ensure(SpriteRenderer host, Transform found)
    {
        if (found != null) return found.GetComponent<SpriteRenderer>();

        GameObject go = new GameObject(ChildName);
        go.transform.SetParent(host.transform, false);

        return go.AddComponent<SpriteRenderer>();
    }

    // Measured against the host's own artwork rather than given a fixed size, because the
    // pans and the request bubble are nothing like the same scale and a badge that suits
    // one would be invisible or enormous on the other.
    private static void Fit(SpriteRenderer host, SpriteRenderer badge, float widthFraction)
    {
        badge.transform.localRotation = Quaternion.identity;
        badge.transform.localPosition = Vector3.zero;

        if (host.sprite == null || badge.sprite == null) return;

        // Centred on the ARTWORK, not on the transform.
        //
        // A sprite is drawn around its pivot, and none of this art is pivoted at its middle,
        // so sitting the badge at the origin left it visibly low and to one side of the
        // thing it was marking. sprite.bounds.center is exactly the gap between the pivot
        // and the centre of the picture, which is the correction needed and which stays
        // right if anybody re-pivots the art later.
        badge.transform.localPosition = host.sprite.bounds.center;

        float hostWidth = host.sprite.bounds.size.x;
        float badgeWidth = badge.sprite.bounds.size.x;

        if (hostWidth <= 0f || badgeWidth <= 0f) return;

        // Child of the host, so this inherits the host's own scale and only has to express
        // the difference between the two sprites.
        float scale = hostWidth * widthFraction / badgeWidth;
        badge.transform.localScale = new Vector3(scale, scale, 1f);
    }
}
