using UnityEngine;

public class Slop : MonoBehaviour
{
    private Color color;
    private bool isSelected = false;

    [Header("Visual References")]
    [SerializeField] private GameObject ladleObject;
    [SerializeField] private SpriteRenderer slopSpriteRenderer;

    private void Start()
    {
        // Register this object with SlopLogic
        if (SlopLogic.Instance != null)
        {
            SlopLogic.Instance.AddSlopObject(this);

            // Bug: this used to always call GetColor(0), so every slop bucket
            // in the scene ended up the exact same color. Use this bucket's own
            // registration order instead so each one gets a distinct color.
            int index = SlopLogic.Instance.GetSlopObjectsList().IndexOf(this);
            if (index >= 0 && index < SlopLogic.Instance.GetColorCount())
            {
                SetColor(SlopLogic.Instance.GetColor(index));
            }
        }
    }

    private void OnMouseDown()
    {
        // Triggered automatically by Unity when clicking this object's Collider
        SelectSlop();
    }

    public void SelectSlop()
    {
        SetIsSelected(true);
        SlopLogic.Instance.SetSelectedSlop(this);

        if (ladleObject != null) ladleObject.SetActive(true);
    }

    // --- GETTERS & SETTERS ---
    public Color GetColor()
    {
        return color;
    }

    public void SetColor(Color newColor)
    {
        color = newColor;
        if (slopSpriteRenderer != null)
        {
            slopSpriteRenderer.color = color;
        }
    }

    public bool GetIsSelected()
    {
        return isSelected;
    }

    public void SetIsSelected(bool selected)
    {
        isSelected = selected;

        if (!isSelected)
        {
            if (ladleObject != null) ladleObject.SetActive(false);
        }
    }
}
