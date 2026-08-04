using UnityEngine;
using UnityEngine.UI;

// Works either as a world sprite with a Collider2D, or as a UI Button on a Canvas.
// Those are two completely separate click systems in Unity: OnMouseDown only fires on
// world colliders and never on UI, while UI goes through the EventSystem and Button.onClick.
// This component listens to both, so it does not matter which one you build.
//
// NOTE: the filename must stay SlopOutButton.cs. Unity requires a MonoBehaviour's
// filename to match its class name.
public class SlopOutButton : MonoBehaviour
{
    [Header("Optional visual feedback. Either one, found automatically if left empty.")]
    [SerializeField] private SpriteRenderer buttonRenderer;   // world sprite
    [SerializeField] private Image buttonImage;               // UI Image
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color pressedColor = new Color(1f, 0.7f, 0.3f);

    private bool slopOutPressed = false;

    private void Awake()
    {
        if (buttonRenderer == null) buttonRenderer = GetComponent<SpriteRenderer>();
        if (buttonImage == null) buttonImage = GetComponent<Image>();

        // If this is sitting on a UI Button, hook its click here so nobody has to
        // remember to wire the onClick list by hand in the Inspector.
        Button uiButton = GetComponent<Button>();
        if (uiButton != null) uiButton.onClick.AddListener(Toggle);
    }

    private void Start() => Refresh();

    // World-object path. Requires a Collider2D on this object.
    private void OnMouseDown() => Toggle();

    // UI path, and safe to call from anywhere.
    public void Toggle()
    {
        // Toggle rather than latch, so a mis-click can be taken back before picking a kid.
        SetSlopOutPressed(!slopOutPressed);

        // Claiming you are out and holding a pan are mutually exclusive answers.
        if (slopOutPressed && SlopLogic.Instance != null) SlopLogic.Instance.ClearSelection();
    }

    public bool GetSlopOutPressed() => slopOutPressed;

    public void SetSlopOutPressed(bool value)
    {
        slopOutPressed = value;
        Refresh();
    }

    // Call after a student has been resolved.
    public void ResetButton() => SetSlopOutPressed(false);

    private void Refresh()
    {
        Color c = slopOutPressed ? pressedColor : idleColor;

        if (buttonRenderer != null) buttonRenderer.color = c;
        if (buttonImage != null) buttonImage.color = c;
    }
}
