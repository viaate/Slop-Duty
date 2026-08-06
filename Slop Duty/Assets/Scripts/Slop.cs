using UnityEngine;

public class Slop : MonoBehaviour
{
    private Color color = Color.white;
    private bool isSelected = false;

    [Header("Visual References")]
    [SerializeField] private GameObject outlineObject;
    [SerializeField] private GameObject ladleObject;
    [SerializeField] private SpriteRenderer slopSpriteRenderer;

    [Header("Coloring")]
    [Tooltip("On: the slop art is repainted to the day's color, so colored source art " +
             "such as a green blob still works. Off: the sprite is tinted instead, which " +
             "only looks right if the art is white or grey to begin with.")]
    [SerializeField] private bool recolorSprite = true;

    // The original artwork, captured before anything repaints it.
    private Sprite baseSprite;

    private void Awake()
    {
        if (slopSpriteRenderer == null)
        {
            // Only guess when there is exactly one renderer to guess at. Once the tray is
            // on the root and the slop blob is on a child, guessing wrong means recoloring
            // the tray and leaving the food permanently red, which looks like the recolor
            // is broken rather than like a wiring mistake.
            SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);

            if (all.Length == 1) slopSpriteRenderer = all[0];
            else Debug.LogError($"{name}: set Slop Sprite Renderer to the slop blob, not the container.", this);
        }

        if (slopSpriteRenderer != null) baseSprite = slopSpriteRenderer.sprite;
    }

    private void Start()
    {
        if (SlopLogic.Instance == null)
        {
            Debug.LogError($"{name}: no SlopLogic in the scene. Add the SlopLogic component to SlopManager.", this);
            return;
        }

        // Registering also assigns this pan a color, one index per pan,
        // so two pans on the counter never share a color.
        SlopLogic.Instance.AddSlopObject(this);
    }

    private void OnMouseDown()
    {
        SelectSlop();
    }

    public void SelectSlop()
    {
        if (SlopLogic.Instance == null) return;

        SetIsSelected(true);
        SlopLogic.Instance.SetSelectedSlop(this);
    }

    // --- GETTERS & SETTERS ---

    public Color GetColor() => color;

    public void SetColor(Color newColor)
    {
        color = newColor;

        if (slopSpriteRenderer == null) return;

        if (baseSprite == null) baseSprite = slopSpriteRenderer.sprite;

        if (recolorSprite && baseSprite != null)
        {
            Sprite painted = SpriteRecolor.For(baseSprite, newColor);

            if (painted != null)
            {
                // Repaint the pixels and leave the renderer tint neutral. Tinting on top
                // of a repaint would darken it, because renderer color multiplies.
                slopSpriteRenderer.sprite = painted;
                slopSpriteRenderer.color = Color.white;
                return;
            }

            // Repainting was not possible, so fall through to tinting rather than
            // leaving every pan a flat white.
        }

        slopSpriteRenderer.sprite = baseSprite;
        slopSpriteRenderer.color = newColor;
    }

    // The pixel shape marking this pan when the player has said they cannot rely on color.
    // Below zero hides it. Driven by SlopLogic, which knows which pan this is.
    public void ShowSymbol(int symbol) => SymbolBadge.Apply(slopSpriteRenderer, symbol, 0.34f);

    public bool GetIsSelected() => isSelected;

    public void SetIsSelected(bool selected)
    {
        isSelected = selected;

        if (outlineObject != null) outlineObject.SetActive(selected);
        if (ladleObject != null) ladleObject.SetActive(selected);
    }
}
