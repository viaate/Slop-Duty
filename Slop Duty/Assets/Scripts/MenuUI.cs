using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The main menu, built in code in the same style as GameUI: no panels, hard offset
// shadows, pixel font.
//
// Deliberately self contained rather than sharing builders with GameUI. The two files
// duplicate about eighty lines of Canvas plumbing, which is a real cost, but it means
// changing the menu can never break the in-game HUD. If you later want one palette to
// rule both, lift the MakeText and MakeButton helpers into a static PixelUI class and
// have both call it.
public class MenuUI : MonoBehaviour
{
    [Header("Font")]
    [Tooltip("Same pixel font asset you put on GameUI.")]
    [SerializeField] private TMP_FontAsset pixelFont;

    [Header("Palette")]
    [SerializeField] private Color ink = new Color(0.851f, 0.898f, 0.769f);
    [SerializeField] private Color inkDim = new Color(0.541f, 0.596f, 0.427f);
    [SerializeField] private Color accent = new Color(0.788f, 0.843f, 0.290f);
    [SerializeField] private Color shadowInk = new Color(0.043f, 0.047f, 0.031f, 0.85f);
    [SerializeField] private Color background = new Color(0.055f, 0.059f, 0.043f);

    [Header("Shadow")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(6f, -6f);

    [Header("Title")]
    [Tooltip("Slow vertical bob, in reference pixels. Zero for a dead still title.")]
    [SerializeField] private float titleBob = 8f;
    [SerializeField] private float titleBobSpeed = 1.4f;

    [Header("Leaderboard")]
    [SerializeField] private float rowSpacing = 30f;

    // One entry per person, each carrying its own frames. Replaces the old pair of
    // parallel arrays, which assumed everybody had exactly one rest frame and one wave
    // frame. They do not: some have two frames and some have three.
    [System.Serializable]
    public class TeamMember
    {
        public string name;

        [Tooltip("Frame 0 is the resting pose. Everything after it is the hover loop, " +
                 "played in order. One frame means they simply stand still.")]
        public Sprite[] frames;

        [Tooltip("Frames per second for this person only. Leave at 0 to use the shared " +
                 "Wave Fps below, so you only set this where somebody needs to differ.")]
        public float fps;
    }

    [Header("Team")]
    [SerializeField]
    private TeamMember[] team =
    {
        new TeamMember { name = "OLIVIA GONSHER" },
        new TeamMember { name = "SELENA YUE" },
        new TeamMember { name = "JOHN SANDERS" },
    };

    [Tooltip("Default frames per second while hovering, used by anyone whose own Fps is 0. " +
             "2 means each pose holds half a second.")]
    [SerializeField] private float waveFps = 2f;

    [Tooltip("192 is a clean 6x on a 32 pixel sprite. Stick to multiples of 32 (96, 128, " +
             "160, 192) or some source pixels render wider than others and it looks uneven.")]
    [SerializeField] private float portraitPixels = 192f;

    [SerializeField] private float portraitGap = 340f;

    [Tooltip("Height of the portrait row above the bottom edge. The caption, the names and " +
             "the best score are all placed relative to this, so moving it moves the whole " +
             "block together.")]
    [SerializeField] private float teamRowY = 150f;

    [Tooltip("Without this the three characters read as decoration and people wonder why " +
             "those particular kids are on the title screen.")]
    [SerializeField] private string teamCaption = "MADE BY";

    private RectTransform titleRect;
    private bool leaving;

    private GameObject menuRoot;
    private GameObject boardPanel;
    private PixelText boardNames;
    private PixelText boardScores;
    private PixelText boardStatus;
    private bool awaitingBoard;

    private void Awake()
    {
        // Pull the camera to the theme color so the menu is not framed by Unity's
        // default blue, which undoes most of the work before anyone reads a word.
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
        }

        Time.timeScale = 1f;
        Build();
    }

    private void Update()
    {
        BobTitle();
        PollBoard();
    }

    private void BobTitle()
    {
        if (titleRect == null || titleBob <= 0f) return;

        // Snapped to whole pixels so a pixel font never lands on a half pixel and blurs.
        float raw = Mathf.Sin(Time.unscaledTime * titleBobSpeed) * titleBob;
        float snapped = Mathf.Round(raw / 2f) * 2f;

        titleRect.anchoredPosition = new Vector2(0f, -170f + snapped);
    }

    // --- actions ---

    private void StartShift()
    {
        // First time anyone plays, walk them through Sunday whether they asked for it or
        // not. After they have finished one, this goes straight to Monday and the training
        // shift stays on the menu for anybody who wants it again.
        int tutorial = HighScores.TutorialDone ? 0 : 1;
        Leave(tutorial);
    }

    private void StartTraining() => Leave(1);

    private void Leave(int tutorialFlag)
    {
        if (leaving) return;

        leaving = true;

        PlayerPrefs.SetInt(GameManager.TutorialPrefKey, tutorialFlag);
        PlayerPrefs.Save();

        // The music is not faded any more. It carries straight through the load, so
        // fading it here would put a hole in a track that is meant to be continuous.
        SceneFader.Go("SlopDuty");
    }

    private void ToggleBoard()
    {
        bool opening = !boardPanel.activeSelf;

        // The menu is switched off rather than covered. An overlay at 97% alpha still
        // lets a bright title bleed through visibly against a near-black background,
        // because the eye picks up small absolute differences in dark areas.
        menuRoot.SetActive(!opening);
        boardPanel.SetActive(opening);

        if (opening) RequestBoard();
    }

    // --- leaderboard ---

    private void RequestBoard()
    {
        boardNames.Text = "";
        boardScores.Text = "";

        Leaderboard board = Leaderboard.Instance;
        if (board == null) board = FindFirstObjectByType<Leaderboard>();

        if (board == null)
        {
            boardStatus.Text = "NO LEADERBOARD IN THIS SCENE";
            return;
        }

        if (!board.Configured)
        {
            boardStatus.Text = "GLOBAL SCORES NOT SET UP YET";
            return;
        }

        boardStatus.Text = "LOADING...";
        board.Refresh();
        awaitingBoard = true;
    }

    // Polled from Update rather than a coroutine so the panel can be opened and closed
    // freely without leaving a request dangling against a destroyed object.
    private void PollBoard()
    {
        if (!awaitingBoard) return;

        Leaderboard board = Leaderboard.Instance;
        if (board == null || board.Busy) return;

        awaitingBoard = false;

        if (!string.IsNullOrEmpty(board.LastError))
        {
            boardStatus.Text = board.LastError;
            return;
        }

        if (board.Top.Count == 0)
        {
            boardStatus.Text = "NO SCORES YET. BE FIRST.";
            return;
        }

        StringBuilder names = new StringBuilder();
        StringBuilder scores = new StringBuilder();

        for (int i = 0; i < board.Top.Count; i++)
        {
            Leaderboard.Entry e = board.Top[i];
            names.AppendLine($"{i + 1}.  {e.name}");
            scores.AppendLine(e.score.ToString());
        }

        boardStatus.Text = "";
        boardNames.Text = names.ToString();
        boardScores.Text = scores.ToString();
    }

    // --- construction ---

    private void Build()
    {
        GameObject canvasGo = new GameObject("MenuUI Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform root = canvasGo.GetComponent<RectTransform>();

        // Everything on the front page goes in one container so it can be switched off
        // wholesale when another screen opens.
        menuRoot = new GameObject("Menu Content", typeof(RectTransform));
        menuRoot.transform.SetParent(root, false);
        Stretch(menuRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        RectTransform content = menuRoot.GetComponent<RectTransform>();

        PixelText title = MakeText("Title", content, 128, TextAlignmentOptions.Top, ink,
                                   new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(1600f, 150f));
        title.Text = "SLOP DUTY";
        titleRect = title.root;

        PixelText tagline = MakeText("Tagline", content, 26, TextAlignmentOptions.Top, accent,
                                     new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1400f, 36f));
        tagline.Text = "MATCH THE COLOR AND MOVE FAST.";

        // Pulled up from -470/-570/-670 to clear the bigger portrait row and its caption.
        MakeButton("Play", content, "START SHIFT", new Vector2(0f, -400f), new Vector2(460f, 84f))
            .onClick.AddListener(StartShift);

        MakeButton("Training", content, "TRAINING SHIFT", new Vector2(0f, -490f), new Vector2(460f, 84f))
            .onClick.AddListener(StartTraining);

        MakeButton("Board", content, "LEADERBOARD", new Vector2(0f, -580f), new Vector2(460f, 84f))
            .onClick.AddListener(ToggleBoard);

        BuildTeam(content);

        PixelText best = MakeText("Best", content, 24, TextAlignmentOptions.Bottom, inkDim,
                                  new Vector2(0.5f, 0f), new Vector2(0f, teamRowY - 108f), new Vector2(900f, 34f));
        best.Text = HighScores.BestScore > 0
            ? $"BEST {HighScores.BestScore}   ({HighScores.BestDays} DAYS)"
            : "NO SHIFTS SURVIVED YET";

        BuildBoardPanel(root);
    }

    // The credits line, but as the three of you standing in the canteen. Replaces the
    // plain text row, so nothing else needed moving except the best score sliding down.
    private void BuildTeam(RectTransform parent)
    {
        int count = team == null ? 0 : team.Length;
        if (count == 0) return;

        if (!string.IsNullOrEmpty(teamCaption))
        {
            PixelText caption = MakeText("Team Caption", parent, 22, TextAlignmentOptions.Bottom, inkDim,
                                         new Vector2(0.5f, 0f),
                                         new Vector2(0f, teamRowY + portraitPixels + 14f),
                                         new Vector2(900f, 30f));
            caption.Text = teamCaption;
        }

        float span = portraitGap * (count - 1);

        for (int i = 0; i < count; i++)
        {
            TeamMember member = team[i];
            if (member == null) continue;

            float x = (-span * 0.5f) + (i * portraitGap);

            bool hasArt = member.frames != null && member.frames.Length > 0 && member.frames[0] != null;

            if (hasArt)
            {
                GameObject go = new GameObject($"Portrait {i}", typeof(RectTransform));
                go.transform.SetParent(parent, false);

                Anchor(go.GetComponent<RectTransform>(), new Vector2(0.5f, 0f),
                       new Vector2(x, teamRowY), new Vector2(portraitPixels, portraitPixels));

                Image img = go.AddComponent<Image>();
                img.preserveAspect = true;

                // Hover to animate. Setup shows frame 0 and decides whether this portrait
                // takes pointer events at all, based on whether there is more than one frame.
                float fps = member.fps > 0f ? member.fps : waveFps;
                go.AddComponent<PortraitWave>().Setup(member.frames, fps);
            }

            // Sits below the row rather than at a fixed height, so resizing the portraits
            // only pushes their tops up and never lands the name on top of the artwork.
            PixelText label = MakeText($"Name {i}", parent, 22, TextAlignmentOptions.Top, ink,
                                       new Vector2(0.5f, 0f), new Vector2(x, teamRowY - 58f),
                                       new Vector2(portraitGap - 20f, 30f));
            label.Text = member.name;
        }
    }

    private void BuildBoardPanel(RectTransform root)
    {
        boardPanel = new GameObject("Leaderboard Panel", typeof(RectTransform));
        boardPanel.transform.SetParent(root, false);
        Stretch(boardPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        // Fully opaque. See ToggleBoard for why 97% was not enough.
        Image bg = boardPanel.AddComponent<Image>();
        bg.color = background;

        RectTransform panel = boardPanel.GetComponent<RectTransform>();

        // Inverted header strip: dark text on a solid accent band, running the full width.
        // The front page is light text floating on dark, so flipping the relationship here
        // makes this read as a different screen instead of the same one with new words.
        GameObject strip = new GameObject("Header Strip", typeof(RectTransform));
        strip.transform.SetParent(panel, false);

        RectTransform stripRect = strip.GetComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot = new Vector2(0.5f, 1f);
        stripRect.sizeDelta = new Vector2(0f, 150f);
        stripRect.anchoredPosition = Vector2.zero;

        Image stripImage = strip.AddComponent<Image>();
        stripImage.color = accent;

        // No drop shadow on this one. A shadow is there to lift light text off a dark
        // background, and dark text on a light band does not need lifting.
        TextMeshProUGUI heading = RawText("Heading", stripRect, 64, TextAlignmentOptions.Center, background);
        Stretch(heading.rectTransform, Vector2.zero, Vector2.zero);
        heading.text = "LEADERBOARD";

        PixelText yours = MakeText("Yours", panel, 26, TextAlignmentOptions.Top, ink,
                                   new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(1200f, 36f));
        yours.Text = HighScores.BestScore > 0
            ? $"YOUR BEST   {HighScores.BestScore}   ({HighScores.BestDays} DAYS, {HighScores.BestServed} SERVED)"
            : "YOU HAVE NOT SURVIVED A SHIFT YET";

        // Two columns rather than one padded string, so names of any length still line up
        // against right aligned scores.
        boardNames = MakeText("Names", panel, 28, TextAlignmentOptions.TopLeft, ink,
                              new Vector2(0.5f, 1f), new Vector2(-320f, -310f), new Vector2(460f, 480f));

        boardScores = MakeText("Scores", panel, 28, TextAlignmentOptions.TopRight, accent,
                               new Vector2(0.5f, 1f), new Vector2(320f, -310f), new Vector2(460f, 480f));

        SetLineSpacing(boardNames, rowSpacing);
        SetLineSpacing(boardScores, rowSpacing);

        // Sits in the same space the list would occupy, since only one of them ever has
        // anything in it.
        boardStatus = MakeText("Status", panel, 26, TextAlignmentOptions.Top, inkDim,
                               new Vector2(0.5f, 1f), new Vector2(0f, -340f), new Vector2(1200f, 40f));

        MakeButton("Back", panel, "BACK", new Vector2(0f, -880f), new Vector2(320f, 76f))
            .onClick.AddListener(ToggleBoard);

        boardPanel.SetActive(false);
    }

    // --- shared bits, mirrored from GameUI ---

    private class PixelText
    {
        public RectTransform root;
        public TextMeshProUGUI main;
        public TextMeshProUGUI shadow;

        public string Text
        {
            set
            {
                main.text = value;
                shadow.text = value;
            }
        }
    }

    // Applied to both copies, or the shadow drifts out of register with the main text.
    private static void SetLineSpacing(PixelText t, float spacing)
    {
        t.main.lineSpacing = spacing;
        t.shadow.lineSpacing = spacing;
    }

    private PixelText MakeText(string name, RectTransform parent, int size, TextAlignmentOptions align,
                               Color color, Vector2 anchor, Vector2 position, Vector2 boxSize)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        Anchor(rt, anchor, position, boxSize);

        TextMeshProUGUI shadow = RawText(name + " Shadow", rt, size, align, shadowInk);
        Stretch(shadow.rectTransform, shadowOffset, shadowOffset);

        TextMeshProUGUI main = RawText(name + " Main", rt, size, align, color);
        Stretch(main.rectTransform, Vector2.zero, Vector2.zero);

        return new PixelText { root = rt, main = main, shadow = shadow };
    }

    private TextMeshProUGUI RawText(string name, RectTransform parent, int size,
                                    TextAlignmentOptions align, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        if (pixelFont != null) t.font = pixelFont;

        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;

        return t;
    }

    // Every button sits dim and lights up to the accent on hover, rather than one of them
    // being permanently highlighted. Transition is forced to None because Unity's own
    // ColorBlock only tints the background Image, so the label would stay dim while the
    // box behind it brightened. HoverTint moves both together.
    private Button MakeButton(string name, RectTransform parent, string label,
                              Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();

        Button b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.None;

        Anchor(go.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), position, size);

        PixelText text = MakeText(name + " Text", go.GetComponent<RectTransform>(), 30,
                                  TextAlignmentOptions.Center, inkDim,
                                  new Vector2(0.5f, 0.5f), Vector2.zero, size);
        text.Text = label;

        go.AddComponent<HoverTint>().Setup(img, text.main, inkDim, accent);

        return b;
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }
}
