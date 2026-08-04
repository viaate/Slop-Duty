using UnityEngine;

public class SlopOutButton : MonoBehaviour
{
    private bool slopOutPressed = false;

    private void OnMouseDown()
    {
        SetSlopOutPressed(true);
    }

    public bool GetSlopOutPressed()
    {
        return slopOutPressed;
    }

    public void SetSlopOutPressed(bool value)
    {
        slopOutPressed = value;
    }

    // Call this at the end of the frame/turn to reset state
    public void ResetButton()
    {
        slopOutPressed = false;
    }
}