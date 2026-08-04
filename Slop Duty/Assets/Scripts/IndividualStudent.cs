using UnityEngine;

[RequireComponent(typeof(Student))]
public class IndividualStudent : MonoBehaviour
{
    private Color assignedColor;
    private bool isStaying = false; // don't count down until the student has actually walked into place
    private bool satisfied = false;

    public static LevelTimer levelTimer;
    [SerializeField] private float startingStudentTimer = 10f;
    private float studentTimer;

    [Header("References")]
    [SerializeField] private SpriteRenderer studentSpriteRenderer;
    [SerializeField] private SlopOutButton slopOutButton;

    private Student studentMovement;
    private StudentQueue studentQueue;

    private void Awake()
    {
        // Automatically find global timer if not set
        if (levelTimer == null)
        {
            levelTimer = FindFirstObjectByType<LevelTimer>();
        }

        studentMovement = GetComponent<Student>();
        // StudentQueue instantiates students as its own children, so this finds
        // the queue that spawned this student.
        studentQueue = GetComponentInParent<StudentQueue>();
        studentTimer = startingStudentTimer;
    }

    private void OnEnable()
    {
        if (studentMovement != null) studentMovement.Arrived += HandleArrived;
    }

    private void OnDisable()
    {
        if (studentMovement != null) studentMovement.Arrived -= HandleArrived;
    }

    // Previously the 10s countdown started the instant the student object was
    // instantiated, burning down while it was still walking in from off-screen.
    // Now it starts once Student.cs says the student has reached its spot.
    private void HandleArrived(Student s)
    {
        isStaying = true;
        studentTimer = startingStudentTimer;
    }

    private void Start()
    {
        assignedColor = GenerateColor();
        if (studentSpriteRenderer != null)
        {
            studentSpriteRenderer.color = assignedColor;
        }
    }

    private void Update()
    {
        if (isStaying)
        {
            studentTimer -= Time.deltaTime;
            if (studentTimer <= 0)
            {
                OnTimerExpired();
            }
        }

        if (Input.GetMouseButtonDown(0) && isStaying)
        {
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if (hit.collider != null && hit.collider.gameObject == gameObject)
            {
                TryServeStudent();
            }
        }
    }

    private Color GenerateColor()
    {
        float rand = Random.value; // Value between 0.0 and 1.0

        if (rand <= 0.05f) // 5% chance for a completely custom random color
        {
            float r = Random.Range(0f, 1f);
            float g = Random.Range(0f, 1f);
            float b = Random.Range(0f, 1f);
            return new Color(r, g, b, 1f);
        }
        else // Select random color from active Slop Logic palette
        {
            if (SlopLogic.Instance != null && SlopLogic.Instance.GetColorCount() > 0)
            {
                int randomIndex = Random.Range(0, SlopLogic.Instance.GetColorCount());
                return SlopLogic.Instance.GetColor(randomIndex);
            }
            return Color.gray; // Fallback
        }
    }

    private void TryServeStudent()
    {
        Slop selectedSlopObj = SlopLogic.Instance.GetSelectedSlop();

        if (selectedSlopObj == null || !selectedSlopObj.GetIsSelected()) return;

        Color selectedColor = selectedSlopObj.GetColor();
        bool isSlopOutPressed = slopOutButton != null && slopOutButton.GetSlopOutPressed();

        // Matching logic execution
        if (assignedColor == selectedColor)
        {
            satisfied = !isSlopOutPressed;
        }
        else
        {
            bool exists = false;
            var slopList = SlopLogic.Instance.GetSlopObjectsList();

            for (int i = 0; i < slopList.Count; i++)
            {
                if (slopList[i].GetColor() == assignedColor)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                satisfied = isSlopOutPressed;
            }
            else
            {
                satisfied = !isSlopOutPressed;
            }
        }

        // Apply time rewards/penalties based on satisfaction
        if (satisfied)
        {
            if (levelTimer != null) levelTimer.AddTime(5f);
            WalkAway();
        }
        else
        {
            if (levelTimer != null) levelTimer.SubtractTime(5f);
        }

        // Reset slop out button state after evaluation
        if (slopOutButton != null) slopOutButton.ResetButton();
    }

    private void OnTimerExpired()
    {
        isStaying = false;
        if (levelTimer != null) levelTimer.SubtractTime(5f);
        WalkAway();
    }

    private void WalkAway()
    {
        isStaying = false;

        // Previously this only called Destroy(), so StudentQueue never knew the
        // student left: its internal `line` list kept a reference to a destroyed
        // object, which would throw null-reference errors the next time the
        // queue reflowed and shift everyone's positions/sorting incorrectly.
        if (studentQueue != null) studentQueue.Remove(studentMovement);

        Destroy(gameObject, 0.5f);
    }

    // --- GETTERS & SETTERS ---
    public Color GetAssignedColor() => assignedColor;
    public void SetAssignedColor(Color color) => assignedColor = color;

    public bool GetIsStaying() => isStaying;
    public void SetIsStaying(bool staying) => isStaying = staying;

    public bool GetSatisfied() => satisfied;
    public void SetSatisfied(bool isSatisfied) => satisfied = isSatisfied;

    public float GetStudentTimer() => studentTimer;
    public void SetStudentTimer(float time) => studentTimer = time;
}
