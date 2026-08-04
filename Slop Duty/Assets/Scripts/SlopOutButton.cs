using UnityEngine;

// NOTE: this file must be called SlopOutButton.cs. Unity requires a MonoBehaviour's
// filename to match its class name, which is why the old SlopOut.cs could never be
// attached to a GameObject. Delete SlopOut.cs when you add this one.
public class SlopOutButton : MonoBehaviour
{
    [Header("Optional visual feedback")]
    [SerializeField] private SpriteRenderer buttonRenderer;
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color pressedColor = new Color(1f, 0.7f, 0.3f);

    private bool slopOutPressed = false;

    private void Start() => Refresh();

    private void OnMouseDown()
    {
        // Toggle, so a mis-click can be taken back before you pick a kid.
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
        if (buttonRenderer == null) return;
        buttonRenderer.color = slopOutPressed ? pressedColor : idleColor;
    }
}
