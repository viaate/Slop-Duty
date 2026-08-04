using UnityEngine;

// Renamed from SlopOut.cs -> SlopOutButton.cs. In Unity the file name has to
// match the MonoBehaviour class name (SlopOutButton) or you'll get a
// "script class cannot be found" error the moment you try to (re)attach it.
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
