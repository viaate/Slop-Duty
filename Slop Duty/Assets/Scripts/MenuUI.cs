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

    // A teacher in a bottom corner, popping up now and then.
    //
    // Rise and Duck are for somebody drawn as a peek, like Taneim. Leave them empty and set
    // Still instead, and the same peek is done by sliding the picture up from below the
    // screen edge rather than by frames. Both read as the same gag, so one animated teacher
    // and one flat one still look like a matching pair.
    [System.Serializable]
    public class Teacher
    {
        public string name;

        [Tooltip("Coming up, in order. Leave empty to slide instead of animating.")]
        public Sprite[] rise;

        [Tooltip("Going back down, in order. The last frame is what 'hidden' looks like.")]
        public Sprite[] duck;

        [Tooltip("Used only when there are no frames. The whole picture slides instead.")]
        public Sprite still;

        public float fps = 12f;

        [Tooltip("Right hand corner instead of the left.")]
        public bool rightSide;

        [Tooltip("How much of him stays showing when nobody is hovering, as a fraction of " +
                 "his height. This is the hint that there is anything there to hover at " +
                 "all. Ignored when there are frames, since a drawn peek already decides " +
                 "for itself how much of him is left in shot.\n\n" +
                 "0.31 matches Taneim: his resting frame leaves 9 of its 32 rows showing, " +
                 "which is 0.281, and the extra 0.03 covers the transparent row above Al's " +
                 "head. Re-measure if either drawing changes.")]
        [Range(0f, 0.6f)] public float peekAtRest = 0.31f;
    }

    [Header("Teachers")]
    [Tooltip("One per bottom corner. Hidden until you put the pointer in their corner, " +
             "then they peek up and duck away again when it leaves.")]
    [SerializeField]
    private Teacher[] teachers =
    {
        new Teacher { name = "MR TANEIM", rightSide = false },
        new Teacher { name = "", rightSide = true },
    };

    [Tooltip("How tall a teacher is drawn, in reference pixels.")]
    [SerializeField] private float teacherPixels = 260f;

    [Tooltip("Gap from the screen corner, so they are not jammed against the edge.")]
    [SerializeField] private float teacherInset = 24f;

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

    // Matches the CanvasScaler reference resolution below. Needed because the team block
    // is positioned from the bottom edge and the buttons from the top, so one of them has
    // to be converted to compare them.
    private const float RefHeight = 1080f;

    private const float TaglineBottom = 366f;   // tagline sits at 330 and is 36 tall
    private const float ButtonHeight = 84f;
    private const float ButtonGap = 6f;
    private const float CaptionHeight = 30f;

    // Top edge of the whole team block, measured up from the bottom of the screen.
    private float TeamBlockTop =>
        teamRowY + portraitPixels + 14f + (string.IsNullOrEmpty(teamCaption) ? 0f : CaptionHeight);

    private RectTransform titleRect;
    private bool leaving;

    private GameObject menuRoot;
    private GameObject boardPanel;
    private GameObject settingsPanel;
    private GameObject accessPanel;
    private PixelText volumeReadout;
    private PixelText accessNote;
    private HoverTint[] visionTints;
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

    private void ToggleSettings()
    {
        bool opening = !settingsPanel.activeSelf;

        menuRoot.SetActive(!opening);
        settingsPanel.SetActive(opening);
        accessPanel.SetActive(false);
    }

    // Settings stays switched off underneath rather than showing through, matching how the
    // leaderboard covers the menu. Coming back lands on settings, not all the way out,
    // because accessibility is somewhere you arrived from there.
    private void ToggleAccess()
    {
        bool opening = !accessPanel.activeSelf;

        settingsPanel.SetActive(!opening);
        accessPanel.SetActive(opening);
    }

    // --- settings ---

    private void BuildSettingsPanel(RectTransform root)
    {
        RectTransform panel = MakePanel(root, "Settings Panel", "SETTINGS", out settingsPanel);

        PixelText volumeLabel = MakeText("Volume Label", panel, 30, TextAlignmentOptions.Top, ink,
                                         new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(900f, 40f));
        volumeLabel.Text = "VOLUME";

        volumeReadout = MakeText("Volume Value", panel, 44, TextAlignmentOptions.Top, accent,
                                 new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(900f, 56f));

        Slider slider = MakeSlider("Volume", panel, new Vector2(0f, -420f), new Vector2(760f, 40f));
        slider.value = Settings.Volume;
        slider.onValueChanged.AddListener(OnVolumeChanged);

        ShowVolume(Settings.Volume);

        MakeButton("Access", panel, "ACCESSIBILITY", new Vector2(0f, -560f), new Vector2(480f, 84f),
                   new Vector2(0.5f, 1f), 30)
            .onClick.AddListener(ToggleAccess);

        MakeButton("Settings Back", panel, "BACK", new Vector2(0f, -880f), new Vector2(320f, 76f),
                   new Vector2(0.5f, 1f), 30)
            .onClick.AddListener(ToggleSettings);

        settingsPanel.SetActive(false);
    }

    private void OnVolumeChanged(float value)
    {
        Settings.Volume = value;
        ShowVolume(value);
    }

    // Shown as a percentage rather than only as a bar position. A slider with no number is
    // guesswork at the quiet end, where the difference between very low and silent is the
    // difference between working and broken.
    private void ShowVolume(float value)
    {
        if (volumeReadout == null) return;

        int percent = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f);
        volumeReadout.Text = percent <= 0 ? "MUTED" : $"{percent}%";
    }

    // --- accessibility ---

    private void BuildAccessPanel(RectTransform root)
    {
        RectTransform panel = MakePanel(root, "Accessibility Panel", "COLOR VISION", out accessPanel);

        PixelText blurb = MakeText("Access Blurb", panel, 26, TextAlignmentOptions.Top, inkDim,
                                   new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(1300f, 120f));
        blurb.Text = "PICK WHAT YOU SEE AND THE COUNTER STOPS USING COLORS\n" +
                     "YOU CANNOT TELL APART. EVERY SLOP ALSO GETS A SHAPE.";

        SetLineSpacing(blurb, 14f);

        visionTints = new HoverTint[4];

        for (int i = 0; i < 4; i++)
        {
            // Copied into its own local before the listener closes over it. Capturing the
            // loop variable directly would give every button whichever value i ended on.
            ColorVision option = (ColorVision)i;

            MakeButton($"Vision {i}", panel, Colorblind.LabelFor(option),
                       new Vector2(0f, -(360f + (i * 96f))), new Vector2(560f, 84f),
                       new Vector2(0.5f, 1f), 30, out visionTints[i])
                .onClick.AddListener(() => ChooseVision(option));
        }

        accessNote = MakeText("Access Note", panel, 24, TextAlignmentOptions.Top, accent,
                              new Vector2(0.5f, 1f), new Vector2(0f, -812f), new Vector2(1300f, 40f));

        MakeButton("Access Back", panel, "BACK", new Vector2(0f, -880f), new Vector2(320f, 76f),
                   new Vector2(0.5f, 1f), 30)
            .onClick.AddListener(ToggleAccess);

        ShowVision();

        accessPanel.SetActive(false);
    }

    private void ChooseVision(ColorVision option)
    {
        Settings.Vision = option;
        ShowVision();
    }

    // The chosen option stays lit, rather than only lighting under the pointer.
    //
    // Four identical buttons and one line of text underneath was not enough: the state was
    // shown somewhere other than where the choice is made, so the only way to check what
    // you had picked was to read the bottom of the screen. Lighting the button itself puts
    // the answer where the question is.
    private void ShowVision()
    {
        if (visionTints != null)
        {
            for (int i = 0; i < visionTints.Length; i++)
            {
                if (visionTints[i] == null) continue;

                bool on = (ColorVision)i == Settings.Vision;
                visionTints[i].Setup(visionTints[i].Fill, visionTints[i].Label,
                                     on ? accent : inkDim, accent);
            }
        }

        if (accessNote == null) return;

        accessNote.Text = Colorblind.NoteFor(Settings.Vision);
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
        // Locked to height, not the 0.5 blend it used to be.
        //
        // The camera is orthographic, so the world's scale is decided purely by screen
        // HEIGHT: a wider window shows more sideways but never makes anything bigger.
        // Matching height makes the interface behave the same way, so a shape change moves
        // the edges further apart and leaves everything the size it was.
        //
        // On the 0.5 blend the scale factor was a mix of both axes, so widening the window
        // grew every label, and at a wide enough aspect the menu grew until it overflowed
        // its own screen. That is the whole "nothing is locked" problem.
        scaler.matchWidthOrHeight = 1f;

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
        // The two shift buttons are the decision the player is here to make, so they own
        // the centre. Identical size on purpose: they are equal choices, not a primary
        // and a fallback.
        //
        // Their position is computed rather than typed, so the pair stays centred in the
        // space between the tagline and the team block. Change Team Row Y or the portrait
        // size and the buttons re-centre themselves instead of drifting out of balance.
        float gapTop = TaglineBottom;
        float gapBottom = RefHeight - TeamBlockTop;
        float blockHeight = (ButtonHeight * 2f) + ButtonGap;

        float firstY = gapTop + (((gapBottom - gapTop) - blockHeight) * 0.5f);
        float secondY = firstY + ButtonHeight + ButtonGap;

        MakeButton("Play", content, "START SHIFT", new Vector2(0f, -firstY), new Vector2(460f, ButtonHeight),
                   new Vector2(0.5f, 1f), 30)
            .onClick.AddListener(StartShift);

        MakeButton("Training", content, "TRAINING SHIFT", new Vector2(0f, -secondY), new Vector2(460f, ButtonHeight),
                   new Vector2(0.5f, 1f), 30)
            .onClick.AddListener(StartTraining);

        // Tucked into the top right corner. It is somewhere to go rather than something to
        // do, so it should not sit in the same column as the two things that start a game.
        MakeButton("Board", content, "LEADERBOARD", new Vector2(-32f, -32f), new Vector2(300f, 64f),
                   new Vector2(1f, 1f), 24)
            .onClick.AddListener(ToggleBoard);

        // Mirrored into the opposite corner. Same size, same inset, same reasoning: both
        // are places to go rather than things to do, so neither belongs in the column with
        // the two buttons that start a game.
        MakeButton("Settings", content, "SETTINGS", new Vector2(32f, -32f), new Vector2(300f, 64f),
                   new Vector2(0f, 1f), 24)
            .onClick.AddListener(ToggleSettings);

        BuildTeam(content);
        BuildTeachers(content);

        PixelText best = MakeText("Best", content, 24, TextAlignmentOptions.Bottom, inkDim,
                                  new Vector2(0.5f, 0f), new Vector2(0f, teamRowY - 108f), new Vector2(900f, 34f));
        best.Text = HighScores.BestScore > 0
            ? $"BEST {HighScores.BestScore}   ({HighScores.BestDays} DAYS)"
            : "NO SHIFTS SURVIVED YET";

        BuildBoardPanel(root);
        BuildSettingsPanel(root);
        BuildAccessPanel(root);
    }

    // Both teachers, one per bottom corner.
    //
    // Anchored to the corners themselves rather than offset from the middle, so they stay
    // put when the window changes shape instead of drifting toward the team block.
    private void BuildTeachers(RectTransform parent)
    {
        if (teachers == null) return;

        foreach (Teacher t in teachers)
        {
            if (t == null) continue;

            bool framed = t.rise != null && t.rise.Length > 0;
            if (!framed && t.still == null) continue;

            Vector2 corner = new Vector2(t.rightSide ? 1f : 0f, 0f);

            // The zone is what catches the pointer, and it never moves. The character
            // himself cannot be the hover target: he is hidden until you hover him, so
            // there would be nothing there to hover in the first place. Invisible, but an
            // Image at zero alpha still takes raycasts, which is the whole point of it.
            GameObject zone = new GameObject($"Teacher {t.name}", typeof(RectTransform));
            zone.transform.SetParent(parent, false);

            RectTransform zoneRect = zone.GetComponent<RectTransform>();
            zoneRect.anchorMin = corner;
            zoneRect.anchorMax = corner;
            zoneRect.pivot = corner;
            zoneRect.sizeDelta = new Vector2(teacherPixels, teacherPixels);
            zoneRect.anchoredPosition = new Vector2(t.rightSide ? -teacherInset : teacherInset, 0f);

            Image catcher = zone.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            // The picture is a child so it can slide out from under the zone without
            // dragging the pointer target off screen with it.
            GameObject artGo = new GameObject("Art", typeof(RectTransform));
            artGo.transform.SetParent(zone.transform, false);

            RectTransform rt = artGo.GetComponent<RectTransform>();
            Stretch(rt, Vector2.zero, Vector2.zero);

            Image art = artGo.AddComponent<Image>();
            art.raycastTarget = false;
            art.preserveAspect = true;
            art.sprite = framed ? t.rise[0] : t.still;

            // Parented to the menu rather than to the teacher, so the name holds still
            // while he sinks inside his own box. It fades with him instead of moving.
            GameObject nameGo = new GameObject($"Teacher {t.name} Name", typeof(RectTransform));
            nameGo.transform.SetParent(parent, false);

            CanvasGroup group = nameGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            RectTransform nameRect = nameGo.GetComponent<RectTransform>();
            Anchor(nameRect, corner,
                   new Vector2(t.rightSide ? -teacherInset : teacherInset, teacherPixels + 8f),
                   new Vector2(teacherPixels, 30f));

            // Exactly as wide as he is, so a centred name sits over him rather than being
            // nudged toward the middle of the screen by a wider box.
            PixelText label = MakeText($"Teacher {t.name} Text", nameRect, 22,
                                       TextAlignmentOptions.Center, inkDim,
                                       new Vector2(0.5f, 0.5f), Vector2.zero,
                                       new Vector2(teacherPixels, 30f));
            label.Text = t.name;

            zone.AddComponent<MenuPeeker>().Setup(rt, group, t.rise, t.duck, t.still, t.fps,
                                                  teacherPixels, t.peekAtRest);
        }
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
                                         new Vector2(900f, CaptionHeight));
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

        MakeButton("Back", panel, "BACK", new Vector2(0f, -880f), new Vector2(320f, 76f),
                   new Vector2(0.5f, 1f), 30)
            .onClick.AddListener(ToggleBoard);

        boardPanel.SetActive(false);
    }

    // The full screen overlay with an inverted header strip, as established by the
    // leaderboard: dark text on a solid accent band. The front page is light text on dark,
    // so flipping the relationship makes these read as a different screen rather than the
    // same one with new words on it.
    private RectTransform MakePanel(RectTransform root, string objectName, string heading,
                                    out GameObject panelObject)
    {
        panelObject = new GameObject(objectName, typeof(RectTransform));
        panelObject.transform.SetParent(root, false);

        RectTransform panel = panelObject.GetComponent<RectTransform>();
        Stretch(panel, Vector2.zero, Vector2.zero);

        // Fully opaque. A near-opaque overlay still lets a bright title bleed through
        // against a near-black background, because the eye picks up small absolute
        // differences in dark areas.
        Image bg = panelObject.AddComponent<Image>();
        bg.color = background;

        GameObject strip = new GameObject("Header Strip", typeof(RectTransform));
        strip.transform.SetParent(panel, false);

        RectTransform stripRect = strip.GetComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot = new Vector2(0.5f, 1f);
        stripRect.sizeDelta = new Vector2(0f, 150f);
        stripRect.anchoredPosition = Vector2.zero;

        strip.AddComponent<Image>().color = accent;

        // No drop shadow. A shadow lifts light text off a dark background, and dark text on
        // a light band does not need lifting.
        TextMeshProUGUI title = RawText("Heading", stripRect, 64, TextAlignmentOptions.Center, background);
        Stretch(title.rectTransform, Vector2.zero, Vector2.zero);
        title.text = heading;

        return panel;
    }

    // Built by hand rather than from Unity's slider prefab, so it matches everything else
    // here: flat rectangles, no rounded sprites, no gradients.
    private Slider MakeSlider(string objectName, RectTransform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Anchor(go.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), position, size);

        Slider slider = go.AddComponent<Slider>();

        // The track is what catches the click, so it has to stay raycastable. Everything
        // else here is decoration and is switched off for hit testing.
        Image track = MakeBlock("Track", go.transform, new Color(ink.r, ink.g, ink.b, 0.18f), true);
        Stretch(track.rectTransform, Vector2.zero, Vector2.zero);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        Image fill = MakeBlock("Fill", fillArea.transform, accent, false);
        fill.rectTransform.anchorMin = Vector2.zero;
        fill.rectTransform.anchorMax = Vector2.one;
        fill.rectTransform.offsetMin = Vector2.zero;
        fill.rectTransform.offsetMax = Vector2.zero;

        GameObject slideArea = new GameObject("Handle Area", typeof(RectTransform));
        slideArea.transform.SetParent(go.transform, false);

        // Inset by half a handle so the handle's own edges stop flush with the track's
        // rather than hanging off both ends.
        float half = size.y * 0.5f;
        Stretch(slideArea.GetComponent<RectTransform>(), new Vector2(half, 0f), new Vector2(-half, 0f));

        Image handle = MakeBlock("Handle", slideArea.transform, ink, true);
        handle.rectTransform.sizeDelta = new Vector2(size.y, size.y + 16f);

        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;

        return slider;
    }

    private static Image MakeBlock(string objectName, Transform parent, Color color, bool clickable)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = clickable;

        return img;
    }

    // --- shared bits, mirrored from GameUI ---

    private class PixelText
    {
        public RectTransform root;
        public TextMeshProUGUI main;
        public TextMeshProUGUI shadow;

        // Same guard as GameUI: writing a string rebuilds TextMeshPro's mesh whether or not
        // it differs, and the menu polls the leaderboard every frame while it is open.
        public string Text
        {
            set
            {
                if (main.text == value) return;

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

        // Same guard as GameUI. Nothing in the menu uses color tags yet, but both halves of
        // a PixelText share one string, so the first thing that does would come out doubled.
        shadow.overrideColorTags = true;

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
    // anchor lets a button hang off a corner rather than off the top centre, which is what
    // the leaderboard needs now that it lives in the top right.
    private Button MakeButton(string name, RectTransform parent, string label,
                              Vector2 position, Vector2 size, Vector2 anchor, int fontSize)
    {
        return MakeButton(name, parent, label, position, size, anchor, fontSize, out _);
    }

    // The variant that hands back its hover behaviour, for buttons that also have to show
    // an on or off state. Re-running Setup with a different idle color is what makes the
    // chosen one stay lit instead of only lighting under the pointer.
    private Button MakeButton(string name, RectTransform parent, string label,
                              Vector2 position, Vector2 size, Vector2 anchor, int fontSize,
                              out HoverTint tint)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();

        Button b = go.AddComponent<Button>();
        b.targetGraphic = img;
        b.transition = Selectable.Transition.None;

        Anchor(go.GetComponent<RectTransform>(), anchor, position, size);

        PixelText text = MakeText(name + " Text", go.GetComponent<RectTransform>(), fontSize,
                                  TextAlignmentOptions.Center, inkDim,
                                  new Vector2(0.5f, 0.5f), Vector2.zero, size);
        text.Text = label;

        tint = go.AddComponent<HoverTint>();
        tint.Setup(img, text.main, inkDim, accent);

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
