using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("References (found automatically if left empty)")]
    [SerializeField] private StudentQueue queue;
    [SerializeField] private LevelTimer timer;
    [SerializeField] private SlopLogic slopLogic;

    [Header("Run")]
    [Tooltip("0 = Sunday, the tutorial shift with no clock. 1 = Monday, the real thing.")]
    [SerializeField] private int startDay = 1;

    [Tooltip("Share of kids who want a colour that is not on the counter. " +
             "For them the only correct answer is the sorry-we're-out button.")]
    [SerializeField, Range(0f, 0.5f)] private float outOfStockChance = 0.12f;

    [SerializeField] private bool freezeOnGameOver = true;

    public DayConfig Today { get; private set; }
    public int Day { get; private set; }
    public bool RunOver { get; private set; }
    public RunStats Stats { get; private set; } = new RunStats();
    public float OutOfStockChance => outOfStockChance;

    private int resolvedToday;

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

        Stats.Reset();
        Day = startDay;
        ApplyDay();

        if (timer == null) return;

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
    }

    // --- called by IndividualStudent ---

    public void ReportCorrect()
    {
        if (RunOver) return;

        Stats.served++;
        if (timer != null) timer.AddTime(Today.reward);
        CountResolution();
    }

    public void ReportWrong()
    {
        if (RunOver) return;

        Stats.wrongColour++;
        if (timer != null) timer.SubtractTime(Today.penalty);
        /* if(PukePuddle.active != true)
         * {
         *     Instantiate(PukePuddle Object, Puddle Position, PukePuddle.transform.rotation) ;
         * }
         * else
         * {
         *     PukePuddle.ChangeOpacity(50) ;
         * } */

        CountResolution();
    }

    public void ReportWalkOut()
    {
        if (RunOver) return;

        Stats.walkedOut++;
        if (timer != null) timer.SubtractTime(Today.penalty);
        CountResolution();
    }

    private void CountResolution()
    {
        resolvedToday++;
        if (resolvedToday < Today.quota) return;

        resolvedToday = 0;
        Stats.daysCleared++;
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
                  $"wrong {Stats.wrongColour}, walked out {Stats.walkedOut}, " +
                  $"accuracy {Stats.Accuracy:P0}");

        if (freezeOnGameOver) Time.timeScale = 0f;
    }
}
