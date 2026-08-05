using UnityEngine;

public class PukeManager : MonoBehaviour
{
    public static PukeManager Instance;

    [SerializeField] private PukePuddle puddlePrefab;

    [Tooltip("Where the puddle sits, as an offset from this object. There is only ever " +
             "one puddle: every mess piles into the same spot and it just gets thicker.")]
    [SerializeField] private Vector2 puddleOffset = Vector2.zero;

    [Header("How much a dirty floor costs you")]
    [Tooltip("Seconds you cannot serve for after each mop click. This is what stops " +
             "mopping being free.")]
    [SerializeField] private float mopLockout = 0.4f;

    [Tooltip("Seconds you are stuck after serving when the floor is at full opacity. " +
             "Scales with how filthy it is, so a messy floor drags on everything.")]
    [SerializeField] private float maxServeDelay = 1.0f;

    private PukePuddle puddle;
    private float lockoutUntil;

    // 0 when the floor is clean, 1 when the puddle is at full opacity.
    public float Filth => puddle == null ? 0f : puddle.Opacity;

    public bool AnyBlockingPuddle => puddle != null && puddle.IsBlocking;

    // The player cannot serve while mopping, while still slowed by the mess, or at all
    // once the puddle hits full opacity.
    public bool ServingBlocked => Time.time < lockoutUntil || AnyBlockingPuddle;

    public Vector3 PuddlePosition => transform.position + new Vector3(puddleOffset.x, puddleOffset.y, 0f);

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

    // Called whenever a student is failed. Everything lands in the one spot.
    public void AddMess()
    {
        if (puddlePrefab == null)
        {
            Debug.LogError($"{name}: Puddle Prefab is not assigned, so no puke will ever spawn.", this);
            return;
        }

        if (puddle == null)
            puddle = Instantiate(puddlePrefab, PuddlePosition, Quaternion.identity, transform);

        puddle.AddPuke();
    }

    // Called after every resolution. The dirtier the floor, the longer the player is
    // stuck before the next kid can be served.
    public void NoteServe()
    {
        float delay = Filth * maxServeDelay;
        if (delay <= 0f) return;

        lockoutUntil = Mathf.Max(lockoutUntil, Time.time + delay);
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
            return;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 0.1f);
        Gizmos.DrawWireSphere(PuddlePosition, 0.6f);
    }
}
