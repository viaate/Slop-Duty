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

    [Header("Counter")]
    [Tooltip("Extra pans are spawned from this when a harder day needs more colors " +
             "than there are pans in the scene. Leave empty to only ever use hand-placed pans.")]
    [SerializeField] private Slop slopPrefab;

    [Tooltip("Hard ceiling on pans, matching the highest slopColors any day asks for.")]
    [SerializeField, Range(1, 16)] private int maxPans = 10;

    [Tooltip("Spread the active pans evenly between these two, as offsets from this object. " +
             "Turn off to keep pans wherever they were placed by hand.")]
    [SerializeField] private bool autoLayout = true;
    [SerializeField] private float counterLeftX = -6f;
    [SerializeField] private float counterRightX = 6f;

    [Header("Color separation")]
    [Tooltip("Smallest perceptual gap allowed between any two pans, in OKLab, where black " +
             "to white is about 1.0. Under roughly 0.13 two pans start reading as the same " +
             "color when you are under time pressure.")]
    [SerializeField, Range(0.05f, 0.35f)] private float minColorGap = 0.17f;

    [Tooltip("Candidates tried per pan. Each is scored against every color already chosen " +
             "and the furthest one wins.")]
    [SerializeField, Range(8, 128)] private int candidateTries = 48;

    [Tooltip("Passes that find the closest pair in the finished palette and re-roll one of " +
             "them. Cheap, and it fixes the corners best-of-N paints itself into.")]
    [SerializeField, Range(0, 12)] private int repairPasses = 8;

    [Tooltip("How far a decoy color must sit from every pan, as a multiple of the gap " +
             "above. A decoy has to be obviously absent, not merely different.")]
    [SerializeField, Range(1f, 2.5f)] private float decoyGapMultiplier = 1.35f;

    private List<Color> colors = new List<Color>();
    private List<Slop> slopObjects = new List<Slop>();

    // The band every slop lives in, pans and decoys alike. Shared so the two can never
    // drift apart: a decoy drawn from a different range than the pans is identifiable on
    // sight, which defeats the entire point of it.
    private const float SatMin = 0.55f;
    private const float SatMax = 0.95f;
    private const float ValMin = 0.65f;
    private const float ValMax = 1.00f;

    // Below this two slops stop being tellable apart at a glance. minColorGap is what the
    // generator aims for; this is where a palette is actually broken.
    private const float ReadableFloor = 0.12f;

    private static readonly HashSet<int> warnedCounts = new HashSet<int>();

    private Slop selectedSlop;
    private int activeCount;
    private float wheelOffset;

    private static Color RandomSlopColor(float hue) =>
        Color.HSVToRGB(hue, Random.Range(SatMin, SatMax), Random.Range(ValMin, ValMax));

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

    // Three things working together, because any one alone is not enough.
    //
    // 1. Hue stratification. The wheel is cut into `count` sectors and each pan draws from
    //    its own, so a minimum hue gap is structural rather than lucky.
    // 2. Best of N in OKLab. Within its sector each color is the candidate furthest from
    //    everything already chosen. Hue gap alone is a bad proxy: 36 degrees in the blues
    //    is nearly indistinguishable while 36 degrees in the yellows is obvious.
    // 3. A repair pass that hunts the closest pair and re-rolls one of them, keeping the
    //    result only when the weakest link in the whole palette actually improves.
    //
    // Do not simplify this back to a single random draw per sector. That is what produced
    // a blue and a blue-violet sitting next to each other.
    public void GeneratePalette(int count)
    {
        if (slopObjects.Count == 0) CollectSlops();

        // Build any pans this day needs but the scene does not have yet.
        EnsurePanCount(Mathf.Min(count, maxPans));

        // The palette can never be larger than the number of pans actually on the counter.
        // If it were, students would ask for colors that physically are not there, every
        // one of those would count as "we're out", and the game would be unplayable.
        activeCount = Mathf.Clamp(count, 1, Mathf.Max(1, Mathf.Min(slopObjects.Count, maxPans)));

        colors.Clear();
        wheelOffset = Random.value;

        for (int i = 0; i < activeCount; i++)
            colors.Add(BestCandidate(i));

        Repair();

        // Warns rather than silently shipping a palette nobody can read.
        //
        // Deliberately not triggered by merely missing minColorGap. That target is
        // unreachable by definition once the counter fills up: eight hues cannot all sit
        // 0.17 apart on a wheel, and measured over 200 runs eight pans land a median of
        // 0.169. Warning on the target itself meant a warning on about half of all late
        // days, which reads as a bug every time you look at the console.
        //
        // ReadableFloor is the point where two pans genuinely start looking alike, and it
        // is only reached when something is actually misconfigured. Once per pan count, so
        // a bad setting is still visible without repeating every day.
        if (ClosestPair(out _, out _, out float achieved) && achieved < ReadableFloor
            && warnedCounts.Add(activeCount))
        {
            Debug.LogWarning($"{name}: only reached a color gap of {achieved:0.000} with " +
                             $"{activeCount} pans, which is close enough that two of them " +
                             "will look alike. Use fewer pans.", this);
        }

        ApplyToCounter();
    }

    // Best of N inside this pan's hue sector, scored by how far it sits from every color
    // already chosen.
    private Color BestCandidate(int index)
    {
        Color best = RandomInSector(index);
        float bestGap = NearestGap(best);

        for (int attempt = 1; attempt < candidateTries; attempt++)
        {
            Color candidate = RandomInSector(index);
            float gap = NearestGap(candidate);

            if (gap <= bestGap) continue;

            bestGap = gap;
            best = candidate;

            // Comfortably clear already, no point burning the rest of the budget.
            if (bestGap >= minColorGap * 1.5f) break;
        }

        return best;
    }

    private Color RandomInSector(int index)
    {
        // A quarter of the sector rather than a third, so stratification still means
        // something before the perceptual pass gets involved.
        float jitter = Random.Range(-0.25f, 0.25f) / activeCount;
        float hue = Mathf.Repeat(wheelOffset + (index / (float)activeCount) + jitter, 1f);

        // Lightness varies within the shared band so neighbouring hues can separate on
        // brightness as well as colour. The floor is deliberately not lower: the renderer
        // multiplies this by the sprite's own shading, which bottoms out around 0.69, so a
        // target of 0.65 already lands near 0.45 on screen and anything under that reads
        // as mud rather than as food.
        return RandomSlopColor(hue);
    }

    private float NearestGap(Color candidate)
    {
        float nearest = float.MaxValue;

        for (int i = 0; i < colors.Count; i++)
            nearest = Mathf.Min(nearest, Distance(candidate, colors[i]));

        return nearest;
    }

    private void Repair()
    {
        for (int pass = 0; pass < repairPasses; pass++)
        {
            if (!ClosestPair(out _, out int worst, out float gap)) return;
            if (gap >= minColorGap) return;

            Color original = colors[worst];
            colors.RemoveAt(worst);

            colors.Insert(worst, BestCandidate(worst));

            ClosestPair(out _, out _, out float after);
            if (after > gap) continue;

            // The re-roll was no better, so put the original back rather than churning.
            colors[worst] = original;
        }
    }

    private bool ClosestPair(out int a, out int b, out float gap)
    {
        a = -1;
        b = -1;
        gap = float.MaxValue;

        for (int i = 0; i < colors.Count; i++)
        {
            for (int j = i + 1; j < colors.Count; j++)
            {
                float d = Distance(colors[i], colors[j]);
                if (d >= gap) continue;

                gap = d;
                a = i;
                b = j;
            }
        }

        return a >= 0;
    }

    private void ApplyToCounter()
    {
        for (int i = 0; i < slopObjects.Count; i++)
        {
            Slop s = slopObjects[i];
            if (s == null) continue;

            bool on = i < activeCount;
            s.gameObject.SetActive(on);

            if (!on) continue;

            // Re-spread every time the count changes, otherwise adding a seventh pan
            // leaves a gap at one end and a pile-up at the other.
            if (autoLayout) s.transform.position = PanPosition(i, activeCount);

            s.SetColor(GetColor(i));

            // Handed the loop index rather than looked up from the color, because the
            // colors are still being written as this loop runs: asking "which pan is this
            // color" halfway through would consult a counter that is only half painted.
            s.ShowSymbol(Settings.UseSymbols ? i : -1);
        }

        // A new day repaints every pan, so any held scoop is now the wrong color.
        // Each pan's own flag has to be cleared too, not just this reference, or a pan
        // keeps claiming it is selected and the player can serve with a stale color.
        ClearSelection();
    }

    // Without this the color lever silently caps at however many SlopType objects
    // somebody happened to drag into the scene, so the difficulty stops ramping.
    private void EnsurePanCount(int wanted)
    {
        if (slopPrefab == null || wanted <= slopObjects.Count) return;

        while (slopObjects.Count < wanted)
        {
            Slop pan = Instantiate(slopPrefab, transform);
            pan.name = $"SlopType ({slopObjects.Count})";
            slopObjects.Add(pan);
        }
    }

    private Vector3 PanPosition(int index, int total)
    {
        float offset = total <= 1
            ? (counterLeftX + counterRightX) * 0.5f
            : Mathf.Lerp(counterLeftX, counterRightX, index / (float)(total - 1));

        return transform.position + new Vector3(offset, 0f, 0f);
    }

    private void OnDrawGizmos()
    {
        if (!autoLayout) return;

        Gizmos.color = Color.magenta;

        int preview = Mathf.Clamp(Application.isPlaying ? activeCount : startingColors, 1, maxPans);
        for (int i = 0; i < preview; i++)
            Gizmos.DrawWireCube(PanPosition(i, preview), new Vector3(0.8f, 0.5f, 0f));
    }

    public Color GetColor(int index)
    {
        if (colors.Count == 0) return Color.white;

        int wrapped = ((index % colors.Count) + colors.Count) % colors.Count;
        return colors[wrapped];
    }

    public Color GetRandomColor() => GetColor(Random.Range(0, Mathf.Max(1, colors.Count)));

    public int GetColorCount() => colors.Count;

    // True if a pan that is currently switched on is this color.
    public bool IsOnCounter(Color wanted)
    {
        for (int i = 0; i < slopObjects.Count && i < activeCount; i++)
        {
            if (slopObjects[i] == null) continue;
            if (slopObjects[i].GetColor() == wanted) return true;
        }

        return false;
    }

    // Which pixel shape belongs to a color, or -1 when shapes are switched off.
    //
    // Colors on the counter get their own pan's index, so a shape on a pan and the same
    // shape in a kid's thought bubble mean the same slop.
    //
    // A color that is NOT on the counter still gets a shape, taken from the pool beyond the
    // pans in use. That matters more than it looks: leave a decoy with no badge at all and
    // the empty bubble answers the question before the player has compared anything, which
    // turns the one case the sorry-we're-out button exists for into a freebie. This way the
    // decoy has a shape like everyone else, and it simply is not one of the shapes on the
    // counter, which is the same job the colors were doing.
    public int SymbolFor(Color wanted)
    {
        if (!Settings.UseSymbols) return -1;

        for (int i = 0; i < slopObjects.Count && i < activeCount; i++)
        {
            if (slopObjects[i] == null) continue;
            if (slopObjects[i].GetColor() == wanted) return i;
        }

        int spare = Mathf.Max(1, SlopSymbols.Count - activeCount);

        // Derived from the color itself rather than rolled, so the same kid keeps the same
        // shape for as long as they are standing there.
        int hash = Mathf.Abs(wanted.GetHashCode());

        return activeCount + (hash % spare);
    }

    // A color deliberately unlike anything on the counter, for the "sorry we're out" case.
    //
    // Held to a higher bar than the pans are held to each other, because a decoy that is
    // merely different is a coin flip under pressure, while a decoy that is obviously
    // absent is a fair test. Chosen at random from everything that clears the bar rather
    // than always taking the furthest, so it is not a learnable fixed hue.
    public Color GetOffPaletteColor()
    {
        float required = minColorGap * decoyGapMultiplier;

        List<Color> qualifying = new List<Color>();
        Color best = Color.white;
        float bestGap = -1f;

        // Drawn from exactly the same band as the pans, and simply tried many more times.
        //
        // An earlier version let decoys roam a much wider range of saturation and
        // brightness so they could escape a crowded hue wheel along another axis. It
        // worked far too well: going dark is the cheapest way to be far from everything,
        // so decoys came out near-black nearly every time and became identifiable at a
        // glance. A decoy you can spot without reading its colour is not a decoy.
        //
        // The honest consequence is that on a crowded counter a decoy genuinely cannot be
        // both plausible and obviously distinct. That is difficulty, not a defect. If late
        // days feel unfair, lower Max Pans rather than widening this.
        for (int attempt = 0; attempt < 160; attempt++)
        {
            Color candidate = RandomSlopColor(Random.value);
            float gap = NearestGap(candidate);

            if (gap > bestGap)
            {
                bestGap = gap;
                best = candidate;
            }

            if (gap >= required) qualifying.Add(candidate);
        }

        if (qualifying.Count > 0) return qualifying[Random.Range(0, qualifying.Count)];

        // Nothing cleared the bar, which means the counter is crowded. Take the furthest
        // we found rather than handing out something indistinguishable.
        return best;
    }

    // OKLab, not RGB. RGB distance badly misjudges how different two colors look: it rates
    // a blue against a blue-violet as further apart than a yellow against an orange, which
    // is the opposite of what the eye reports. OKLab is built to be perceptually uniform,
    // so one threshold behaves the same everywhere on the wheel.
    private static float Distance(Color a, Color b)
    {
        ToOkLab(a, out float l1, out float a1, out float b1);
        ToOkLab(b, out float l2, out float a2, out float b2);

        float dl = l1 - l2;
        float da = a1 - a2;
        float db = b1 - b2;

        return Mathf.Sqrt((dl * dl) + (da * da) + (db * db));
    }

    private static void ToOkLab(Color c, out float lightness, out float greenRed, out float blueYellow)
    {
        // Measured as the PLAYER sees it, not as the screen emits it.
        //
        // Everything that decides whether two slops are far enough apart runs through here:
        // the best-of-48 search, the repair pass, and the bar a decoy has to clear. Putting
        // the simulation at this one point means all of it starts avoiding pairs that this
        // particular pair of eyes cannot separate, with no other change anywhere.
        //
        // The blunt alternative, banning red or banning green, would have been worse on
        // both counts: which hues collide depends on the type, and a hue that is fine on
        // its own can still collide with one specific neighbour. Measuring beats guessing.
        c = Colorblind.Simulate(c, Settings.Vision);

        float r = ToLinear(c.r);
        float g = ToLinear(c.g);
        float bl = ToLinear(c.b);

        float l = (0.4122214708f * r) + (0.5363325363f * g) + (0.0514459929f * bl);
        float m = (0.2119034982f * r) + (0.6806995451f * g) + (0.1073969566f * bl);
        float s = (0.0883024619f * r) + (0.2817188376f * g) + (0.6299787005f * bl);

        float lc = Cbrt(l);
        float mc = Cbrt(m);
        float sc = Cbrt(s);

        lightness = (0.2104542553f * lc) + (0.7936177850f * mc) - (0.0040720468f * sc);
        greenRed = (1.9779984951f * lc) - (2.4285922050f * mc) + (0.4505937099f * sc);
        blueYellow = (0.0259040371f * lc) + (0.7827717662f * mc) - (0.8086757660f * sc);
    }

    private static float ToLinear(float channel)
    {
        return channel <= 0.04045f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
    }

    private static float Cbrt(float value)
    {
        return value <= 0f ? 0f : Mathf.Pow(value, 1f / 3f);
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
