using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Lights a menu button up on hover, changing both its fill and its label.
//
// Unity's built-in Button transition cannot do this. Its ColorBlock only tints the
// targetGraphic, which is the background Image, so the text would stay dim while the box
// behind it brightened. Doing it with pointer events instead lets both move together.
public class HoverTint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private Image fill;
    [SerializeField] private TextMeshProUGUI label;

    [SerializeField] private Color idleColor = Color.grey;
    [SerializeField] private Color hoverColor = Color.white;

    [SerializeField, Range(0f, 1f)] private float idleFillAlpha = 0.14f;
    [SerializeField, Range(0f, 1f)] private float hoverFillAlpha = 0.30f;

    // So a caller can re-run Setup with a different idle color without having to hold on
    // to the pieces itself. Used for buttons that are also a choice, where the selected one
    // has to stay lit rather than only lighting under the pointer.
    public Image Fill => fill;
    public TextMeshProUGUI Label => label;

    public void Setup(Image background, TextMeshProUGUI text, Color idle, Color hover)
    {
        fill = background;
        label = text;
        idleColor = idle;
        hoverColor = hover;

        Apply(idleColor, idleFillAlpha);
    }

    private void Start() => Apply(idleColor, idleFillAlpha);

    public void OnPointerEnter(PointerEventData eventData) => Apply(hoverColor, hoverFillAlpha);

    public void OnPointerExit(PointerEventData eventData) => Apply(idleColor, idleFillAlpha);

    private void Apply(Color color, float alpha)
    {
        if (fill != null) fill.color = new Color(color.r, color.g, color.b, alpha);
        if (label != null) label.color = color;
    }
}
