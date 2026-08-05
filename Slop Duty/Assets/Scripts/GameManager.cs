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

    // Fired with the new day number whenever a shift begins, including the first.
    public event System.Action<int> DayStarted;

    // Fired once, when the clock runs out.
    public event System.Action RunEnded;

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
        Today = DayConfig.For(Day);

        if (queue != null) queue.Configure(Today);
        if (slopLogic != null) slopLogic.GeneratePalette(Today.slopColors);

        DayStarted?.Invoke(Day);
    }

    // --- called by IndividualStudent. worldX is unused now that every mess piles into
    //     one puddle, but it is kept on the signature so a future version can place
    //     splatter where the kid was standing. ---

    public void ReportCorrect(float worldX)
    {
        if (RunOver) return;

        Stats.served++;
        if (timer != null) timer.AddTime(Today.reward);
        if (puke != null) puke.NoteServe();
        CountResolution();
    }

    public void ReportWrong(float worldX)
    {
        if (RunOver) return;

        Stats.wrongColor++;
        if (timer != null) timer.SubtractTime(Today.penalty);

        if (puke != null)
        {
            puke.AddMess();
            puke.NoteServe();
        }

        CountResolution();
    }

    public void ReportWalkOut(float worldX)
    {
        if (RunOver) return;

        Stats.walkedOut++;
        if (timer != null) timer.SubtractTime(Today.penalty);

        if (puke != null)
        {
            puke.AddMess();
            puke.NoteServe();
        }

        CountResolution();
    }

    private void CountResolution()
    {
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
