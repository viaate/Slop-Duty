using UnityEngine;

// Decides when somebody drops in, clears the counter for them, and starts the line up again
// afterwards.
//
// Split from BossVisitor on purpose. The visitor knows how to be a boss and nothing about
// the day; this knows about the day and nothing about how a boss works. That means the
// spawn rule can change without touching the encounter, and vice versa.
public class BossDirector : MonoBehaviour
{
    // One person who might turn up. The sprite is whatever you already have of them: their
    // menu portrait is fine, it just needs to be assigned.
    [System.Serializable]
    public class Contender
    {
        public string name = "";
        public Sprite portrait;

        [Tooltip("Leave empty for one person. Fill it in and they turn up as a pair, both " +
                 "standing there at once, each with their own order to fill.")]
        public Sprite partner;

        [Tooltip("Optional prefab spawned behind them for the whole encounter, for anyone " +
                 "who deserves an entrance. A particle system, an animated sprite, anything " +
                 "prefabbed. It is placed behind the portrait, never in front.")]
        public GameObject aura;

        [Tooltip("Which perk beating them is worth. Keep these distinct: two people handing " +
                 "out the same perk makes it not matter who turned up.")]
        public BoostKind boost = BoostKind.BigTips;

        [Tooltip("How many things they order before they are satisfied.")]
        [Range(2, 8)] public int orders = 4;

        [Tooltip("Seconds to clear the whole lot. The day clock keeps running underneath, " +
                 "so this is not the only pressure.")]
        public float seconds = 16f;
    }

    [Header("Who can turn up")]
    [SerializeField]
    private Contender[] contenders =
    {
        new Contender { name = "OLIVIA",  boost = BoostKind.BigTips },
        new Contender { name = "SELENA",  boost = BoostKind.SimplePalette },
        new Contender { name = "JOHN",    boost = BoostKind.CleanFloor },
        new Contender { name = "TANEIM",  boost = BoostKind.Forgiving },
        new Contender { name = "AL",      boost = BoostKind.AutoSorter },

        // The TAs, and the only pair. Six orders rather than four because two of them ask
        // at once, so the same number would be over in half the time.
        new Contender { name = "JOY & LUCAS", boost = BoostKind.ExtraHands, orders = 6, seconds = 20f },
    };

    [Tooltip("Chance per boss day that the person you named yourself after turns up instead " +
             "of the next one in the rota. 0.25 is half again as often as an even share of " +
             "six would give them. 0 switches the favouritism off.")]
    [SerializeField, Range(0f, 1f)] private float favouriteChance = 0.25f;

    [Tooltip("How far apart a pair stands, in world units. They are centred on the same spot " +
             "a lone boss would use, so this splits them either side of it.")]
    [SerializeField] private float duoSpread = 2.6f;

    [Header("When")]
    [Tooltip("First week that can get a visit. Week 1 is left alone so the run has a chance " +
             "to teach itself before a set piece lands on it.")]
    [SerializeField, Min(1)] private int firstWeek = 2;

    [Tooltip("Seconds after the shift begins before they drop in, so the day card is gone " +
             "and a couple of ordinary kids have already walked on.")]
    [SerializeField] private float entranceDelay = 4f;

    // The three layers a kid's request bubble is made of. Copy them straight off the Student
    // prefab's Bubble children so a boss order looks like every other order: Bubble BG is
    // the speech bubble, Bubble Bowl is the dish, Bubble Slop is the bit that gets painted.
    [System.Serializable]
    public class BubbleArt
    {
        public Sprite background;
        public Sprite bowl;

        [Tooltip("student-slop.PNG. This is the only layer that gets repainted.")]
        public Sprite slop;

        [Tooltip("Where the bubble sits relative to the boss, in THEIR local units, so it " +
                 "scales with them. 0.49, 0.42 is what the Student prefab uses.")]
        public Vector2 offset = new Vector2(0.49f, 0.42f);
    }

    [Header("Request bubble")]
    [SerializeField] private BubbleArt bubbleArt = new BubbleArt();

    [Header("Entrance")]
    [SerializeField] private float dropHeight = 9f;

    [Tooltip("3 matches a student exactly, since the Student prefab's root is scaled 3. " +
             "Nudge up a little if you want a boss to loom.")]
    [SerializeField] private float bossScale = 3f;

    [SerializeField] private Vector2 standOffset = new Vector2(0f, 0f);

    [Tooltip("Seconds a correct boss order is worth. Flat, and deliberately not the day's " +
             "reward: the ordinary reward shrinks every day and bottoms out under a second, " +
             "so this is the one source of time that never decays. Four orders at 5 is 20 " +
             "seconds a week that a good player can always earn, whatever day they are on.")]
    [SerializeField] private float secondsPerOrder = 5f;

    [Header("Impact")]
    [SerializeField] private float shakeAmount = 0.42f;
    [SerializeField] private float shakeSeconds = 0.45f;

    [Tooltip("How fast the kids already at the counter drop through the floor to get out " +
             "of the way, in units per second.")]
    [SerializeField] private float clearSpeed = 14f;

    private GameManager game;
    private StudentQueue queue;

    private BossVisitor active;
    private float clock;
    private bool armedToday;
    private bool doneToday;

    public bool Busy => active != null;

    // So the interface can read the encounter without searching the scene every frame.
    public BossVisitor Active => active;

    // Fired once an encounter settles, win or lose. An event rather than something to poll,
    // because Active is cleared the same moment it ends: anything watching that field would
    // see the boss vanish and never see the result.
    public event System.Action<BossVisitor> Resolved;

    // --- naming yourself after somebody in the cast ---

    // The picture belonging to whoever the player named themselves after, or null.
    //
    // The contender list is reused as the cast rather than keeping a second list of names
    // and faces somewhere else. It already has every person in the game with a picture
    // attached, and two lists of the same people would drift the moment anybody was added
    // to one of them.
    //
    // A pair is split on the ampersand, so "JOY" finds Joy and "LUCAS" finds Lucas out of
    // the same entry rather than the duo only answering to both names at once.
    public static Sprite FaceFor(string who)
    {
        BossDirector director = Instance;
        if (director == null || string.IsNullOrWhiteSpace(who)) return null;

        string wanted = who.Trim().ToUpperInvariant();

        foreach (Contender c in director.contenders)
        {
            if (c == null || string.IsNullOrEmpty(c.name)) continue;

            string[] members = c.name.Split('&');

            for (int i = 0; i < members.Length; i++)
            {
                if (members[i].Trim().ToUpperInvariant() != wanted) continue;

                return i == 0 || c.partner == null ? c.portrait : c.partner;
            }
        }

        return null;
    }

    // Found once and cached, so the students asking about it are not each searching the
    // scene. They ask on spawn, which is often.
    private static BossDirector cached;

    private static BossDirector Instance
    {
        get
        {
            if (cached == null) cached = FindFirstObjectByType<BossDirector>();
            return cached;
        }
    }

    private void Start()
    {
        game = GameManager.Instance;
        if (game == null) game = FindFirstObjectByType<GameManager>();

        queue = FindFirstObjectByType<StudentQueue>();

        Check();

        if (game == null)
        {
            Debug.LogError($"{name}: no GameManager in this scene, so no boss will ever " +
                           "arrive. BossDirector reads the day off it.", this);
            return;
        }

        game.DayStarted += OnDayStarted;
        OnDayStarted(game.Day);
    }

    // Complained about at startup rather than four seconds into Monday of week two.
    //
    // Everything below used to fail silently: a contender with no picture simply did not
    // drop, and because the day was already marked as spent, nothing happened for the rest
    // of it either. An empty slot is the single most likely thing to be wrong here, so it
    // says so up front and names which one.
    private void Check()
    {
        if (contenders == null || contenders.Length == 0)
        {
            Debug.LogError($"{name}: the Contenders list is empty, so nobody can turn up.", this);
            return;
        }

        for (int i = 0; i < contenders.Length; i++)
        {
            if (contenders[i] != null && contenders[i].portrait != null) continue;

            string who = contenders[i] == null || string.IsNullOrEmpty(contenders[i].name)
                ? $"entry {i}"
                : contenders[i].name;

            Debug.LogError($"{name}: {who} has no Portrait assigned. They will be skipped " +
                           "when their turn comes round.", this);
        }

        if (bubbleArt == null || bubbleArt.slop == null)
        {
            Debug.LogWarning($"{name}: Bubble Art has no Slop sprite, so a boss will order " +
                             "things with nothing in their bubble. Copy the three sprites " +
                             "off the Student prefab's Bubble children.", this);
        }
    }

    private void OnDestroy()
    {
        if (game != null) game.DayStarted -= OnDayStarted;
    }

    // Mondays only, from firstWeek on. Deterministic rather than a roll, so it can be
    // tested, and so a player who has seen one knows exactly when the next is due. A rare
    // random event that people cannot predict reads as a bug the first time it happens.
    private void OnDayStarted(int day)
    {
        clock = 0f;
        doneToday = false;

        // Anybody still mid encounter goes home when the shift rolls over. The quota can be
        // met during a boss, which starts the next day underneath them, and without this the
        // banner carried on counting down someone else's Monday into Tuesday.
        if (active != null)
        {
            Destroy(active.gameObject);
            active = null;

            if (queue != null) queue.ResumeSpawning();
        }

        bool monday = day > 0 && (day - 1) % 5 == 0;
        armedToday = monday && DayConfig.WeekFor(day) >= firstWeek && contenders.Length > 0;

        // Says so either way, so "he did not drop" can be told apart from "it was never
        // his day". Those two look identical from the outside and want opposite fixes.
        if (armedToday)
        {
            Debug.Log($"{name}: boss day. Day {day}, week {DayConfig.WeekFor(day)}, " +
                      $"dropping in {entranceDelay:0.0}s.", this);
        }
    }

    private void Update()
    {
        if (!armedToday || doneToday || active != null) return;
        if (game == null || game.RunOver) return;

        clock += Time.deltaTime;
        if (clock < entranceDelay) return;

        Drop();
    }

    private void Drop()
    {
        Contender who = Pick();

        // Marked as spent only once somebody has actually landed. Setting it before these
        // checks meant a single missing picture cost the whole day, and cost it again every
        // week, with nothing in the console either way.
        if (who == null || who.portrait == null)
        {
            doneToday = true;

            Debug.LogError($"{name}: it is boss day but {(who == null ? "nobody was picked" : who.name + " has no Portrait")}, " +
                           "so nothing dropped in.", this);
            return;
        }

        doneToday = true;

        // Nobody new arrives while a boss is in, and everybody already standing there gets
        // out of the way. Otherwise the boss lands on top of a queue that is still ticking
        // down its patience, and the player loses kids to a cutscene.
        if (queue != null)
        {
            queue.StopSpawning();
            queue.ClearThroughFloor(clearSpeed);
        }

        Vector3 stand = StandPoint();

        GameObject go = new GameObject($"Boss {who.name}");
        go.transform.SetParent(transform, false);

        active = go.AddComponent<BossVisitor>();
        active.Ended += OnEnded;

        // One portrait or two. Everything past this point treats them the same way.
        Sprite[] cast = who.partner != null
            ? new[] { who.portrait, who.partner }
            : new[] { who.portrait };

        active.Begin(cast, bubbleArt, who.name, who.boost, who.orders,
                     who.seconds, stand, dropHeight, shakeAmount, shakeSeconds, bossScale,
                     secondsPerOrder, duoSpread, who.aura);
    }

    // Centre of the line rather than the front slot. The front slot is off to one side, and
    // a set piece that lands off centre reads as having missed its mark.
    private Vector3 StandPoint()
    {
        Vector3 origin = queue != null ? queue.transform.position : transform.position;
        return new Vector3(origin.x + standOffset.x, origin.y + standOffset.y, origin.z);
    }

    // Everyone gets a turn before anyone repeats. A plain roll would happily show the same
    // person three weeks running, which on a five week run is most of the run.
    private Contender Pick()
    {
        if (contenders.Length == 0) return null;

        // Named yourself after somebody? They turn up half again as often as anyone else.
        //
        // Rolled before the bag rather than folded into it, because the two want opposite
        // things: a shuffle bag exists to guarantee everyone gets a turn, and a favourite
        // exists to break that evenness on purpose. Weighting the bag would have done
        // neither properly. The bag still runs underneath, so the other five keep taking
        // their turns in order whenever this roll does not fire.
        Contender favourite = Favourite();

        if (favourite != null && Random.value < favouriteChance) return favourite;

        int seen = PlayerPrefs.GetInt(BagKey, 0);

        if (seen >= contenders.Length)
        {
            seen = 0;
            PlayerPrefs.SetInt(BagOrderKey, Random.Range(0, int.MaxValue));
        }

        // Shuffled by seeding off a stored number, so the order is stable within a bag but
        // different between them, without having to serialise the whole shuffled list.
        int order = PlayerPrefs.GetInt(BagOrderKey, 1);
        int index = Shuffled(seen, contenders.Length, order);

        PlayerPrefs.SetInt(BagKey, seen + 1);
        PlayerPrefs.Save();

        return contenders[index];
    }

    // Whichever entry the player named themselves after, or null if the name matches nobody.
    private Contender Favourite()
    {
        string who = HighScores.PlayerName;
        if (string.IsNullOrWhiteSpace(who)) return null;

        string wanted = who.Trim().ToUpperInvariant();

        foreach (Contender c in contenders)
        {
            if (c == null || string.IsNullOrEmpty(c.name)) continue;

            foreach (string member in c.name.Split('&'))
            {
                if (member.Trim().ToUpperInvariant() == wanted) return c;
            }
        }

        return null;
    }

    private const string BagKey = "SlopDuty.BossBag";
    private const string BagOrderKey = "SlopDuty.BossBagSeed";

    // A cheap fixed shuffle: step through the list by a stride that shares no factor with
    // its length, which visits every entry exactly once.
    private static int Shuffled(int position, int count, int seed)
    {
        int stride = 1 + (Mathf.Abs(seed) % Mathf.Max(1, count - 1));

        while (count > 1 && Greatest(stride, count) != 1) stride++;

        return ((Mathf.Abs(seed) + (position * stride)) % count + count) % count;
    }

    private static int Greatest(int a, int b)
    {
        while (b != 0)
        {
            int t = b;
            b = a % b;
            a = t;
        }

        return Mathf.Abs(a);
    }

    private void OnEnded(BossVisitor visitor)
    {
        active = null;

        Resolved?.Invoke(visitor);

        // Restarted whichever way it went. Losing the perk is the punishment for a fumbled
        // boss; losing the rest of the day would be a second one.
        if (queue != null) queue.ResumeSpawning();
    }
}
