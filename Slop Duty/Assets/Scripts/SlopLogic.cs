using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SlopLogic : MonoBehaviour
{
    public static SlopLogic Instance;

    private List<Color> colors = new List<Color>();
    private List<Slop> slopObjects = new List<Slop>();
    
    private Slop selectedSlop;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // --- COLOR LIST METHODS ---
    public Color GetColor(int index)
    {
        if (index >= 0 && index < colors.Count)
            return colors[index];
        return Color.white;
    }

    public void SetColor(int red, int green, int blue)
    {
        // Unity uses normalized float values (0.0 to 1.0) for Color
        Color newColor = new Color(red / 255f, green / 255f, blue / 255f, 1f);
        colors.Add(newColor);
    }

    public int GetColorCount()
    {
        return colors.Count;
    }

    // --- SLOP OBJECT LIST METHODS ---
    public Slop GetSlopObject(int index)
    {
        if (index >= 0 && index < slopObjects.Count)
            return slopObjects[index];
        return null;
    }

    public void AddSlopObject(Slop slop)
    {
        if (!slopObjects.Contains(slop))
            slopObjects.Add(slop);
    }

    public List<Slop> GetSlopObjectsList()
    {
        return slopObjects;
    }

    // --- SELECTED SLOP METHODS ---
    public Slop GetSelectedSlop()
    {
        return selectedSlop;
    }

    public void SetSelectedSlop(Slop slop)
    {
        selectedSlop = slop;
        
        // Unselect all other slops
        foreach (Slop s in slopObjects)
        {
            if (s != slop)
            {
                s.SetIsSelected(false);
            }
        }
    }
}