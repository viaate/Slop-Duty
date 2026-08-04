using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
SlopLogic class:
public fv for slop selected

** posibly use methods to get/set instead of properties
private static(?) fv list for colors -
    use property Colors to get/set color
    list of type Color
    get(int index) - return Color[index]
    set(int red, int green, int blue) - uses Color.FromArgb & sets color at the end of list
private static(?) fv list for slop objects -
    use property SlopObjects to get/set slop objects
    list of type GameObject(?)
    get(int index) - return SlopObjects[index]
    set(color) - adds to end of list w/ assigned list


Slop individual/prefab class:
fv for color
fv for is selected
constructor -
    obtain color from SlopLogic & is passed in

in update() -
if Input.GetMouseButtonDown(0) then
    create outline around slop & bucket/holder
    show ladel w/ slop of said color
    set fv is selected to true
** either use a method to return is selected or make public & accesible from other classes


Student prefab class:
fv for assignedColor - call generateColor() method that generates it
fv to make student stay (until they get slop)
fv for satisfied (if get slop of assignedColor)
fv static for levelTimer - timer stays consistent across all students
fv for studentTimer = 10
**IMPLEMENT STUDENT TIMER

generateColor() -
    generate random num
    if num is 0-0.05 then
        generate random color using Color.FromArgb
            use random num for red & green & blue
            assign to respective in Color.FromArgb
    else
        select random color from list of colors in Slop prefab class

in update() -
Color selectedColor - the color of the slop that got selected
if Input.GetMouseButtonDown(0) & slop selected fv from Slop prefab class is true then
    for loop that goes through all slop objects in Sloplogic class
        find the slop object that is selected is true for
        when found, break out of loop & assign color to selectedColor
    if student assignedColor = selectedcolor then
        if slop out button class. slop out pressed fv is true
            satisfied fv = false;
        set satisfied fv to true
    else
        bool exists = false;
        for loop that goes through all slop objects
            if student assigned color is in loop
                exists = true;
                break out of loop
        if exists = false then
            if slop out button class. slop out pressed fv is true
                satisfied fv = true;
            else
                satisfied fv = false;
        else
            if slop out button class. slop out pressed fv is true
                satisfied fv = false;
            else
                satisfied fv = true;
    
    if satisfied fv = true
        levelTimer.addTime(5);
        walk away
    else
        levelTimer.subtractTime(5);
        

slop out button class:
public fv for slop out pressed

in Update() -
    if Input.GetMouseButtonDown(0)
        slop out pressed = true;


Timer class:
public fv for time remaining - is initially 60 sec (? maybe)
positive time scale = 1;
negative time scale = 1;
** pos & neg time scale should change as levels progress

in addTime(int timeToAdd) -
    time remaining += timeToAdd*positive time scale;

in subtractTime(int timeToSubtract) -
    time remaining -= timeToSubtract*negative time scale;
*/

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