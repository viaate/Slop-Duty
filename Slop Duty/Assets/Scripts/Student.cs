using System;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(SortingGroup))]
[RequireComponent(typeof(StudentLook))]
public class Student : MonoBehaviour
{
    [Header("Walk speed (units per second)")]
    [SerializeField] float walkInSpeed  = 6f;   // entering from off screen
    [SerializeField] float shuffleSpeed = 14f;  // shifting forward when the line closes up

    public event Action<Student> Arrived;

    // -1 to 1. The queue scales this by its own jitter setting.
    public float SlotOffset { get; set; }

    public float WalkInSpeed  { get => walkInSpeed;  set => walkInSpeed  = value; }
    public float ShuffleSpeed { get => shuffleSpeed; set => shuffleSpeed = value; }

    SortingGroup group;
    float targetX;
    bool walking;
    bool hasArrived;

    void Awake() => group = GetComponent<SortingGroup>();

    public void SetQueueIndex(int index) => group.sortingOrder = -index;

    public void WalkTo(float x)
    {
        if (Mathf.Abs(transform.position.x - x) < 0.0001f) return;
        targetX = x;
        walking = true;
    }

    void Update()
    {
        if (!walking) return;

        float speed = hasArrived ? shuffleSpeed : walkInSpeed;

        Vector3 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, targetX, speed * Time.deltaTime);
        transform.position = pos;

        if (pos.x != targetX) return;

        walking = false;
        if (hasArrived) return;   // shuffling forward is not a fresh arrival

        hasArrived = true;
        Arrived?.Invoke(this);
    }
}