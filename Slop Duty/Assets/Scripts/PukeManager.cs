using UnityEngine;

// One puddle, in one place, that gets thicker with every failure.
//
// The floor no longer hard-stops the counter. A binary block was unreachable in practice:
// filling it took five failures, and five failures cost more clock than you had, so the
// block fired at the exact moment you were dead anyway.
//
// Instead the mess strangles you continuously. The dirtier the floor, the slower kids
// arrive (so you earn less clock) and the less patience they have when they get there
// (so you fail more). It is a spiral you can still climb out of by mopping, rather than
// a wall you hit once and lose to.
public class PukeManager : MonoBehaviour
{
    public static PukeManager Instance;

    [SerializeField] private PukePuddle puddlePrefab;

    [Tooltip("Where the puddle sits, as an offset from this object.")]
    [SerializeField] private Vector2 puddleOffset = Vector2.zero;

    [Header("How fast it builds")]
    [Tooltip("Opacity added per failure. At 0.34 it takes three mistakes to fill the floor.")]
    [SerializeField, Range(0.05f, 1f)] private float pukePerFailure = 0.34f;

    [Header("What a dirty floor costs you")]
    [Tooltip("Seconds you cannot serve for after each mop click. This is what stops " +
             "mopping being free.")]
    [SerializeField] private float mopLockout = 0.4f;

    [Tooltip("At full filth, kids take this much longer to walk in. 0.8 means a 3 second " +
             "arrival becomes 5.4 seconds, so your income dries up.")]
    [SerializeField, Range(0f, 2f)] private float arrivalSlowdown = 0.8f;

    [Tooltip("At full filth, patience shrinks by this fraction. 0.5 halves it.")]
    [SerializeField, Range(0f, 0.9f)] private float patienceSqueeze = 0.5f;

    private PukePuddle puddle;
    private float lockoutUntil;
    private float lastMopTime = -999f;

    // Used by IndividualStudent for the mop grace window. See TryMopGrace over there.
    public bool MoppedWithin(float seconds) => Time.time - lastMopTime <= seconds;

    // 0 on a clean floor, 1 when the puddle is at full opacity.
    public float Filth => puddle == null ? 0f : puddle.Opacity;

    // Only ever the mop lockout now. The floor being filthy makes the job harder, it does
    // not take the job away.
    public bool ServingBlocked => Time.time < lockoutUntil;

    public Vector3 PuddlePosition => transform.position + new Vector3(puddleOffset.x, puddleOffset.y, 0f);

    // Multipliers the rest of the game reads. Both are 1 on a clean floor.
    public float ArrivalMultiplier => 1f + (Filth * arrivalSlowdown);
    public float PatienceMultiplier => 1f - (Filth * patienceSqueeze);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Called whenever a student is failed. Everything piles into the one spot.
    public void AddMess()
    {
        if (puddlePrefab == null)
        {
            Debug.LogError($"{name}: Puddle Prefab is not assigned, so no puke will ever spawn.", this);
            return;
        }

        if (puddle == null)
            puddle = Instantiate(puddlePrefab, PuddlePosition, Quaternion.identity, transform);

        // Scaled by John's perk, which halves how fast the floor fills for the week.
        // Applied here rather than in DayConfig because the mess is not a per-day number:
        // it is a running total that carries from one kid to the next.
        float scale = GameManager.Instance != null
            ? WeekBoost.MessScale(GameManager.Instance.Day)
            : 1f;

        puddle.AddPuke(pukePerFailure * scale);
    }

    // Kept so GameManager does not need changing. The serve delay used to live here, but
    // slowing arrivals and squeezing patience are a better punishment than a dead pause.
    public void NoteServe()
    {
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (puddle == null) return;

        Vector2 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(world);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].GetComponentInParent<PukePuddle>() != puddle) continue;

            puddle.Clean();
            lockoutUntil = Time.time + mopLockout;
            lastMopTime = Time.time;
            return;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 0.1f);
        Gizmos.DrawWireSphere(PuddlePosition, 0.6f);
    }
}
