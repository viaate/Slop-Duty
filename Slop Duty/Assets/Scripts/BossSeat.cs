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
    private SpriteRenderer bubble;
    private Sprite requestArt;

    private Color want;

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

    public void Ask(Color color)
    {
        want = color;
        Asking = true;

        if (bubble == null) return;

        bubble.enabled = true;

        Sprite painted = requestArt != null ? SpriteRecolor.For(requestArt, color) : null;

        if (painted != null)
        {
            bubble.sprite = painted;
            bubble.color = Color.white;
        }
        else
        {
            bubble.sprite = requestArt;
            bubble.color = color;
        }

        SlopLogic logic = SlopLogic.Instance;
        SymbolBadge.Apply(bubble, logic != null ? logic.SymbolFor(color) : -1, 0.34f);
    }

    // Satisfied, or the encounter is over. The picture stays, so a duo does not lose half
    // of itself the moment one of them is happy.
    public void Quiet()
    {
        Asking = false;
        if (bubble != null) bubble.enabled = false;
    }

    public bool Wants(Color color) => Asking && want == color;

    public void Disable()
    {
        Quiet();

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null) box.enabled = false;
    }
}
