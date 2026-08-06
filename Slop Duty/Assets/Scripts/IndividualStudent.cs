using UnityEngine;

[RequireComponent(typeof(Student))]
public class IndividualStudent : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The renderer that shows what colour slop this kid wants. Once the character " +
             "has proper art this should be a bubble above their head, not their body.")]
    [SerializeField] private SpriteRenderer studentSpriteRenderer;

    [Tooltip("Optional. Assign slop.PNG and the request bubble shows the actual slop art " +
             "repainted to the wanted color, so it matches the pans exactly. Leave empty " +
             "and the renderer is simply tinted instead.")]
    [SerializeField] private Sprite requestSprite;

    [Header("Fallback, used only when there is no GameManager")]
    [SerializeField] private float fallbackPatience = 10f;

    [Header("Mop grace")]
    [Tooltip("A kid about to walk out gets a reprieve if the player was mopping this " +
             "recently. Never shown in the UI, on purpose.")]
    [SerializeField] private float mopGraceWindow = 1f;

    [Tooltip("Seconds granted. Once per kid, so it cannot be farmed.")]
    [SerializeField] private float mopGraceBonus = 1f;

    [Header("Exit")]
    [Tooltip("How fast a resolved kid bolts off screen. High on purpose: their slot has " +
             "already been freed, so a slow exit just reads as a bug.")]
    [SerializeField] private float exitSpeed = 34f;

    private Color assignedColor;
    private bool waiting;     // standing at the counter with the patience clock running
    private bool resolved;    // already dealt with, ignore any further input

    private float patienceTotal;
    private float patienceLeft;
    private bool graceUsed;

    private Student student;
    private StudentQueue queue;

    // 1 on arrival, 0 when they give up. Drive a patience bar off this.
    public float PatienceNormalized => patienceTotal <= 0f ? 1f : Mathf.Clamp01(patienceLeft / patienceTotal);

    private static bool Endless => GameManager.Instance != null
                                   && GameManager.Instance.Today.endlessPatience;

    // Both read by the Sunday tutorial, which has to wait for a kid to actually reach the
    // counter before pointing at them, and has to know which kind of kid they are before
    // deciding what to say.
    public bool Waiting => waiting;

    public bool WantsSomethingMissing => SlopLogic.Instance != null
                                         && !SlopLogic.Instance.IsOnCounter(assignedColor);

    public bool IsResolved => resolved;

    private void Awake()
    {
        student = GetComponent<Student>();

        // Falls back to this object's own renderer so the color still shows without
        // anyone wiring the slot by hand.
        if (studentSpriteRenderer == null) studentSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    // Pushed by StudentQueue at spawn. Do not rely on GetComponentInParent during Awake,
    // because whether the parent is attached by then depends on how the object was created.
    public void SetQueue(StudentQueue owner) => queue = owner;

    private void OnEnable() => student.Arrived += OnArrived;
    private void OnDisable() => student.Arrived -= OnArrived;

    private void Start()
    {
        assignedColor = GenerateColor();
        ShowRequest();
    }

    private void ShowRequest()
    {
        if (studentSpriteRenderer == null) return;

        // The same shape that is on the pan they want, so the two can be matched without
        // reading either color. A kid wanting something the counter has not got still gets
        // a shape, just not one of the shapes out there. See SlopLogic.SymbolFor.
        SlopLogic logic = SlopLogic.Instance;
        SymbolBadge.Apply(studentSpriteRenderer, logic != null ? logic.SymbolFor(assignedColor) : -1, 0.34f);

        // Repainting beats tinting here for the same reason it does on the pans: tinting
        // multiplies, so a red source blob can never become blue. Repainting also means
        // the bubble is literally the same artwork as the pan the player has to match.
        if (requestSprite != null)
        {
            Sprite painted = SpriteRecolor.For(requestSprite, assignedColor);

            if (painted != null)
            {
                studentSpriteRenderer.sprite = painted;
                studentSpriteRenderer.color = Color.white;
                return;
            }
        }

        studentSpriteRenderer.color = assignedColor;
    }

    // Patience starts when they reach the counter, not when they spawn. Otherwise the
    // walk in eats patience, and kids heading for the far slot arrive with less of it
    // than kids heading for the near slot purely because of walk distance.
    private void OnArrived(Student s)
    {
        // CurrentPatience, not Today.patience, so the mess on the floor shortens the
        // window the moment it appears rather than only from the next day.
        patienceTotal = GameManager.Instance != null
            ? GameManager.Instance.CurrentPatience
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
        // Sunday freezes the countdown outright rather than setting a very large patience.
        // A big number still ends, and the whole promise of the training shift is that it
        // does not: you can read a tooltip, go and make a drink, and come back to the same
        // kid still waiting.
        if (waiting && !Endless)
        {
            patienceLeft -= Time.deltaTime;

            if (patienceLeft <= 0f && !TryMopGrace())
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

    // Hidden mercy. A kid on the brink gets one more second if the player was mopping in
    // the last moment. Mopping is forced work that stops you serving, so losing someone
    // TO the mopping reads as the game cheating rather than as your own mistake.
    //
    // Deliberately never surfaced anywhere in the UI. A mercy the player can see is a
    // mercy they start planning around, and then it stops being mercy and becomes a
    // mechanic to exploit. Once per kid and capped at a second, so it cannot be farmed,
    // and it only ever fires on someone who was about to be lost regardless.
    private bool TryMopGrace()
    {
        if (graceUsed) return false;
        if (PukeManager.Instance == null) return false;
        if (!PukeManager.Instance.MoppedWithin(mopGraceWindow)) return false;

        graceUsed = true;

        // Both, so the patience bar reads 0 to 1 rather than overflowing past full.
        patienceLeft += mopGraceBonus;
        patienceTotal += mopGraceBonus;

        return true;
    }

    // OverlapPointAll, not OverlapPoint. OverlapPoint returns one arbitrary collider,
    // so anything else sitting under the cursor swallows the click.
    // GetComponentInParent rather than a gameObject comparison, so a collider on a child
    // of the student still counts as hitting the student.
    private bool IsUnderCursor()
    {
        Vector2 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(world);

        bool onMe = false;

        for (int i = 0; i < hits.Length; i++)
        {
            // A puddle under the cursor always wins: that click is a mop, not a serve.
            // Checked here rather than with a "click consumed" flag on PukeManager,
            // because Unity does not guarantee which component's Update runs first,
            // so a flag would resolve differently from frame to frame.
            if (hits[i].GetComponentInParent<PukePuddle>() != null) return false;

            if (hits[i].GetComponentInParent<IndividualStudent>() == this) onMe = true;
        }

        return onMe;
    }

    private Color GenerateColor()
    {
        SlopLogic logic = SlopLogic.Instance;
        if (logic == null || logic.GetColorCount() == 0) return Color.gray;

        // A small share of kids want something that is not on the counter at all. For them
        // the only correct answer is the sorry-we're-out button.
        //
        // The decision is asked for rather than rolled here, because on Sunday it is not a
        // roll: the training shift deals a fixed hand so the player is guaranteed to meet
        // this case instead of possibly finishing the day never having seen it.
        bool missing = GameManager.Instance != null
            ? GameManager.Instance.NextRequestIsOutOfStock()
            : Random.value < 0.12f;

        if (missing) return logic.GetOffPaletteColor();

        return logic.GetColor(Random.Range(0, logic.GetColorCount()));
    }

    private void TryServeStudent()
    {
        SlopLogic logic = SlopLogic.Instance;
        if (logic == null) return;

        // A full puddle shuts the counter down, and you cannot serve mid-mop either.
        if (PukeManager.Instance != null && PukeManager.Instance.ServingBlocked) return;

        Slop selected = logic.GetSelectedSlop();

        // Nothing scooped up yet, so this click is not an answer.
        if (selected == null || !selected.GetIsSelected()) return;

        // The selection deliberately survives the serve, so you can hand the same color
        // to several kids in a row without re-clicking the pan every time.
        Resolve(selected.GetColor() == assignedColor, false);
    }

    // Driven by the sorry-we're-out button, which now acts on the front of the line
    // rather than making the player click a specific kid.
    public void AnswerOutOfStock()
    {
        if (resolved) return;

        SlopLogic logic = SlopLogic.Instance;
        if (logic == null) return;

        if (PukeManager.Instance != null && PukeManager.Instance.ServingBlocked) return;

        // Right only if this kid genuinely wanted something that is not on the counter.
        Resolve(!logic.IsOnCounter(assignedColor), false);
    }

    private void Resolve(bool correct, bool walkedOut)
    {
        if (resolved) return;

        resolved = true;
        waiting = false;

        // They run off screen wearing the result. Free characterisation, since the exit
        // animation already exists and this is one sprite swap.
        if (!correct)
        {
            StudentLook look = GetComponentInChildren<StudentLook>();
            if (look != null) look.ShowPukeFace();
        }

        GameManager game = GameManager.Instance;

        // Full position, not just the x it used to pass. The floating time number is
        // anchored here, and reading the height off the student keeps it over their head
        // rather than at a guessed y that would drift the moment anyone moves the counter.
        Vector2 where = transform.position;

        if (game != null)
        {
            if (walkedOut) game.ReportWalkOut(where);
            else if (correct) game.ReportCorrect(where);
            else game.ReportWrong(where);
        }

        Leave();
    }

    // Every exit path goes through here. Removing from the queue before leaving is what
    // lets the line close up: previously the queue kept holding destroyed students, so
    // its count never dropped and spawning stopped forever after four kids.
    private void Leave()
    {
        if (queue == null) queue = GetComponentInParent<StudentQueue>();

        if (queue != null) queue.Remove(student);
        else Debug.LogWarning($"{name} left without a StudentQueue, so the line will not close up.", this);

        // Stop soaking up clicks meant for whoever shuffles into their place.
        Collider2D hitbox = GetComponentInChildren<Collider2D>();
        if (hitbox != null) hitbox.enabled = false;

        // Unparented so the queue's reflow cannot drag them back into a slot mid-exit.
        transform.SetParent(null, true);

        student.ExitTo(OffScreenRight(), exitSpeed);
    }

    private float OffScreenRight()
    {
        Camera cam = Camera.main;
        if (cam == null) return transform.position.x + 20f;

        return cam.transform.position.x + (cam.orthographicSize * cam.aspect) + 3f;
    }

    // --- GETTERS & SETTERS ---
    public Color GetAssignedColor() => assignedColor;
    public void SetAssignedColor(Color color)
    {
        assignedColor = color;
        ShowRequest();
    }

    public bool GetIsStaying() => waiting;
    public float GetStudentTimer() => patienceLeft;
}
