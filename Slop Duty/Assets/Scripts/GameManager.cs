using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public const string TutorialPrefKey = "SlopDutyTutorial";

    [Header("References (found automatically if left empty)")]
    [SerializeField] private StudentQueue queue;
    [SerializeField] private LevelTimer timer;
    [SerializeField] private SlopLogic slopLogic;
    [SerializeField] private PukeManager puke;

    [Header("Run")]
    [Tooltip("0 = Sunday, the tutorial shift with no clock. 1 = Monday, the real thing. " +
             "The main menu's tutorial button overrides this.")]
    [SerializeField] private int startDay = 1;

    [Tooltip("Share of kids who want a color that is not on the counter. " +
             "For them the only correct answer is the sorry-we're-out button.")]
    [SerializeField, Range(0f, 0.5f)] private float outOfStockChance = 0.12f;

    [Header("Clock")]
    [Tooltip("Seconds you start the shift with.")]
    [SerializeField] private float startClock = 45f;

    [Tooltip("Ceiling on banked time. This is what stops a good player stockpiling " +
             "minutes on the easy days and coasting through the hard ones.")]
    [SerializeField] private float clockCap = 60f;

    [SerializeField] private bool freezeOnGameOver = true;

    // Guarded the same way the handler is, so a release build has neither the field nor a
    // compiler warning about a setting nothing reads.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Tooltip("Editor and development builds only. ] jumps to the Monday of the next week, " +
             "for reaching the boss weeks without playing through to them.")]
    [SerializeField] private bool devShortcuts = true;
#endif

    // Fired with the new day number whenever a shift begins, including the first.
    public event System.Action<int> DayStarted;

    // Fired once, when the clock runs out.
    public event System.Action RunEnded;

    // Fired whenever a serve moves the clock. Carries the change the timer actually made,
    // signed, along with the world position of the student who caused it, so the interface
    // can float the number where the player was already looking.
    //
    // An event rather than GameManager reaching into the UI directly, matching the two
    // above: the manager stays unaware that any interface exists, and the popups can be
    // switched off by simply not subscribing.
    public event System.Action<float, Vector2> TimeChanged;

    // Fired after every kid is dealt with, true when the player got it right. The Sunday
    // tutorial listens so it can move on to the next thing it wants to teach.
    public event System.Action<bool> StudentResolved;

    [Header("Sunday")]
    [Tooltip("Which kid of the training shift is the one who wants a color you have not " +
             "got. Guaranteed rather than rolled, so nobody can finish Sunday without ever " +
             "meeting the case that the sorry-we're-out button exists for.")]
    [SerializeField, Min(1)] private int sundayOutOfStockKid = 3;

    private int sundayKids;

    public DayConfig Today { get; private set; }
    public int Day { get; private set; }
    public bool RunOver { get; private set; }
    public RunStats Stats { get; private set; } = new RunStats();
    public float OutOfStockChance => outOfStockChance;

    private int resolvedToday;

    public int ResolvedToday => resolvedToday;
    public int QuotaToday => Today.quota;

    // The day's numbers after the floor has had its say. A filthy floor slows arrivals,
    // which starves your income, and shortens patience, which makes you fail more. Read
    // these rather than Today.* anywhere that actually drives gameplay.
    public float CurrentArrivalInterval =>
        Today.arrivalInterval * (puke != null ? puke.ArrivalMultiplier : 1f);

    public float CurrentPatience =>
        Mathf.Max(0.4f, Today.patience * (puke != null ? puke.PatienceMultiplier : 1f));

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // A previous run that ended with a freeze leaves timeScale at 0, and the scene
        // would reload permanently paused without this.
        Time.timeScale = 1f;
    }

    private void Start()
    {
        if (queue == null) queue = FindFirstObjectByType<StudentQueue>();
        if (timer == null) timer = FindFirstObjectByType<LevelTimer>();
        if (slopLogic == null) slopLogic = FindFirstObjectByType<SlopLogic>();
        if (puke == null) puke = FindFirstObjectByType<PukeManager>();

        // The main menu writes this because a plain bool on UIManager is destroyed
        // along with the menu scene, so the tutorial button used to do nothing.
        if (PlayerPrefs.GetInt(TutorialPrefKey, 0) == 1) startDay = 0;

        Stats.Reset();

        // A perk is worth a week, and a week does not outlive the run that won it. Without
        // this, a boost earned on one attempt would still be running on the next, which
        // would read as a mystery difficulty swing rather than as a reward.
        WeekBoost.Clear();

        Day = startDay;
        ApplyDay();

        if (timer == null) return;

        timer.Configure(startClock, clockCap);
        timer.Expired += OnTimerExpired;

        if (Day > 0) timer.Begin();   // Sunday runs without a clock
    }

    private void OnDestroy()
    {
        if (timer != null) timer.Expired -= OnTimerExpired;
        if (Instance == this) Instance = null;
    }

    private void ApplyDay()
    {
        // The week's perk is folded in the moment the day is built, so everything
        // downstream reads adjusted numbers and none of it has to know a boost exists.
        Today = WeekBoost.Apply(DayConfig.For(Day), Day);

        // Restarted with the shift, so replaying Sunday deals the same teaching hand
        // rather than carrying a count over from the last time through.
        if (Day == 0) sundayKids = 0;

        if (queue != null) queue.Configure(Today);
        if (slopLogic != null) slopLogic.GeneratePalette(Today.slopColors);

        DayStarted?.Invoke(Day);
    }

    // --- called by IndividualStudent. `where` is the student's world position: the mess
    //     still all piles into one puddle regardless, but the floating time number is
    //     anchored to it so it appears over the kid who caused it. ---

    public void ReportCorrect(Vector2 where)
    {
        if (RunOver) return;

        Stats.served++;
        Bank(timer != null ? timer.AddTime(Today.reward) : 0f, where);

        if (puke != null) puke.NoteServe();

        CountResolution(true);
    }

    public void ReportWrong(Vector2 where)
    {
        if (RunOver) return;

        Stats.wrongColor++;
        Bank(timer != null ? timer.SubtractTime(Today.penalty) : 0f, where);

        if (puke != null)
        {
            puke.AddMess();
            puke.NoteServe();
        }

        CountResolution(false);
    }

    public void ReportWalkOut(Vector2 where)
    {
        if (RunOver) return;

        Stats.walkedOut++;
        Bank(timer != null ? timer.SubtractTime(Today.penalty) : 0f, where);

        if (puke != null)
        {
            puke.AddMess();
            puke.NoteServe();
        }

        CountResolution(false);
    }

    // A boss order. Pays a flat amount instead of the day's reward, and does NOT count
    // toward the quota.
    //
    // Flat, because Today.reward shrinks every day and bottoms out at 0.8 seconds. A boss on
    // day 30 paying what a boss on day 6 paid is the whole point: it is the one source of
    // time that does not decay, so a player good enough to keep clearing them can stay ahead
    // of the curve indefinitely rather than being ground down on schedule.
    //
    // Off the quota, because the quota rolls the day over, and a day rolling over mid
    // encounter used to end the boss early and cost the player the perk they were most of
    // the way to earning.
    public void ReportBossOrder(Vector2 where, float seconds)
    {
        if (RunOver) return;

        Stats.served++;
        Bank(timer != null ? timer.AddTime(seconds) : 0f, where);
    }

    // Asked once per kid, at the moment their request is chosen.
    //
    // Every other day this is the dice roll it has always been. Sunday deals from a script
    // instead, because a 12% chance across a six kid shift means better than two in five
    // players finish the tutorial having never once needed the sorry-we're-out button, and
    // then meet it for the first time on Monday with a clock running.
    public bool NextRequestIsOutOfStock()
    {
        // Al's perk. With the sorter running nobody asks for something the counter has not
        // got, which is the one boost that removes a question rather than softening a number.
        if (WeekBoost.ScreensOrders(Day)) return false;

        if (Day > 0) return Random.value < outOfStockChance;

        sundayKids++;
        return sundayKids == sundayOutOfStockKid;
    }

    // One funnel for every clock change a serve can cause, so the popup and the sound can
    // never disagree with each other or with the timer.
    //
    // Takes the value the timer returned rather than the value it was asked for. On a
    // clock already at maxTime a correct serve genuinely banks nothing, and announcing a
    // reward that did not happen would teach the player the wrong thing about when to
    // stop hoarding.
    private void Bank(float delta, Vector2 where)
    {
        if (Mathf.Approximately(delta, 0f) && !ClockFull) return;

        TimeChanged?.Invoke(delta, where);

        if (delta < 0f) Sfx.TimeLost();
        else Sfx.TimeGained();
    }

    // True when a reward would be thrown away. Lets the interface say so out loud instead
    // of showing a silent nothing that looks identical to a bug.
    private bool ClockFull => timer != null && timer.Running
                              && timer.TimeRemaining >= timer.MaxTime - 0.01f;

    // --- testing ---

    // ] jumps to the Monday of the next week, which is where the bosses live. Press it
    // again to keep going: from Sunday or any of week 1 it lands on day 6, then 11, then 16.
    //
    // Compiled out of anything but the editor and a development build, so it cannot ship to
    // itch as a cheat. That is a compile-time guard rather than a runtime check on purpose,
    // because a runtime check leaves the key handler in the build for somebody to find.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        if (!devShortcuts || RunOver) return;
        if (!Input.GetKeyDown(KeyCode.RightBracket)) return;

        SkipToNextWeek();
    }

    private void SkipToNextWeek()
    {
        // Monday of the week after this one. Day 0 counts as week 1, so a jump from the
        // training shift lands on the first week that can have a boss in it.
        Day = (DayConfig.WeekFor(Day) * 5) + 1;
        resolvedToday = 0;

        ApplyDay();

        // The clock is only started by the real day-one rollover, so a jump that skips
        // past it has to start the clock itself or the run sits at zero forever.
        if (timer != null && !timer.Running) timer.Begin();

        Debug.Log($"Skipped to day {Day}, week {DayConfig.WeekFor(Day)}.", this);
    }
#endif

    private void CountResolution(bool correct)
    {
        StudentResolved?.Invoke(correct);

        resolvedToday++;
        if (resolvedToday < Today.quota) return;

        resolvedToday = 0;
        Stats.daysCleared++;

        // Surviving the training shift is what marks it as done, so the menu can send
        // first timers through Sunday and everyone else straight to Monday.
        if (Day == 0) HighScores.TutorialDone = true;

        Day++;
        ApplyDay();

        // The clock only starts once the tutorial shift is over.
        if (Day == 1 && timer != null) timer.Begin();
    }

    private void OnTimerExpired()
    {
        RunOver = true;

        if (queue != null) queue.StopSpawning();

        Debug.Log($"RUN OVER. Days cleared {Stats.daysCleared}, served {Stats.served}, " +
                  $"wrong {Stats.wrongColor}, walked out {Stats.walkedOut}, " +
                  $"accuracy {Stats.Accuracy:P0}");

        RunEnded?.Invoke();

        // Raised before the freeze so anything listening still gets a frame to react.
        if (freezeOnGameOver) Time.timeScale = 0f;
    }
}
