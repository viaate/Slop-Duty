using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The Sunday training shift, told through speech bubbles parked in the middle of the
// counter.
//
// Runs on day 0 only and does nothing on any other day. Sunday cannot be lost: there is no
// clock, and DayConfig.Sunday sets endlessPatience so nobody ever walks off. That is what
// makes a tutorial possible at all, because it means a bubble can sit on screen for as
// long as somebody wants to read it.
//
// The beats are driven by watching the game rather than by a timer. A tooltip that says
// "click a pan" while the player has already clicked one is worse than no tooltip, so each
// step waits for the thing it is describing to actually be true.
public class Tutorial : MonoBehaviour
{
    [Header("Bubble art")]
    [Tooltip("Assign the -alt sprites, the ones with no tail. Only the alt art is a closed " +
             "shape: the tailed versions have their bottom row broken open where the tail " +
             "joins. The widest one gets used for every tooltip, so listing all three is " +
             "fine and the narrow ones simply sit unused.")]
    [SerializeField] private SpeechBubble.Style[] styles = new SpeechBubble.Style[1];

    [Header("Text")]
    [Tooltip("Leave empty and it borrows the font off GameUI, which is almost always what " +
             "you want. Only fill this in to give tooltips a different face on purpose.")]
    [SerializeField] private TMP_FontAsset pixelFont;

    [SerializeField] private Color textColor = new Color(0.129f, 0.121f, 0.098f);

    [Tooltip("Point size of the writing. Smaller text means a smaller bubble too, since " +
             "the bubble is only ever as big as the words need.")]
    [SerializeField] private int textPointSize = 28;

    [Tooltip("Widest a bubble may get before the writing wraps, in 1080p reference pixels.")]
    [SerializeField] private float bubbleMaxWidth = 900f;

    [Header("Emphasis")]
    [Tooltip("Looks for words wrapped in *asterisks*. Each marked word takes the next entry " +
             "and the list carries on across tooltips, so no two lines in a row come out " +
             "the same. Colors are picked to sit on the pale tray without competing with " +
             "the slop colors on the counter.")]
    [SerializeField]
    private Emphasis[] emphasis =
    {
        new Emphasis { color = new Color(0.647f, 0.212f, 0.145f), effect = TextEffect.Wave },
        new Emphasis { color = new Color(0.145f, 0.298f, 0.588f), effect = TextEffect.Pop },
        new Emphasis { color = new Color(0.804f, 0.435f, 0.106f), effect = TextEffect.Shake },
        new Emphasis { color = new Color(0.451f, 0.180f, 0.478f), effect = TextEffect.Tilt },
    };

    [Tooltip("Shortest a tooltip may stay up before the next one is allowed to replace it. " +
             "Beats fire off game events, and two of them landing close together used to " +
             "flash a line by before it could be read.")]
    [SerializeField] private float minShowSeconds = 2.6f;

    [Tooltip("How far highlighted letters bob, as a fraction of the point size. Kept small " +
             "on purpose: this is meant to catch the eye, not to be read through.")]
    [SerializeField, Range(0f, 0.5f)] private float highlightWobble = 0.14f;

    [SerializeField, Range(1f, 14f)] private float highlightSpeed = 5f;

    [Tooltip("Extra room around the writing, in whole scale steps. 0 is the tightest fit " +
             "the text allows. Each step grows the bubble only, never the text, and stays " +
             "on whole pixels so it cannot go soft.")]
    [SerializeField, Range(0, 6)] private int bubbleRoom = 1;

    // Short on purpose. The bubble is scaled whole, so it can only grow to fit the writing,
    // never reshape around it: every extra clause makes the bubble physically bigger until
    // it is covering the counter it is trying to explain. One idea per bubble.
    //
    // Wrap a word in *asterisks* to color it and make it bob. One or two per line at most.
    // The whole point is that the marked word is the thing to do, and marking half the
    // sentence means marking nothing.
    [Header("Wording")]
    [SerializeField, TextArea(2, 3)] private string sayWelcome =
        "*Sunday.* Nobody's in. Good day to learn.";

    [SerializeField, TextArea(2, 3)] private string sayPans =
        "Click a *pan* to load that color.";

    [SerializeField, TextArea(2, 3)] private string sayServe =
        "*Match* their color, then click them.";

    [SerializeField, TextArea(2, 3)] private string sayLadle =
        "The ladle *keeps* its color. Serve a whole run.";

    [SerializeField, TextArea(2, 3)] private string sayOutOfStock =
        "You don't have this one. Hit *WE'RE OUT*.";

    [SerializeField, TextArea(2, 3)] private string sayMop =
        "Wrong color. Click the *puddle* to mop it.";

    [SerializeField, TextArea(2, 3)] private string sayDone =
        "That's the job. Stay *as long as you like*.";

    [Tooltip("Seconds after the shift begins before the first bubble, so it does not " +
             "collide with the day card.")]
    [SerializeField] private float openDelay = 2.4f;

    [Tooltip("How far above the row of pans a tooltip sits, in world units. Raise it if a " +
             "bubble still overlaps the colors it is asking you to compare.")]
    [SerializeField] private float tooltipLift = 2.6f;

    private enum Step
    {
        Waiting,       // shift has not begun
        Welcome,
        Pans,
        Serve,
        Ladle,
        OutOfStock,
        Done,
        Off,           // any day but Sunday
    }

    private GameManager game;
    private StudentQueue queue;
    private SlopLogic slop;
    private Camera worldCam;

    private Canvas canvas;
    private RectTransform canvasRect;
    private SpeechBubble bubble;

    private Step step = Step.Waiting;
    private float clock;
    private bool taughtMopping;

    private int emphasisCursor;
    private float shownFor;
    private string queued;
    private Vector3? queuedAt;
    private Vector3? anchorAt;
    private Vector2 lastCanvasSize;

    private void Awake() => BuildCanvas();

    private void Start()
    {
        game = GameManager.Instance;
        if (game == null) game = FindFirstObjectByType<GameManager>();

        queue = FindFirstObjectByType<StudentQueue>();
        slop = SlopLogic.Instance;
        if (slop == null) slop = FindFirstObjectByType<SlopLogic>();

        if (game == null)
        {
            step = Step.Off;
            return;
        }

        game.DayStarted += OnDayStarted;
        game.StudentResolved += OnStudentResolved;

        OnDayStarted(game.Day);
    }

    private void OnDestroy()
    {
        if (game == null) return;

        game.DayStarted -= OnDayStarted;
        game.StudentResolved -= OnStudentResolved;
    }

    private void OnDayStarted(int day)
    {
        Dismiss();

        // Monday onwards the tutorial is simply gone. Nothing is disabled or hidden, there
        // is just no step left that does anything.
        if (day != 0)
        {
            step = Step.Off;
            return;
        }

        step = Step.Waiting;
        clock = 0f;
        taughtMopping = false;
    }

    private void OnStudentResolved(bool correct)
    {
        if (step == Step.Off) return;

        // Reactive, and deliberately allowed to interrupt whatever else is on screen. A
        // player who has just made a mess is looking at the mess, and this is the only
        // moment where explaining it will land.
        if (!correct && !taughtMopping)
        {
            taughtMopping = true;
            SayAt(sayMop, MessPoint());

            // The step is deliberately not advanced, so whatever was being taught is still
            // the current lesson and the next kid of that kind still counts toward it. This
            // does replace the instruction that was on screen, which is the right trade:
            // the player has already read it once, and the mess is the more urgent thing.
            return;
        }

        if (step == Step.Serve && correct) Advance(Step.Ladle);
        else if (step == Step.OutOfStock && correct) Advance(Step.Done);
    }

    private void Update()
    {
        if (step == Step.Off) return;

        clock += Time.unscaledDeltaTime;
        shownFor += Time.unscaledDeltaTime;

        // The pans can be relaid out when a palette regenerates, so the parking spot is
        // recomputed rather than cached.
        // Only recomputed when the window changes shape.
        //
        // The spot a tooltip parks in does not move otherwise: the pans are only relaid out
        // when a palette regenerates, and that happens on a day change, which dismisses the
        // bubble anyway. Doing it every frame meant a WorldToScreenPoint, a rectangle
        // conversion and a canvas write, all to arrive at last frame's answer.
        if (bubble != null && canvasRect != null && canvasRect.rect.size != lastCanvasSize)
        {
            lastCanvasSize = canvasRect.rect.size;
            Reposition();
        }

        // Whatever was held back while the last line served its minimum.
        if (queued != null && shownFor >= minShowSeconds) Show(queued, queuedAt);

        switch (step)
        {
            case Step.Waiting:
                if (clock >= openDelay) Advance(Step.Welcome);
                break;

            case Step.Welcome:
                if (clock >= 4f) Advance(Step.Pans);
                break;

            case Step.Pans:
                // Held until somebody is actually standing there, so the instruction to
                // serve never arrives before there is anybody to serve.
                if (FrontWaiting() != null) Advance(Step.Serve);
                break;

            case Step.Serve:
            case Step.Ladle:
                WatchForOutOfStock();
                break;
        }
    }

    // The scripted kid who wants a color that is not on the counter. GameManager guarantees
    // one appears on Sunday rather than leaving it to a 12% roll.
    private void WatchForOutOfStock()
    {
        IndividualStudent front = FrontWaiting();
        if (front == null || !front.WantsSomethingMissing) return;

        Advance(Step.OutOfStock);
    }

    private IndividualStudent FrontWaiting()
    {
        if (queue == null || queue.Front == null) return null;

        IndividualStudent kid = queue.Front.GetComponent<IndividualStudent>();
        if (kid == null || !kid.Waiting) return null;

        return kid;
    }

    private void Advance(Step next)
    {
        step = next;
        clock = 0f;

        switch (next)
        {
            case Step.Welcome:
                Say(sayWelcome);
                break;

            case Step.Pans:
                Say(sayPans);
                break;

            case Step.Serve:
                Say(sayServe);
                break;

            case Step.Ladle:
                Say(sayLadle);
                break;

            case Step.OutOfStock:
                Say(sayOutOfStock);
                break;

            case Step.Done:
                Say(sayDone);
                break;
        }
    }

    // Held back rather than shown immediately when the one on screen is still new.
    //
    // The beats fire off game events, and two landing close together used to flash a line
    // past before anybody could read it. Queueing means the newer line still wins, it just
    // waits its turn. Only one is ever held, so a burst collapses to the last thing said
    // rather than making the player sit through a backlog.
    private void Say(string text) => SayAt(text, null);

    // at is where to park it, or null for the usual spot in the middle of the counter.
    private void SayAt(string text, Vector3? at)
    {
        if (bubble != null && shownFor < minShowSeconds)
        {
            queued = text;
            queuedAt = at;
            return;
        }

        Show(text, at);
    }

    private void Show(string text, Vector3? at)
    {
        // Dismiss FIRST, then set the anchor. Dismiss clears it as part of tearing down the
        // last bubble, so setting it beforehand would wipe it a line later and every tooltip
        // would fall back to the default spot no matter what it asked for.
        Dismiss();
        anchorAt = at;

        SpeechBubble.Style style = WidestStyle();
        if (style == null) return;

        queued = null;
        shownFor = 0f;

        GameObject go = new GameObject("Tooltip", typeof(RectTransform));
        bubble = go.AddComponent<SpeechBubble>();
        bubble.Build(canvasRect, style, Font(), textColor, textPointSize, bubbleMaxWidth, bubbleRoom);

        // Before SetText, not after. The markers turn into color tags during sizing, and
        // the tagged string is what has to be measured, or the character positions the
        // wobble reads would belong to a different sentence than the one on screen.
        bubble.SetFlair(emphasis, emphasisCursor, highlightWobble, highlightSpeed);
        bubble.SetText(text);

        // Walked on per tooltip, not per word, so a line with one marked word still looks
        // different from the line before it.
        emphasisCursor++;

        Reposition();
    }

    private void Reposition()
    {
        if (bubble == null || canvasRect == null) return;

        bubble.PlaceOn(ToCanvas(anchorAt ?? CounterCentre()), canvasRect.rect.size);
    }

    // Always the widest bubble on the list, never a rotation through them.
    //
    // The bubble is scaled whole, so the writing has to fit the shape the art already is.
    // A sentence in the 32 by 12 bubble is two comfortable lines. The same sentence in the
    // nearly square one, or in the cloud, becomes a five line column in a blob that covers
    // the counter. Rotating through all three meant most of the tutorial got the wrong
    // shape, and the wrong shape is not a style choice, it is unreadable.
    //
    // Chosen rather than being a slot to fill in, because "put the wide one first" is
    // exactly the sort of instruction that gets lost and then looks like a bug.
    private SpeechBubble.Style WidestStyle()
    {
        if (styles == null) return null;

        SpeechBubble.Style best = null;
        float bestRatio = -1f;

        foreach (SpeechBubble.Style s in styles)
        {
            if (s == null || s.art == null || s.body.height <= 0f) continue;

            float ratio = s.body.width / s.body.height;
            if (ratio <= bestRatio) continue;

            bestRatio = ratio;
            best = s;
        }

        return best;
    }

    // The middle of the row of pans, which is the one part of the screen guaranteed to be
    // empty while a tooltip is up.
    //
    // Sunday runs on two colors and they sit at opposite ends of the counter, so the gap
    // between them is wide open. Every other spot is taken: kids fill the upper half, each
    // of them carries a thought bubble showing what they want, and that bubble is the one
    // thing the player has to be able to read.
    // The middle of the row of pans, where tooltips sit by default. Only the mess note
    // moves off it, and it does that by asking for MessPoint instead.
    private Vector3 CounterCentre()
    {
        if (slop == null) return Vector3.zero;

        int count = slop.GetColorCount();
        Vector3 sum = Vector3.zero;
        int found = 0;

        for (int i = 0; i < count; i++)
        {
            Slop pan = slop.GetSlopObject(i);
            if (pan == null) continue;

            sum += pan.transform.position;
            found++;
        }

        return found > 0 ? sum / found : Vector3.zero;
    }

    // Where the mess note goes, and ONLY the mess note.
    //
    // It is the one tooltip that appears while the counter is full, and parked on the pan
    // row it lay straight across the colors. Every other line shows during Sunday's opening,
    // when the counter is two pans at opposite ends and the middle is empty, so lifting
    // those as well just floated them up onto the brick for no reason.
    private Vector3 MessPoint()
    {
        Vector3 at = CounterCentre();
        at.y += tooltipLift;

        return at;
    }

    // The game's own font unless somebody deliberately overrode it. Looked up rather than
    // required, because an empty font slot silently falls back to Unity's default sans and
    // the tooltip ends up as the only thing on screen that is not pixel art.
    private TMP_FontAsset Font()
    {
        if (pixelFont != null) return pixelFont;

        GameUI ui = FindFirstObjectByType<GameUI>();
        return ui != null ? ui.Font : null;
    }

    private void Dismiss()
    {
        if (bubble != null) bubble.Close();

        bubble = null;
        anchorAt = null;
        queued = null;
        queuedAt = null;
    }

    private Vector2 ToCanvas(Vector3 world)
    {
        if (worldCam == null) worldCam = FindWorldCamera();
        if (worldCam == null || canvasRect == null) return Vector2.zero;

        Vector2 screen = worldCam.WorldToScreenPoint(world);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null,
                                                                out Vector2 local);
        return local;
    }

    // Same trap as the time popups: Camera.main needs the MainCamera tag, and LetterboxCamera
    // parents a second camera whose only job is clearing the bars. Picking that one would
    // put every bubble in the wrong place.
    private Camera FindWorldCamera()
    {
        if (Camera.main != null) return Camera.main;

        foreach (Camera c in Camera.allCameras)
        {
            if (c.cullingMask != 0) return c;
        }

        return null;
    }

    // Its own canvas rather than sharing GameUI's, so the tooltips sit above the HUD and so
    // neither file has to know the other exists.
    private void BuildCanvas()
    {
        GameObject go = new GameObject("Tutorial Canvas", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;   // above GameUI, which sits at 100

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;   // lock to height, see note in MenuUI

        canvasRect = go.GetComponent<RectTransform>();
    }
}
