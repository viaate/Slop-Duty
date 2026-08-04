using UnityEngine;

public class Slop : MonoBehaviour
{
    private Color color = Color.white;
    private bool isSelected = false;

    [Header("Visual References")]
    [SerializeField] private GameObject outlineObject;
    [SerializeField] private GameObject ladleObject;
    [SerializeField] private SpriteRenderer slopSpriteRenderer;

    private void Start()
    {
        if (SlopLogic.Instance == null)
        {
            Debug.LogError($"{name}: no SlopLogic in the scene. Add the SlopLogic component to SlopManager.", this);
            return;
        }

        // Registering also assigns this pan a colour, one index per pan,
        // so two pans on the counter never share a colour.
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
        if (slopSpriteRenderer != null) slopSpriteRenderer.color = color;
    }

    public bool GetIsSelected() => isSelected;

    public void SetIsSelected(bool selected)
    {
        isSelected = selected;

        if (outlineObject != null) outlineObject.SetActive(selected);
        if (ladleObject != null) ladleObject.SetActive(selected);
    }
}
