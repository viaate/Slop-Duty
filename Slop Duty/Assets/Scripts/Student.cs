using System;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(SortingGroup))]
[RequireComponent(typeof(StudentLook))]
public class Student : MonoBehaviour
{
    [Header("Walk speed (units per second)")]
    [SerializeField, Min(0.01f)] private float walkInSpeed = 6f;   // entering from off screen
    [SerializeField, Min(0.01f)] private float shuffleSpeed = 14f;  // shifting forward when the line closes up

    // Fires once, when they first reach the counter. IndividualStudent starts the
    // patience clock off this rather than off Start.
    public event Action<Student> Arrived;

    // Both rolled once at spawn and scaled by the queue, so its sliders stay live.
    public float SlotOffset { get; set; }   // -1 to 1
    public float SpeedRoll { get; set; }    // 0 to 1

    public float WalkInSpeed { get => walkInSpeed; set => walkInSpeed = value; }
    public float ShuffleSpeed { get => shuffleSpeed; set => shuffleSpeed = value; }

    private SortingGroup group;
    private float targetX;
    private bool walking;
    private bool hasArrived;
    private bool exiting;

    private bool sinking;
    private float sinkSpeed;
    private float sinkFloor;

    public bool IsExiting => exiting || sinking;

    // Straight down through the counter and gone. Used to clear the floor for a boss.
    //
    // Downward rather than off to the side because the side is where kids normally come and
    // go, and a queue that walks off the way it walked on reads as everyone being served at
    // once. Dropping through the floor reads as getting out of the way.
    public void SinkAway(float speed, float depth)
    {
        if (sinking) return;

        sinking = true;
        walking = false;
        sinkSpeed = Mathf.Max(0.01f, speed);
        sinkFloor = transform.position.y - Mathf.Max(0.5f, depth);
    }

    // Bolt off screen and delete on arrival. Used when a kid is dismissed or served, so
    // they visibly clear out instead of vanishing on the spot.
    public void ExitTo(float worldX, float speed)
    {
        exiting = true;
        hasArrived = true;        // makes Update use the fast lane below
        shuffleSpeed = Mathf.Max(0.01f, speed);
        targetX = worldX;
        walking = true;
    }

    private void Awake() => group = GetComponent<SortingGroup>();

    public void SetQueueIndex(int index) => group.sortingOrder = -index;

    public void WalkTo(float x)
    {
        if (Mathf.Abs(transform.position.x - x) < 0.0001f) return;

        targetX = x;
        walking = true;
    }

    private void Update()
    {
        if (sinking)
        {
            Vector3 down = transform.position;
            down.y = Mathf.MoveTowards(down.y, sinkFloor, sinkSpeed * Time.deltaTime);
            transform.position = down;

            if (down.y <= sinkFloor) Destroy(gameObject);
            return;
        }

        if (!walking) return;

        float speed = hasArrived ? shuffleSpeed : walkInSpeed;

        Vector3 pos = transform.position;
        pos.x = Mathf.MoveTowards(pos.x, targetX, speed * Time.deltaTime);
        transform.position = pos;

        if (pos.x != targetX) return;

        walking = false;

        if (exiting)
        {
            Destroy(gameObject);
            return;
        }

        if (hasArrived) return;   // shuffling forward is not a fresh arrival

        hasArrived = true;
        Arrived?.Invoke(this);
    }
}
