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

    [Tooltip("Used only if there is no GameManager driving the day. GameManager overrides this.")]
    [SerializeField, Range(1, 10)] private int startingColors = 3;

    private List<Color> colors = new List<Color>();
    private List<Slop> slopObjects = new List<Slop>();

    private Slop selectedSlop;
    private int activeCount;

    // How many pans are currently switched on. This is the brief's third difficulty
    // lever, and it is capped by how many SlopType objects exist in the scene.
    public int ActivePanCount => activeCount;
    public int TotalPanCount => slopObjects.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        CollectSlops();
        GeneratePalette(startingColors);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Gathered here rather than waiting for each Slop to register itself in Start,
    // because Awake on this object runs before any Start, so the palette would
    // otherwise be generated against an empty counter.
    private void CollectSlops()
    {
        Slop[] found = FindObjectsByType<Slop>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        slopObjects.Clear();
        slopObjects.AddRange(found);

        // Left to right, so switching pans on and off fills the counter predictably.
        slopObjects.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
    }

    // --- PALETTE ---

    // Hue stratified: the colour wheel is cut into `count` sectors and each colour is
    // drawn from its own sector. That makes a minimum hue gap structural instead of lucky,
    // which is what stops two near-identical greens landing on the counter together.
    public void GeneratePalette(int count)
    {
        if (slopObjects.Count == 0) CollectSlops();

        // The palette can never be larger than the number of pans actually on the counter.
        // If it were, students would ask for colours that physically are not there, every
        // one of those would count as "we're out", and the game would be unplayable.
        activeCount = Mathf.Clamp(count, 1, Mathf.Max(1, slopObjects.Count));

        colors.Clear();
        float wheelOffset = Random.value;

        for (int i = 0; i < activeCount; i++)
        {
            // Jitter stays inside the sector so colours can never cross into each other.
            float jitter = Random.Range(-0.35f, 0.35f) / activeCount;
            float hue = Mathf.Repeat(wheelOffset + (i / (float)activeCount) + jitter, 1f);

            float saturation = Random.Range(0.55f, 0.95f);
            float value = Random.Range(0.45f, 0.90f);

            colors.Add(Color.HSVToRGB(hue, saturation, value));
        }

        ApplyToCounter();
    }

    private void ApplyToCounter()
    {
        for (int i = 0; i < slopObjects.Count; i++)
        {
            Slop s = slopObjects[i];
            if (s == null) continue;

            bool on = i < activeCount;
            s.gameObject.SetActive(on);

            if (on) s.SetColor(GetColor(i));
        }

        selectedSlop = null;
    }

    public Color GetColor(int index)
    {
        if (colors.Count == 0) return Color.white;

        int wrapped = ((index % colors.Count) + colors.Count) % colors.Count;
        return colors[wrapped];
    }

    public Color GetRandomColor() => GetColor(Random.Range(0, Mathf.Max(1, colors.Count)));

    public int GetColorCount() => colors.Count;

    // True if a pan that is currently switched on is this colour.
    public bool IsOnCounter(Color wanted)
    {
        for (int i = 0; i < slopObjects.Count && i < activeCount; i++)
        {
            if (slopObjects[i] == null) continue;
            if (slopObjects[i].GetColor() == wanted) return true;
        }

        return false;
    }

    // A colour deliberately unlike anything on the counter, for the "sorry we're out" case.
    // Best of 40 candidates rather than the first random one, so the answer is never
    // a colour the player could mistake for a pan they actually have.
    public Color GetOffPaletteColor()
    {
        Color best = Color.white;
        float bestGap = -1f;

        for (int attempt = 0; attempt < 40; attempt++)
        {
            Color candidate = Color.HSVToRGB(Random.value, Random.Range(0.55f, 0.95f), Random.Range(0.45f, 0.90f));

            float nearest = float.MaxValue;
            for (int i = 0; i < colors.Count; i++)
                nearest = Mathf.Min(nearest, SquaredDistance(candidate, colors[i]));

            if (nearest <= bestGap) continue;

            bestGap = nearest;
            best = candidate;
        }

        return best;
    }

    private static float SquaredDistance(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return (dr * dr) + (dg * dg) + (db * db);
    }

    // --- SLOP OBJECT LIST ---

    public Slop GetSlopObject(int index)
    {
        if (index >= 0 && index < slopObjects.Count) return slopObjects[index];
        return null;
    }

    public void AddSlopObject(Slop slop)
    {
        if (slop == null || slopObjects.Contains(slop)) return;

        slopObjects.Add(slop);
        slopObjects.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        ApplyToCounter();
    }

    public List<Slop> GetSlopObjectsList() => slopObjects;

    // --- SELECTION ---

    public Slop GetSelectedSlop() => selectedSlop;

    public void SetSelectedSlop(Slop slop)
    {
        selectedSlop = slop;

        for (int i = 0; i < slopObjects.Count; i++)
        {
            if (slopObjects[i] == null || slopObjects[i] == slop) continue;
            slopObjects[i].SetIsSelected(false);
        }
    }

    public void ClearSelection()
    {
        selectedSlop = null;

        for (int i = 0; i < slopObjects.Count; i++)
        {
            if (slopObjects[i] == null) continue;
            slopObjects[i].SetIsSelected(false);
        }
    }
}
