using System.Collections.Generic;
using UnityEngine;

public class PukeManager : MonoBehaviour
{
    public static PukeManager Instance;

    [SerializeField] private PukePuddle puddlePrefab;

    [Header("Where puddles can land (offsets from this object)")]
    [SerializeField, Min(1)] private int spotCount = 4;
    [SerializeField] private float leftX = -6f;
    [SerializeField] private float rightX = 6f;

    [Tooltip("A new mess joins an existing puddle if one is within this distance, " +
             "instead of always taking a fresh spot.")]
    [SerializeField] private float mergeDistance = 3f;

    [Tooltip("Seconds after each mop click during which the player cannot serve. " +
             "This is what makes cleaning cost you something.")]
    [SerializeField] private float mopLockout = 0.4f;

    private readonly Dictionary<int, PukePuddle> puddles = new Dictionary<int, PukePuddle>();
    private float lockoutUntil;

    // The player cannot serve while mopping, or while any puddle is at full opacity.
    public bool ServingBlocked
    {
        get
        {
            if (Time.time < lockoutUntil) return true;

            foreach (PukePuddle p in puddles.Values)
                if (p != null && p.IsBlocking) return true;

            return false;
        }
    }

    public bool AnyBlockingPuddle
    {
        get
        {
            foreach (PukePuddle p in puddles.Values)
                if (p != null && p.IsBlocking) return true;

            return false;
        }
    }

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

    // Called whenever a student is failed. worldX is where they were standing, so the
    // mess lands under them rather than in a fixed place.
    public void AddMess(float worldX)
    {
        if (puddlePrefab == null)
        {
            Debug.LogError($"{name}: Puddle Prefab is not assigned, so no puke will ever spawn.", this);
            return;
        }

        Prune();

        int spot = PickSpot(worldX);
        if (spot < 0) return;

        if (!puddles.TryGetValue(spot, out PukePuddle puddle) || puddle == null)
        {
            puddle = Instantiate(puddlePrefab, SpotPosition(spot), Quaternion.identity, transform);
            puddles[spot] = puddle;
        }

        puddle.AddPuke();
    }

    private void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(world);

        for (int i = 0; i < hits.Length; i++)
        {
            PukePuddle puddle = hits[i].GetComponentInParent<PukePuddle>();
            if (puddle == null) continue;

            puddle.Clean();
            lockoutUntil = Time.time + mopLockout;
            return;
        }
    }

    // Prefer joining a nearby puddle, then a free spot, then whichever is closest.
    private int PickSpot(float worldX)
    {
        int nearestExisting = -1;
        int nearestFree = -1;
        int nearestAny = -1;
        float bestExisting = float.MaxValue;
        float bestFree = float.MaxValue;
        float bestAny = float.MaxValue;

        for (int i = 0; i < spotCount; i++)
        {
            float distance = Mathf.Abs(SpotPosition(i).x - worldX);
            bool occupied = puddles.TryGetValue(i, out PukePuddle p) && p != null;

            if (distance < bestAny)
            {
                bestAny = distance;
                nearestAny = i;
            }

            if (occupied)
            {
                if (distance > mergeDistance || distance >= bestExisting) continue;
                bestExisting = distance;
                nearestExisting = i;
            }
            else if (distance < bestFree)
            {
                bestFree = distance;
                nearestFree = i;
            }
        }

        if (nearestExisting >= 0) return nearestExisting;
        if (nearestFree >= 0) return nearestFree;
        return nearestAny;
    }

    private void Prune()
    {
        List<int> dead = null;

        foreach (KeyValuePair<int, PukePuddle> pair in puddles)
        {
            if (pair.Value != null) continue;

            dead ??= new List<int>();
            dead.Add(pair.Key);
        }

        if (dead == null) return;

        for (int i = 0; i < dead.Count; i++) puddles.Remove(dead[i]);
    }

    private Vector3 SpotPosition(int index)
    {
        float offset = spotCount <= 1
            ? (leftX + rightX) * 0.5f
            : Mathf.Lerp(leftX, rightX, index / (float)(spotCount - 1));

        return transform.position + new Vector3(offset, 0f, 0f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 0.1f);

        for (int i = 0; i < spotCount; i++)
            Gizmos.DrawWireSphere(SpotPosition(i), 0.5f);
    }
}
