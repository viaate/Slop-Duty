using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PukePuddle : MonoBehaviour
{
    [Tooltip("Opacity added each time a student pukes into this puddle.")]
    [SerializeField, Range(0.01f, 1f)] private float perPuke = 0.20f;

    [Tooltip("Opacity removed by each mop click.")]
    [SerializeField, Range(0.01f, 1f)] private float perClick = 0.10f;

    private SpriteRenderer visual;
    private float opacity;

    public float Opacity => opacity;

    // At full opacity the counter shuts down until this is mopped off.
    public bool IsBlocking => opacity >= 1f;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        Apply();
    }

    public void AddPuke()
    {
        opacity = Mathf.Min(1f, opacity + perPuke);
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

    private void Apply()
    {
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
