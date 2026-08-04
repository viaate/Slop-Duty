using UnityEngine;

public class Slop : MonoBehaviour
{
    private Color color;
    private bool isSelected = false;

    [Header("Visual References")]
    [SerializeField] private GameObject outlineObject;
    [SerializeField] private GameObject ladleObject;
    [SerializeField] private SpriteRenderer slopSpriteRenderer;

    private void Start()
    {
        // Register this object with SlopLogic
        if (SlopLogic.Instance != null)
        {
            SlopLogic.Instance.AddSlopObject(this);
            
            // Assign a color if available
            if (SlopLogic.Instance.GetColorCount() > 0)
            {
                SetColor(SlopLogic.Instance.GetColor(0));
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

        // Visual feedback
        if (outlineObject != null) outlineObject.SetActive(true);
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
        
        // Hide outline and ladle if unselected
        if (!isSelected)
        {
            if (outlineObject != null) outlineObject.SetActive(false);
            if (ladleObject != null) ladleObject.SetActive(false);
        }
    }
}