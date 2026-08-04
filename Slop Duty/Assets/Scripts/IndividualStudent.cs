using UnityEngine;

[RequireComponent(typeof(Student))]
public class IndividualStudent : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer studentSpriteRenderer;

    [Header("Fallback, used only when there is no GameManager")]
    [SerializeField] private float fallbackPatience = 10f;

    private Color assignedColor;
    private bool waiting;     // standing at the counter with the patience clock running
    private bool resolved;    // already dealt with, ignore any further input

    private float patienceTotal;
    private float patienceLeft;

    private Student student;
    private StudentQueue queue;
    private SlopOutButton slopOutButton;

    // 1 on arrival, 0 when they give up. Drive a patience bar off this.
    public float PatienceNormalized => patienceTotal <= 0f ? 1f : Mathf.Clamp01(patienceLeft / patienceTotal);

    private void Awake()
    {
        student = GetComponent<Student>();

        // Falls back to this object's own renderer so the colour still shows without
        // anyone wiring the slot by hand.
        if (studentSpriteRenderer == null) studentSpriteRenderer = GetComponent<SpriteRenderer>();

        // A prefab cannot hold a scene reference, so this is found at runtime.
        slopOutButton = FindFirstObjectByType<SlopOutButton>();
    }

    // Pushed by StudentQueue at spawn. Do not rely on GetComponentInParent during Awake,
    // because whether the parent is attached by then depends on how the object was created.
    public void SetQueue(StudentQueue owner) => queue = owner;

    private void OnEnable() => student.Arrived += OnArrived;
    private void OnDisable() => student.Arrived -= OnArrived;

    private void Start()
    {
        assignedColor = GenerateColor();
        if (studentSpriteRenderer != null) studentSpriteRenderer.color = assignedColor;
    }

    // Patience starts when they reach the counter, not when they spawn. Otherwise the
    // walk in eats patience, and kids heading for the far slot arrive with less of it
    // than kids heading for the near slot purely because of walk distance.
    private void OnArrived(Student s)
    {
        patienceTotal = GameManager.Instance != null
            ? GameManager.Instance.Today.patience
            : fallbackPatience;

        patienceLeft = patienceTotal;
        waiting = true;
    }

    private void Update()
    {
        if (resolved) return;

        // Patience only runs once they have reached the counter, but clicks are accepted
        // from the moment they exist, so a kid can be served while still walking in.
        // Serving early is a small skill reward: you buy back the walk time.
        if (waiting)
        {
            patienceLeft -= Time.deltaTime;

            if (patienceLeft <= 0f)
            {
                patienceLeft = 0f;
                Resolve(false, true);
                return;
            }
        }

        if (!Input.GetMouseButtonDown(0)) return;
        if (!IsUnderCursor()) return;

        TryServeStudent();
    }

    // OverlapPointAll, not OverlapPoint. OverlapPoint returns one arbitrary collider,
    // so anything else sitting under the cursor swallows the click. A cursor-following
    // ladle does exactly that, and puke puddles and roaches will too.
    // GetComponentInParent rather than a gameObject comparison, so a collider on a child
    // of the student still counts as hitting the student.
    private bool IsUnderCursor()
    {
        Vector2 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(world);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].GetComponentInParent<IndividualStudent>() == this) return true;
        }

        return false;
    }

    private Color GenerateColor()
    {
        SlopLogic logic = SlopLogic.Instance;
        if (logic == null || logic.GetColorCount() == 0) return Color.gray;

        float outChance = GameManager.Instance != null
            ? GameManager.Instance.OutOfStockChance
            : 0.12f;

        // A small share of kids want something that is not on the counter at all.
        // For them the only correct answer is the "sorry we're out" button.
        if (Random.value < outChance) return logic.GetOffPaletteColor();

        return logic.GetColor(Random.Range(0, logic.GetColorCount()));
    }

    private void TryServeStudent()
    {
        SlopLogic logic = SlopLogic.Instance;
        if (logic == null) return;

        // A full puddle shuts the counter down, and you cannot serve mid-mop either.
        if (PukeManager.Instance != null && PukeManager.Instance.ServingBlocked) return;

        bool saidOut = slopOutButton != null && slopOutButton.GetSlopOutPressed();
        Slop selected = logic.GetSelectedSlop();
        bool holdingSlop = selected != null && selected.GetIsSelected();

        // No pan picked up and no "we're out" claim, so this click is not an answer yet.
        if (!saidOut && !holdingSlop) return;

        bool correct = saidOut
            ? !logic.IsOnCounter(assignedColor)          // right only if we really are out
            : selected.GetColor() == assignedColor;

        if (slopOutButton != null) slopOutButton.ResetButton();
        logic.ClearSelection();

        Resolve(correct, false);
    }

    private void Resolve(bool correct, bool walkedOut)
    {
        if (resolved) return;

        resolved = true;
        waiting = false;

        GameManager game = GameManager.Instance;
        float where = transform.position.x;

        if (game != null)
        {
            if (walkedOut) game.ReportWalkOut(where);
            else if (correct) game.ReportCorrect(where);
            else game.ReportWrong(where);
        }

        Leave();
    }

    // Every exit path goes through here. Removing from the queue before destroying is
    // the whole fix: previously the queue kept holding destroyed students, so its count
    // never dropped, IsFull stayed true and spawning stopped forever after four kids.
    private void Leave()
    {
        if (queue == null) queue = GetComponentInParent<StudentQueue>();

        if (queue != null) queue.Remove(student);
        else Debug.LogWarning($"{name} left without a StudentQueue, so the line will not close up.", this);

        Destroy(gameObject);
    }

    // --- GETTERS & SETTERS ---
    public Color GetAssignedColor() => assignedColor;
    public void SetAssignedColor(Color color)
    {
        assignedColor = color;
        if (studentSpriteRenderer != null) studentSpriteRenderer.color = color;
    }

    public bool GetIsStaying() => waiting;
    public float GetStudentTimer() => patienceLeft;
}
