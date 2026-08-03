using System.Collections.Generic;
using UnityEngine;

public class StudentQueue : MonoBehaviour
{
    [SerializeField] Student studentPrefab;

    [Header("Line layout (offsets from this object)")]
    [SerializeField, Min(1)] int maxStudents = 4;
    [SerializeField] float frontX = 6f;    // where the kid at the front stands
    [SerializeField] float backX  = -6f;   // where the last kid stands when the line is full
    [SerializeField] float spawnX = -13f;  // off the left edge

    [Tooltip("0 = perfectly even. 1 = as scattered as it can get while keeping right-to-left order.")]
    [SerializeField, Range(0f, 1f)] float lineJitter = 0.5f;

    [Header("Walk speed (units per second)")]
    [SerializeField] float walkInSpeedMin = 5f;
    [SerializeField] float walkInSpeedMax = 7f;
    [SerializeField] float shuffleSpeed   = 14f;

    [Header("Arrivals")]
    [SerializeField] float spawnInterval = 3f;

    readonly List<Student> line = new List<Student>();
    float spawnTimer;

    public int Capacity => maxStudents;
    public int Count => line.Count;
    public bool IsFull => line.Count >= maxStudents;

    void Awake()
    {
        if (studentPrefab != null) return;

        Debug.LogError($"{name}: Student Prefab is not assigned.", this);
        enabled = false;
    }

    void Update()
    {
        spawnTimer -= Time.deltaTime;
        if (spawnTimer > 0f) return;

        spawnTimer = spawnInterval;
        TrySpawn();
    }

    void TrySpawn()
    {
        if (IsFull) return;

        Student s = Instantiate(studentPrefab, SpawnPos(), Quaternion.identity, transform);

        s.SlotOffset   = Random.Range(-1f, 1f);
        s.WalkInSpeed  = Random.Range(walkInSpeedMin, walkInSpeedMax);
        s.ShuffleSpeed = shuffleSpeed;

        StudentLook look = s.GetComponentInChildren<StudentLook>();
        if (look != null) look.Randomize();

        line.Add(s);
        Reflow();
    }

    public void Remove(Student s)
    {
        if (!line.Remove(s)) return;
        Reflow();
    }

    void Reflow()
    {
        for (int i = 0; i < line.Count; i++)
        {
            line[i].SetQueueIndex(i);
            line[i].WalkTo(SlotX(i) + line[i].SlotOffset * MaxJitter);
        }
    }

    Vector3 SpawnPos() => transform.position + new Vector3(spawnX, 0f, 0f);

    float Spacing => maxStudents <= 1 ? 0f : Mathf.Abs(frontX - backX) / (maxStudents - 1);

    // 0.45 rather than 0.5 so two neighbours can never land on the exact same spot
    float MaxJitter => Spacing * 0.45f * lineJitter;

    float SlotX(int index)
    {
        float offset = maxStudents <= 1
            ? frontX
            : Mathf.Lerp(frontX, backX, index / (float)(maxStudents - 1));

        return transform.position.x + offset;
    }

    void OnValidate()
    {
        if (Application.isPlaying) Reflow();
    }

    void OnDrawGizmos()
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