using UnityEngine;

// One person inside a boss encounter: their picture, their thought bubble, and whatever
// they happen to be asking for right now.
//
// Split out of BossVisitor so a boss can be more than one person. A solo boss is one seat,
// the TAs are two, and the encounter itself does not care which: it hands out orders and
// asks who was clicked. Without this the duo would have meant a second copy of the drawing,
// the collider and the bubble code with "A" and "B" on the end of every field.
public class BossSeat : MonoBehaviour
{
    private SpriteRenderer body;
    private GameObject bubbleRoot;
    private SpriteRenderer bubble;
    private Sprite requestArt;

    private SlopMix want;

    public bool Asking { get; private set; }

    public void Build(Sprite portrait, BossDirector.BubbleArt art)
    {
        requestArt = art.slop;

        body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = portrait;
        body.sortingOrder = 2;

        BuildBubble(art);
        BuildHitbox(portrait);

        Quiet();
    }

    // Three stacked layers, same as the kids carry: the speech bubble, the bowl inside it,
    // and the slop in the bowl. Only the slop is repainted.
    private void BuildBubble(BossDirector.BubbleArt art)
    {
        GameObject go = new GameObject("Bubble");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = art.offset;

        // Kept, so the whole bubble can be taken down as one thing. Hiding the slop alone
        // leaves the frame and the empty bowl on screen, and a seat holding out an empty
        // dish still reads as a seat asking for something.
        bubbleRoot = go;

        Layer("Bubble BG", go.transform, art.background, 5);
        Layer("Bubble Bowl", go.transform, art.bowl, 6);

        bubble = Layer("Bubble Slop", go.transform, art.slop, 7);
    }

    private static SpriteRenderer Layer(string layerName, Transform parent, Sprite sprite, int order)
    {
        GameObject go = new GameObject(layerName);
        go.transform.SetParent(parent, false);

        SpriteRenderer r = go.AddComponent<SpriteRenderer>();
        r.sprite = sprite;
        r.sortingOrder = order;

        return r;
    }

    // Sized off the artwork rather than a fixed box, so a taller person is not clickable
    // above their own head and a shorter one is not unclickable at the shoulders. It also
    // means the two halves of a duo have separate hit areas rather than one shared blob.
    private void BuildHitbox(Sprite portrait)
    {
        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();

        if (portrait == null) return;

        box.size = portrait.bounds.size;
        box.offset = portrait.bounds.center;
    }

    // Something showy behind them: a particle system, an animated sprite, anything that is
    // a prefab. Optional, and only the people who warrant it get one.
    //
    // Parented to the seat so it falls in with them and leaves with them, and pushed behind
    // the portrait rather than in front. An effect meant to make somebody look impressive
    // has to be behind them, or it is just something covering their face.
    public void AddAura(GameObject prefab)
    {
        if (prefab == null) return;

        GameObject aura = Instantiate(prefab, transform);
        aura.transform.localPosition = Vector3.zero;

        // One behind the body, which sits at 2. Inside the boss's SortingGroup, so this is
        // ordering within the encounter rather than against the rest of the scene.
        foreach (Renderer r in aura.GetComponentsInChildren<Renderer>(true))
        {
            r.sortingLayerID = body != null ? body.sortingLayerID : r.sortingLayerID;
            r.sortingOrder = 1;
        }
    }

    public void Ask(SlopMix mix)
    {
        want = mix;
        Asking = true;

        if (bubble == null) return;

        if (bubbleRoot != null) bubbleRoot.SetActive(true);
        bubble.enabled = true;

        SlopLogic logic = SlopLogic.Instance;

        MixPainter.Paint(bubble, requestArt, logic != null ? logic.BubbleRightHalf : null, mix);
        SymbolBadge.Apply(bubble, logic != null ? logic.SymbolFor(mix) : -1, 0.34f);
    }

    // Satisfied, or the encounter is over. The picture stays, so a duo does not lose half
    // of itself the moment one of them is happy.
    public void Quiet()
    {
        Asking = false;

        // The whole bubble goes, not just the slop inside it.
        //
        // Two separate things were wrong here. The second colour of a double and the
        // colorblind shape are CHILD renderers, and Renderer.enabled does not reach a child,
        // so switching off the slop layer took the whole shape away and left its right half
        // drawing on its own. And the frame and the bowl were never hidden by anything at
        // all, so even once that was fixed the seat was still holding out an empty dish.
        //
        // SetActive on the root covers all five layers at once. There is no legitimate empty
        // bubble to preserve: orders are dealt in the same frame a seat is satisfied, so a
        // seat is only ever quiet for less than a frame unless it has finished for good.
        if (bubbleRoot != null) bubbleRoot.SetActive(false);

        // Belt and braces. SetActive does not clear these flags, so anything that ever brings
        // the root back without going through Ask still finds the children in a sane state.
        MixPainter.Hide(bubble);
        SymbolBadge.Hide(bubble);
    }

    public bool Wants(SlopMix mix) => Asking && want.Matches(mix);

    public void Disable()
    {
        Quiet();

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null) box.enabled = false;
    }
}
