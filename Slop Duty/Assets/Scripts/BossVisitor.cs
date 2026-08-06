using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The boss encounter: somebody drops in from above, orders several things in a row, and
// pays out a perk for the week if you keep up.
//
// Deliberately not an IndividualStudent. A student is one request, one patience clock and a
// slot in a line, and every one of those is wrong here: a boss has a queue of requests, its
// own timer, and stands where the line is not. Reusing that class would have meant a flag
// on nearly every method in it.
//
// The day clock keeps running throughout, which is the stake. Each order served pays the
// normal reward, so keeping up roughly breaks even on time and the perk is the upside,
// while fumbling burns the day's tempo for nothing. That is the whole risk and it needed no
// invented penalty on top.
public class BossVisitor : MonoBehaviour
{
    private enum Phase { Falling, Ordering, Leaving }

    private SpriteRenderer body;
    private SpriteRenderer bubble;
    private Sprite requestArt;

    private readonly List<Color> orders = new List<Color>();
    private int served;

    private BoostKind prize;
    private string owner = string.Empty;

    private Phase phase = Phase.Falling;
    private float fallFrom;
    private float landAt;
    private float fallClock;
    private float fallSeconds = 0.55f;

    private float timeLimit;
    private float timeLeft;
    private float leaveClock;

    private float payPerOrder = 5f;
    private float shakeAmount;
    private float shakeSeconds;

    public bool Finished { get; private set; }
    public bool Won { get; private set; }

    // Fired once the encounter is over either way, so the director can start the line up
    // again without polling.
    public event System.Action<BossVisitor> Ended;

    public float Remaining => Mathf.Max(0f, timeLeft);
    public float Fraction => timeLimit <= 0f ? 0f : Mathf.Clamp01(timeLeft / timeLimit);
    public int Served => served;
    public int Total => orders.Count;
    public string Owner => owner;

    // Read by the interface so the player can see what they are playing for while they are
    // playing for it. A prize only revealed on winning is not a motivation, it is a surprise.
    public BoostKind Prize => prize;

    public void Begin(Sprite portrait, BossDirector.BubbleArt art, string who, BoostKind boost,
                      int orderCount, float seconds, Vector3 stand, float dropHeight,
                      float shake, float shakeTime, float scale, float pay)
    {
        owner = who;
        prize = boost;
        timeLimit = Mathf.Max(1f, seconds);
        timeLeft = timeLimit;
        requestArt = art.slop;
        payPerOrder = pay;
        shakeAmount = shake;
        shakeSeconds = shakeTime;

        transform.position = new Vector3(stand.x, stand.y + dropHeight, stand.z);
        transform.localScale = Vector3.one * scale;

        landAt = stand.y;
        fallFrom = stand.y + dropHeight;
        fallClock = 0f;

        // A SortingGroup at zero, which is exactly what a kid at the front of the line has.
        // Sorted by hand instead, the boss drew over the counter and stood on top of it: the
        // counter is in front of the queue, so anything that wants to be behind the counter
        // has to sort the way the queue sorts rather than pick a number and hope.
        SortingGroup group = gameObject.AddComponent<SortingGroup>();
        group.sortingOrder = 0;

        body = gameObject.AddComponent<SpriteRenderer>();
        body.sprite = portrait;
        body.sortingOrder = 2;

        BuildBubble(art);
        BuildHitbox(portrait);
        RollOrders(orderCount);
    }

    // Three stacked layers, same as the kids carry: the speech bubble, the bowl inside it,
    // and the slop in the bowl. Only the slop is repainted.
    //
    // This was one sprite at a guessed offset to begin with, which is why it came out as a
    // giant blob floating near the ceiling: the offset was written as though it were world
    // units, then multiplied again by the boss's own scale. Copying the prefab's actual
    // numbers is the fix, and it makes a boss order look like every other order.
    private void BuildBubble(BossDirector.BubbleArt art)
    {
        GameObject go = new GameObject("Boss Bubble");
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

    // Sized off the artwork rather than a fixed box, so a taller boss is not clickable
    // above their own head and a shorter one is not unclickable at the shoulders.
    private void BuildHitbox(Sprite portrait)
    {
        BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();

        if (portrait == null) return;

        box.size = portrait.bounds.size;
        box.offset = portrait.bounds.center;
    }

    // Every order is a color that IS on the counter, so a boss is always answerable. The
    // sorry-we're-out case is a different lesson and mixing it in here would turn a set
    // piece into a guessing game.
    private void RollOrders(int count)
    {
        SlopLogic logic = SlopLogic.Instance;

        orders.Clear();
        if (logic == null || logic.GetColorCount() == 0) return;

        int available = logic.GetColorCount();

        for (int i = 0; i < count; i++)
        {
            Color next = logic.GetColor(Random.Range(0, available));

            // Never the same color twice running. Repeats would let a whole boss be
            // cleared on one held scoop, which is not an encounter, it is a button.
            if (orders.Count > 0 && available > 1)
            {
                int guard = 0;

                while (next == orders[orders.Count - 1] && guard++ < 12)
                    next = logic.GetColor(Random.Range(0, available));
            }

            orders.Add(next);
        }

        ShowOrder();
    }

    private void ShowOrder()
    {
        if (bubble == null) return;

        if (served >= orders.Count)
        {
            bubble.enabled = false;
            return;
        }

        Color want = orders[served];

        bubble.enabled = true;

        Sprite painted = requestArt != null ? SpriteRecolor.For(requestArt, want) : null;

        if (painted != null)
        {
            bubble.sprite = painted;
            bubble.color = Color.white;
        }
        else
        {
            bubble.sprite = requestArt;
            bubble.color = want;
        }

        SlopLogic logic = SlopLogic.Instance;
        SymbolBadge.Apply(bubble, logic != null ? logic.SymbolFor(want) : -1, 0.34f);
    }

    private void Update()
    {
        switch (phase)
        {
            case Phase.Falling:
                Fall();
                break;

            case Phase.Ordering:
                Order();
                break;

            case Phase.Leaving:
                Leave();
                break;
        }
    }

    private void Fall()
    {
        fallClock += Time.deltaTime;

        float t = Mathf.Clamp01(fallClock / fallSeconds);

        // Accelerating, because a linear drop reads as a lift descending rather than as
        // something falling.
        float eased = t * t;

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(fallFrom, landAt, eased);
        transform.position = pos;

        if (t < 1f) return;

        CameraShake.Shake(shakeAmount, shakeSeconds);
        phase = Phase.Ordering;
    }

    private void Order()
    {
        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            Finish(false);
            return;
        }

        if (!Input.GetMouseButtonDown(0)) return;
        if (!UnderCursor()) return;

        SlopLogic logic = SlopLogic.Instance;
        if (logic == null) return;

        if (PukeManager.Instance != null && PukeManager.Instance.ServingBlocked) return;

        Slop held = logic.GetSelectedSlop();
        if (held == null || !held.GetIsSelected()) return;

        bool right = held.GetColor() == orders[served];

        GameManager game = GameManager.Instance;

        if (game != null)
        {
            // A right answer pays a flat amount that never shrinks with the day, which is
            // what makes a run survivable past the point the ordinary reward curve gives up.
            // A wrong one is charged exactly like any other mistake, so the penalty still
            // scales and a boss is not a free place to guess.
            if (right) game.ReportBossOrder(transform.position, payPerOrder);
            else game.ReportWrong(transform.position);
        }

        if (!right) return;

        served++;

        if (served >= orders.Count)
        {
            Finish(true);
            return;
        }

        ShowOrder();
    }

    // Same trap as the students: OverlapPoint returns one arbitrary collider, so anything
    // else under the cursor would swallow the click.
    private bool UnderCursor()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector2 point = cam.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(point);

        bool onMe = false;

        for (int i = 0; i < hits.Length; i++)
        {
            // A puddle under the cursor always wins: that click is a mop, not a serve.
            if (hits[i].GetComponentInParent<PukePuddle>() != null) return false;

            if (hits[i].GetComponentInParent<BossVisitor>() == this) onMe = true;
        }

        return onMe;
    }

    private void Finish(bool won)
    {
        Won = won;
        Finished = true;

        if (won && GameManager.Instance != null)
        {
            WeekBoost.Grant(prize, DayConfig.WeekFor(GameManager.Instance.Day), owner);
        }

        if (bubble != null) bubble.enabled = false;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null) box.enabled = false;

        phase = Phase.Leaving;
        leaveClock = 0f;

        Ended?.Invoke(this);
    }

    // Straight back up the way they came, so the exit reads as the entrance in reverse
    // rather than as the object being deleted.
    private void Leave()
    {
        leaveClock += Time.deltaTime;

        float t = Mathf.Clamp01(leaveClock / fallSeconds);

        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(landAt, fallFrom, t * t);
        transform.position = pos;

        if (t >= 1f) Destroy(gameObject);
    }
}
