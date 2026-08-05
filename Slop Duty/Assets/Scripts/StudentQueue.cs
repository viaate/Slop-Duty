using System.Collections.Generic;
using UnityEngine;

public class StudentQueue : MonoBehaviour
{
    [SerializeField] private Student studentPrefab;

    [Header("Line layout (offsets from this object)")]
    [SerializeField, Min(1)] private int maxStudents = 4;
    [SerializeField] private float frontX = 6f;    // where the kid at the front stands
    [SerializeField] private float backX = -6f;    // where the last kid stands when the line is full
    [SerializeField] private float spawnX = -13f;  // off the left edge

    [Tooltip("0 = perfectly even. 1 = as scattered as it can get while keeping right-to-left order.")]
    [SerializeField, Range(0f, 1f)] private float lineJitter = 0.5f;

    [Header("Walk speed (units per second)")]
    [SerializeField, Min(0.01f)] private float walkInSpeedMin = 5f;
    [SerializeField, Min(0.01f)] private float walkInSpeedMax = 7f;
    [SerializeField, Min(0.01f)] private float shuffleSpeed = 14f;

    [Header("Driven by DayConfig at runtime")]
    [SerializeField] private float spawnInterval = 3f;

    private readonly List<Student> line = new List<Student>();
    private float spawnTimer;
    private bool spawning = true;

    public int Capacity => maxStudents;
    public int Count => line.Count;
    public bool IsFull => line.Count >= maxStudents;

    // Index 0 is the kid at frontX, which is the one who has been there longest.
    public Student Front => line.Count > 0 ? line[0] : null;

    private void Awake()
    {
        if (studentPrefab != null) return;

        Debug.LogError($"{name}: Student Prefab is not assigned.", this);
        enabled = false;
    }

    public void Configure(DayConfig day)
    {
        spawnInterval = day.arrivalInterval;
    }

    public void StopSpawning() => spawning = false;

    private void Update()
    {
        if (!spawning) return;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;

        // Asked for fresh each time rather than cached from Configure, because the floor
        // stretches this out as the mess builds and that has to take effect immediately.
        spawnTimer = GameManager.Instance != null
            ? GameManager.Instance.CurrentArrivalInterval
            : spawnInterval;

        TrySpawn();
    }

    private void TrySpawn()
    {
        if (IsFull) return;

        Student s = Instantiate(studentPrefab, SpawnPos(), Quaternion.identity, transform);

        s.SlotOffset = Random.Range(-1f, 1f);
        s.SpeedRoll = Random.value;

        // Handed over explicitly rather than found with GetComponentInParent during Awake.
        // If this is missed, the student is destroyed without telling the queue and the
        // line silently stops closing up behind them.
        IndividualStudent kid = s.GetComponent<IndividualStudent>();
        if (kid != null) kid.SetQueue(this);

        StudentLook look = s.GetComponentInChildren<StudentLook>();
        if (look != null) look.Randomize();

        line.Add(s);
        ApplySpeeds();
        Reflow();
    }

    public void Remove(Student s)
    {
        if (!line.Remove(s)) return;
        Reflow();
    }

    private void ApplySpeeds()
    {
        // Speeds are re-applied from a per-student roll rather than set once at spawn,
        // so dragging the sliders during Play updates everyone already on screen.
        foreach (Student s in line)
        {
            if (s == null) continue;
            s.WalkInSpeed = Mathf.Lerp(walkInSpeedMin, walkInSpeedMax, s.SpeedRoll);
            s.ShuffleSpeed = shuffleSpeed;
        }
    }

    private void Reflow()
    {
        // Safety net. If anything ever destroys a student without calling Remove, this
        // stops the queue jamming at IsFull forever and stops Reflow throwing on nulls.
        line.RemoveAll(s => s == null);

        for (int i = 0; i < line.Count; i++)
        {
            line[i].SetQueueIndex(i);
            line[i].WalkTo(SlotX(i) + (line[i].SlotOffset * MaxJitter));
        }
    }

    private Vector3 SpawnPos() => transform.position + new Vector3(spawnX, 0f, 0f);

    private float Spacing => maxStudents <= 1 ? 0f : Mathf.Abs(frontX - backX) / (maxStudents - 1);

    // 0.45 rather than 0.5 so two neighbours can never land on the exact same spot
    private float MaxJitter => Spacing * 0.45f * lineJitter;

    private float SlotX(int index)
    {
        float offset = maxStudents <= 1
            ? frontX
            : Mathf.Lerp(frontX, backX, index / (float)(maxStudents - 1));

        return transform.position.x + offset;
    }

    private void OnValidate()
    {
        walkInSpeedMax = Mathf.Max(walkInSpeedMax, walkInSpeedMin);

        if (!Application.isPlaying) return;

        ApplySpeeds();
        Reflow();
    }

    private void OnDrawGizmos()
    {
        Vector3 origin = transform.position;

        Gizmos.color = Color.grey;
        Gizmos.DrawLine(SpawnPos(), new Vector3(SlotX(0), origin.y, origin.z));

        for (int i = 0; i < maxStudents; i++)
        {
            float x = SlotX(i);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(new Vector3(x, origin.y, origin.z), 0.35f);

            if (MaxJitter <= 0f) continue;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(x - MaxJitter, origin.y, origin.z),
                            new Vector3(x + MaxJitter, origin.y, origin.z));
        }
    }
}