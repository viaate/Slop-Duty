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

    // One entry per person. A solo boss has one, the TAs have two, and everything below
    // works the same either way: each seat owns a fixed share of the orders, and clicks are
    // matched against whichever seat was actually under the cursor.
    private readonly List<BossSeat> seats = new List<BossSeat>();

    private readonly List<Color> orders = new List<Color>();
    private int served;

    // Where each seat is up to in the order list. They step through it by the number of
    // seats, so seat 0 takes 0, 2, 4 and seat 1 takes 1, 3, 5: an exactly even share rather
    // than whoever happens to be free grabbing the next one.
    private int[] cursor;

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

    public void Begin(Sprite[] portraits, BossDirector.BubbleArt art, string who, BoostKind boost,
                      int orderCount, float seconds, Vector3 stand, float dropHeight,
                      float shake, float shakeTime, float scale, float pay, float spread, GameObject aura)
    {
        owner = who;
        prize = boost;
        timeLimit = Mathf.Max(1f, seconds);
        timeLeft = timeLimit;
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

        BuildSeats(portraits, art, spread, aura);
        RollOrders(orderCount);
    }

    // One seat per portrait, spread evenly about the centre so a duo lands either side of
    // where a solo boss would have stood rather than one of them being off to a side.
    private void BuildSeats(Sprite[] portraits, BossDirector.BubbleArt art, float spread, GameObject aura)
    {
        seats.Clear();

        int count = portraits == null ? 0 : portraits.Length;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            if (portraits[i] == null) continue;

            GameObject go = new GameObject($"Seat {i}");
            go.transform.SetParent(transform, false);

            // Centred: one person sits at 0, two sit at minus and plus half the spread.
            float offset = count == 1 ? 0f : ((i / (float)(count - 1)) - 0.5f) * spread;
            go.transform.localPosition = new Vector3(offset, 0f, 0f);

            BossSeat seat = go.AddComponent<BossSeat>();
            seat.Build(portraits[i], art);
            seat.AddAura(aura);

            seats.Add(seat);
        }
    }

    // Every order is a color that IS on the counter, so a boss is always answerable. The
    // sorry-we're-out case is a different lesson and mixing it in here would turn a set
    // piece into a guessing game.
    private void RollOrders(int count)
    {
        SlopLogic logic = SlopLogic.Instance;

        orders.Clear();
        if (logic == null || logic.GetColorCount() == 0 || seats.Count == 0) return;

        // Rounded up to a whole number of helpings each, so a pair always eats exactly the
        // same amount. Asking for five between two people can only ever be three and two,
        // and one of them being hungrier than the other is not a duo, it is a bug report.
        count = Mathf.CeilToInt(count / (float)seats.Count) * seats.Count;

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

        // Each seat starts on its own index and steps by the number of seats from there.
        cursor = new int[seats.Count];
        for (int i = 0; i < cursor.Length; i++) cursor[i] = i;

        DealOrders();
    }

    // Everybody with nothing to ask for takes the next order from THEIR OWN share.
    //
    // A duo therefore has two live requests at once rather than taking turns, which is the
    // whole point of there being two of them: you choose who to serve, and one held scoop
    // might satisfy either. Taking turns would just be a solo boss with a spare drawing.
    //
    // Their shares are fixed rather than raced for. Dealing to whoever happened to be free
    // meant a slow eater could end up with two helpings and a fast one with four, which
    // reads as the game having lost count.
    private void DealOrders()
    {
        if (cursor == null) return;

        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i] == null || seats[i].Asking) continue;
            if (cursor[i] >= orders.Count) continue;

            seats[i].Ask(orders[cursor[i]]);
            cursor[i] += seats.Count;
        }
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

        DealOrders();

        if (!Input.GetMouseButtonDown(0)) return;

        // WHICH of them was clicked, not merely whether the boss was. With two people
        // standing there, "was it me" is not enough: serving the wrong one the right color
        // has to count as a mistake, or a duo would be no harder than one person with two
        // pictures.
        BossSeat clicked = SeatUnderCursor();
        if (clicked == null || !clicked.Asking) return;

        SlopLogic logic = SlopLogic.Instance;
        if (logic == null) return;

        if (PukeManager.Instance != null && PukeManager.Instance.ServingBlocked) return;

        Slop held = logic.GetSelectedSlop();
        if (held == null || !held.GetIsSelected()) return;

        bool right = clicked.Wants(held.GetColor());

        GameManager game = GameManager.Instance;

        if (game != null)
        {
            // A right answer pays a flat amount that never shrinks with the day, which is
            // what makes a run survivable past the point the ordinary reward curve gives up.
            // A wrong one is charged exactly like any other mistake, so the penalty still
            // scales and a boss is not a free place to guess.
            if (right) game.ReportBossOrder(clicked.transform.position, payPerOrder);
            else game.ReportWrong(clicked.transform.position);
        }

        if (!right) return;

        served++;
        clicked.Quiet();

        if (served >= orders.Count)
        {
            Finish(true);
            return;
        }

        DealOrders();
    }

    // Same trap as the students: OverlapPoint returns one arbitrary collider, so anything
    // else under the cursor would swallow the click.
    private BossSeat SeatUnderCursor()
    {
        Camera cam = Camera.main;
        if (cam == null) return null;

        Vector2 point = cam.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(point);

        BossSeat found = null;

        for (int i = 0; i < hits.Length; i++)
        {
            // A puddle under the cursor always wins: that click is a mop, not a serve.
            if (hits[i].GetComponentInParent<PukePuddle>() != null) return null;

            BossSeat seat = hits[i].GetComponentInParent<BossSeat>();
            if (seat != null && seats.Contains(seat)) found = seat;
        }

        return found;
    }

    private void Finish(bool won)
    {
        Won = won;
        Finished = true;

        if (won && GameManager.Instance != null)
        {
            WeekBoost.Grant(prize, DayConfig.WeekFor(GameManager.Instance.Day), owner);
        }

        foreach (BossSeat seat in seats)
        {
            if (seat != null) seat.Disable();
        }

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
