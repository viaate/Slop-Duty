using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PukePuddle : MonoBehaviour
{
    [Tooltip("Opacity added each time a student pukes into this puddle.")]
    [SerializeField, Range(0.01f, 1f)] private float perPuke = 0.20f;

    [Tooltip("Opacity removed by each mop click.")]
    [SerializeField, Range(0.01f, 1f)] private float perClick = 0.10f;

    [Header("Growth")]
    [Tooltip("Scale on a fresh puddle. Left at 1 so it starts exactly the size it was " +
             "drawn, and only ever grows from there. Dropping this below 1 makes the " +
             "first puke look shrunken and slightly off-centre, because the art is not " +
             "dead centre on its canvas so scaling pulls it toward the pivot.")]
    [SerializeField] private float growFrom = 1f;

    [Tooltip("Scale at full filth.")]
    [SerializeField] private float growTo = 1.25f;

    [Tooltip("Extra scale kicked in the moment someone throws up, then eased away.")]
    [SerializeField] private float punchScale = 0.25f;
    [SerializeField] private float punchDecay = 3.5f;

    private SpriteRenderer visual;
    private Vector3 baseScale = Vector3.one;
    private float opacity;
    private float punch;

    public float Opacity => opacity;

    public bool IsFull => opacity >= 1f;

    private void Awake()
    {
        // Captured before anything touches it. Growth is a multiplier on whatever size the
        // prefab was authored at, not an absolute scale. Setting localScale directly threw
        // away the prefab's own scale and snapped every puddle to one world unit, which
        // looked like the puddle spawning tiny and then growing.
        baseScale = transform.localScale;

        visual = GetComponent<SpriteRenderer>();
        Apply();
    }

    public void AddPuke() => AddPuke(perPuke);

    // The amount comes from PukeManager so the balance lives in one place in the scene,
    // rather than being frozen into whatever was typed on the prefab.
    public void AddPuke(float amount)
    {
        opacity = Mathf.Min(1f, opacity + Mathf.Max(0.01f, amount));
        punch = 1f;
        Apply();
    }

    public void Clean()
    {
        opacity -= perClick;

        if (opacity > 0f)
        {
            Apply();
            return;
        }

        opacity = 0f;
        Destroy(gameObject);
    }

    private void Update()
    {
        if (punch <= 0f) return;

        punch = Mathf.Max(0f, punch - (Time.deltaTime * punchDecay));
        Apply();
    }

    private void Apply()
    {
        // Scale tracks opacity, so the puddle spreads as it thickens rather than only
        // getting darker. Opacity alone is very easy to miss while you are looking at
        // the counter.
        float grow = Mathf.Lerp(growFrom, growTo, opacity) + (punch * punchScale);
        transform.localScale = new Vector3(baseScale.x * grow, baseScale.y * grow, baseScale.z);

        // Unity alpha runs 0 to 1, not 0 to 255. The previous version compared and added
        // values like 10 and 255 against a 0-to-1 channel, so one click drove alpha to
        // -10 and the puddle went invisible permanently on the very first mop.
        if (visual == null) visual = GetComponent<SpriteRenderer>();
        if (visual == null) return;

        Color c = visual.color;
        c.a = opacity;
        visual.color = c;
    }
}
