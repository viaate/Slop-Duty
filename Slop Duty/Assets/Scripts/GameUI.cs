using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the whole interface in code at runtime. Nothing to wire, which is deliberate:
// hand-built Canvas hierarchies are the most conflict-prone thing three people can share
// in one Unity scene.
//
// Style is deliberately bare. No panels, no boxes, no rounded anything. Just text with a
// hard offset shadow, which is how pixel art games have always done readable UI. The one
// thing that matters most is the font: drop a pixel font into Pixel Font below and the
// whole thing changes character. Without one you get Unity's default sans, which will
// always look generic no matter what the layout does.
public class GameUI : MonoBehaviour
{
    [Header("Font")]
    [Tooltip("A pixel font TMP asset. Everything uses it. This is the single biggest " +
             "visual lever here, far more than colors or layout.")]
    [SerializeField] private TMP_FontAsset pixelFont;

    [Header("Palette")]
    [SerializeField] private Color ink = new Color(0.851f, 0.898f, 0.769f);
    [SerializeField] private Color inkDim = new Color(0.541f, 0.596f, 0.427f);
    [SerializeField] private Color accent = new Color(0.788f, 0.843f, 0.290f);
    [SerializeField] private Color danger = new Color(0.878f, 0.298f, 0.239f);
    [SerializeField] private Color shadowInk = new Color(0.043f, 0.047f, 0.031f, 0.85f);

    [Header("Shadow")]
    [Tooltip("Hard offset behind every label, in reference pixels. Whole numbers only, " +
             "or it stops looking like pixel art.")]
    [SerializeField] private Vector2 shadowOffset = new Vector2(5f, -5f);

    [Header("Day card")]
    [SerializeField] private float cardRise = 0.22f;
    [SerializeField] private float cardHold = 1.20f;
    [SerializeField] private float cardFall = 0.40f;

    [Header("Clock")]
    [SerializeField] private float lowTimeSeconds = 10f;

    [Header("Game over")]
    [Tooltip("Extra gap between stat rows. Pixel fonts have almost no built-in leading, " +
             "so without this the rows read as one solid block.")]
    [SerializeField] private float statRowGap = 60f;

    private GameManager game;
    private LevelTimer timer;

    private PixelText dayLabel;
    private PixelText weekLabel;
    private PixelText clockLabel;
    private PixelText quotaLabel;
    private PixelText warningLabel;

    private GameObject hudRoot;
    private CanvasGroup cardGroup;
    private RectTransform cardRect;
    private PixelText cardDay;
    private PixelText cardWeek;

    private GameObject overPanel;
    private PixelText overScore;
    private PixelText overLabels;
    private PixelText overValues;
    private PixelText overRecord;
    private PixelText overBoard;
    private TMP_InputField nameField;
    private GameObject submitButton;

    private float cardTimer = -1f;
    private bool overShown;
    private bool awaitingBoard;

    // Main text plus a hard black copy sitting behind it. Two real text objects rather
    // than TMP's built-in underlay, because the underlay is SDF based and comes out soft
    // and blurry, which is exactly the look we are trying to avoid.
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

        public Color Tint { set => main.color = value; }
        public GameObject Root => root.gameObject;
    }

    private void Awake() => Build();

    private void Start()
    {
        game = GameManager.Instance;
        if (game == null) game = FindFirstObjectByType<GameManager>();

        timer = FindFirstObjectByType<LevelTimer>();

        if (game == null) return;

        game.DayStarted += ShowDayCard;
        game.RunEnded += ShowGameOver;

        ShowDayCard(game.Day);
    }

    private void OnDestroy()
    {
        if (game == null) return;

        game.DayStarted -= ShowDayCard;
        game.RunEnded -= ShowGameOver;
    }

    // Unscaled throughout, because the run ends by freezing timeScale and a game over
    // screen that cannot animate itself in is worse than none.
    private void Update()
    {
        RefreshHud();
        TickCard();
        PollBoard();
    }

    // --- HUD ---

    private void RefreshHud()
    {
        if (game != null)
        {
            dayLabel.Text = DayConfig.NameFor(game.Day);
            weekLabel.Text = game.Day <= 0 ? "TRAINING" : $"WEEK {DayConfig.WeekFor(game.Day)}";
            quotaLabel.Text = $"{game.ResolvedToday}/{game.QuotaToday}";
        }

        if (timer != null)
        {
            float left = timer.TimeRemaining;
            clockLabel.Text = Mathf.CeilToInt(left).ToString();

            bool low = left <= lowTimeSeconds && timer.Running;
            clockLabel.Tint = low ? danger : ink;

            // Snapped to whole steps rather than a smooth sine, so it ticks like a
            // sprite animation instead of breathing like a web page.
            float step = low && (Mathf.FloorToInt(Time.unscaledTime * 6f) % 2 == 0) ? 1.08f : 1f;
            clockLabel.root.localScale = Vector3.one * step;
        }

        bool blocked = PukeManager.Instance != null && PukeManager.Instance.AnyBlockingPuddle;
        warningLabel.Root.SetActive(blocked);
    }

    // --- day card ---

    private void ShowDayCard(int day)
    {
        cardDay.Text = DayConfig.NameFor(day);
        cardWeek.Text = day <= 0 ? "NOBODY COMES IN ON A SUNDAY" : $"WEEK {DayConfig.WeekFor(day)}";
        cardTimer = 0f;
    }

    private void TickCard()
    {
        if (cardTimer < 0f)
        {
            cardGroup.alpha = 0f;
            return;
        }

        cardTimer += Time.unscaledDeltaTime;

        float total = cardRise + cardHold + cardFall;

        if (cardTimer >= total)
        {
            cardTimer = -1f;
            cardGroup.alpha = 0f;
            return;
        }

        // Alpha steps in thirds rather than fading smoothly, which reads as a sprite
        // popping in rather than a CSS transition.
        float alpha;
        float scale;

        if (cardTimer < cardRise)
        {
            float t = cardTimer / cardRise;
            alpha = Mathf.Ceil(t * 3f) / 3f;
            scale = Mathf.Lerp(1.25f, 1f, t * t);
        }
        else if (cardTimer < cardRise + cardHold)
        {
            alpha = 1f;
            scale = 1f;
        }
        else
        {
            float t = (cardTimer - cardRise - cardHold) / cardFall;
            alpha = 1f - (Mathf.Ceil(t * 3f) / 3f);
            scale = 1f;
        }

        cardGroup.alpha = alpha;
        cardRect.localScale = Vector3.one * scale;
    }

    // --- game over ---

    private void ShowGameOver()
    {
        if (overShown) return;
        overShown = true;

        RunStats s = game != null ? game.Stats : new RunStats();
        int score = HighScores.ScoreOf(s);
        bool record = HighScores.Submit(s);

        overScore.Text = score.ToString();

        StringBuilder labels = new StringBuilder();
        StringBuilder values = new StringBuilder();

        Row(labels, values, "DAYS CLEARED", s.daysCleared.ToString());
        Row(labels, values, "SERVED RIGHT", s.served.ToString());
        Row(labels, values, "WRONG COLOR", s.wrongColor.ToString());
        Row(labels, values, "WALKED OUT", s.walkedOut.ToString());
        Row(labels, values, "ACCURACY", s.Total == 0 ? "N/A" : $"{s.Accuracy:P0}");

        overLabels.Text = labels.ToString();
        overValues.Text = values.ToString();

        overRecord.Text = record ? "NEW BEST" : $"BEST {HighScores.BestScore}";
        overRecord.Tint = record ? accent : inkDim;

        // Switched off rather than covered, so nothing can bleed through the panel.
        if (hudRoot != null) hudRoot.SetActive(false);

        overPanel.SetActive(true);

        Leaderboard board = Leaderboard.Instance;
        bool online = board != null && board.Configured;

        nameField.gameObject.SetActive(online);
        submitButton.SetActive(online);
        overBoard.Root.SetActive(online);

        if (!online) return;

        nameField.text = HighScores.PlayerName;
        overBoard.Text = "LOADING...";
        board.Refresh();
        awaitingBoard = true;
    }

    private static void Row(StringBuilder labels, StringBuilder values, string label, string value)
    {
        labels.AppendLine(label);
        values.AppendLine(value);
    }

    private void SubmitScore()
    {
        Leaderboard board = Leaderboard.Instance;
        if (board == null || !board.Configured || game == null) return;

        HighScores.PlayerName = nameField.text;
        board.SubmitAndRefresh(nameField.text, game.Stats);

        overBoard.Text = "SENDING...";
        awaitingBoard = true;
    }

    // Polled from Update rather than InvokeRepeating, because the run ends by setting
    // timeScale to 0 and Invoke runs on scaled time, so it would never fire.
    private void PollBoard()
    {
        if (!awaitingBoard) return;

        Leaderboard board = Leaderboard.Instance;
        if (board == null || board.Busy) return;

        awaitingBoard = false;

        // The real error, not a friendly one. This is the only place you can see it in a
        // WebGL build, where there is no console to check.
        if (!string.IsNullOrEmpty(board.LastError))
        {
            overBoard.Text = $"SCOREBOARD OFFLINE\n{board.LastError}";
            return;
        }

        if (board.Top.Count == 0)
        {
            overBoard.Text = "NO SCORES YET";
            return;
        }

        StringBuilder sb = new StringBuilder("GLOBAL TOP\n");

        for (int i = 0; i < board.Top.Count; i++)
        {
            Leaderboard.Entry e = board.Top[i];
            sb.AppendLine($"{i + 1}. {e.name} {e.score}");
        }

        overBoard.Text = sb.ToString();
    }

    private void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // --- construction ---

    private void Build()
    {
        GameObject canvasGo = new GameObject("GameUI Canvas", typeof(RectTransform));
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

        BuildHud(root);
        BuildDayCard(root);
        BuildGameOver(root);
    }

    private void BuildHud(RectTransform parent)
    {
        // One container so the whole HUD can be switched off when the run ends.
        hudRoot = new GameObject("HUD", typeof(RectTransform));
        hudRoot.transform.SetParent(parent, false);
        Stretch(hudRoot.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        RectTransform root = hudRoot.GetComponent<RectTransform>();

        dayLabel = MakeText("Day", root, 46, TextAlignmentOptions.TopLeft, ink,
                        new Vector2(0f, 1f), new Vector2(36f, -30f), new Vector2(500f, 56f));

        weekLabel = MakeText("Week", root, 22, TextAlignmentOptions.TopLeft, inkDim,
                         new Vector2(0f, 1f), new Vector2(40f, -84f), new Vector2(500f, 30f));

        clockLabel = MakeText("Clock", root, 60, TextAlignmentOptions.TopRight, ink,
                          new Vector2(1f, 1f), new Vector2(-36f, -30f), new Vector2(400f, 70f));

        quotaLabel = MakeText("Quota", root, 22, TextAlignmentOptions.TopRight, inkDim,
                          new Vector2(1f, 1f), new Vector2(-40f, -100f), new Vector2(400f, 30f));

        warningLabel = MakeText("Warning", root, 40, TextAlignmentOptions.Center, danger,
                            new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(900f, 54f));
        warningLabel.Text = "CLEAN IT UP";
        warningLabel.Root.SetActive(false);
    }

    private void BuildDayCard(RectTransform root)
    {
        GameObject go = new GameObject("Day Card", typeof(RectTransform));
        go.transform.SetParent(root, false);

        cardRect = go.GetComponent<RectTransform>();
        Anchor(cardRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 300f));

        cardGroup = go.AddComponent<CanvasGroup>();
        cardGroup.alpha = 0f;
        cardGroup.blocksRaycasts = false;

        cardDay = MakeText("Card Day", cardRect, 130, TextAlignmentOptions.Center, ink,
                       new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(1400f, 160f));

        cardWeek = MakeText("Card Week", cardRect, 30, TextAlignmentOptions.Center, accent,
                        new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(1400f, 44f));
    }

    private void BuildGameOver(RectTransform root)
    {
        overPanel = new GameObject("Game Over", typeof(RectTransform));
        overPanel.transform.SetParent(root, false);
        Stretch(overPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        // Fully opaque. At 94% the pans and the HUD were still clearly visible through it,
        // because the eye picks up small absolute differences against a dark background.
        Image bg = overPanel.AddComponent<Image>();
        bg.color = new Color(0.043f, 0.047f, 0.031f, 1f);

        RectTransform panel = overPanel.GetComponent<RectTransform>();

        PixelText title = MakeText("Title", panel, 78, TextAlignmentOptions.Top, ink,
                               new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(1200f, 96f));
        title.Text = "SHIFT OVER";

        PixelText caption = MakeText("Caption", panel, 22, TextAlignmentOptions.Top, inkDim,
                                 new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(600f, 30f));
        caption.Text = "USEFULNESS";

        overScore = MakeText("Score", panel, 100, TextAlignmentOptions.Top, accent,
                         new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(600f, 120f));

        overLabels = MakeText("Stat Labels", panel, 28, TextAlignmentOptions.TopLeft, inkDim,
                          new Vector2(0.5f, 1f), new Vector2(-300f, -430f), new Vector2(420f, 340f));

        overValues = MakeText("Stat Values", panel, 28, TextAlignmentOptions.TopRight, ink,
                          new Vector2(0.5f, 1f), new Vector2(300f, -430f), new Vector2(420f, 340f));

        SetLineSpacing(overLabels, statRowGap);
        SetLineSpacing(overValues, statRowGap);

        overRecord = MakeText("Record", panel, 30, TextAlignmentOptions.Top, inkDim,
                          new Vector2(0.5f, 1f), new Vector2(0f, -790f), new Vector2(900f, 40f));

        overBoard = MakeText("Board", panel, 22, TextAlignmentOptions.TopLeft, inkDim,
                         new Vector2(1f, 1f), new Vector2(-60f, -150f), new Vector2(400f, 500f));
        overBoard.main.enableWordWrapping = false;
        overBoard.shadow.enableWordWrapping = false;
        SetLineSpacing(overBoard, statRowGap * 0.4f);

        nameField = InputField("Name", panel, new Vector2(-160f, -840f), new Vector2(300f, 54f));

        Button submit = MakeButton("Submit", panel, "SUBMIT", new Vector2(170f, -840f), new Vector2(220f, 54f), accent);
        submit.onClick.AddListener(SubmitScore);
        submitButton = submit.gameObject;
        submitButton.SetActive(false);

        Button again = MakeButton("Restart", panel, "CLOCK BACK IN", new Vector2(-180f, -930f), new Vector2(320f, 66f), accent);
        again.onClick.AddListener(Restart);

        Button menu = MakeButton("Menu", panel, "MAIN MENU", new Vector2(180f, -930f), new Vector2(320f, 66f), inkDim);
        menu.onClick.AddListener(QuitToMenu);

        overPanel.SetActive(false);
    }

    // --- small builders ---

    // Applied to both copies, or the shadow drifts out of register with the main text.
    private static void SetLineSpacing(PixelText t, float spacing)
    {
        t.main.lineSpacing = spacing;
        t.shadow.lineSpacing = spacing;
    }

    // Named MakeText, not Text, so it can never be confused with UnityEngine.UI.Text.
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

    private Button MakeButton(string name, RectTransform parent, string label,
                              Vector2 position, Vector2 size, Color tint)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(tint.r, tint.g, tint.b, 0.14f);

        Button b = go.AddComponent<Button>();
        b.targetGraphic = img;

        Anchor(go.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), position, size);

        PixelText text = MakeText(name + " Text", go.GetComponent<RectTransform>(), 26,
                              TextAlignmentOptions.Center, tint,
                              new Vector2(0.5f, 0.5f), Vector2.zero, size);
        text.Text = label;

        return b;
    }

    private TMP_InputField InputField(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(ink.r, ink.g, ink.b, 0.10f);

        Anchor(go.GetComponent<RectTransform>(), new Vector2(0.5f, 1f), position, size);

        GameObject area = new GameObject("Text Area", typeof(RectTransform));
        area.transform.SetParent(go.transform, false);
        area.AddComponent<RectMask2D>();
        Stretch(area.GetComponent<RectTransform>(), new Vector2(12f, 4f), new Vector2(-12f, -4f));

        TextMeshProUGUI text = RawText("Text", area.GetComponent<RectTransform>(), 26, TextAlignmentOptions.Left, ink);
        Stretch(text.rectTransform, Vector2.zero, Vector2.zero);

        TextMeshProUGUI hint = RawText("Placeholder", area.GetComponent<RectTransform>(), 26, TextAlignmentOptions.Left, inkDim);
        Stretch(hint.rectTransform, Vector2.zero, Vector2.zero);
        hint.text = "YOUR NAME";

        TMP_InputField field = go.AddComponent<TMP_InputField>();
        field.textViewport = area.GetComponent<RectTransform>();
        field.textComponent = text;
        field.placeholder = hint;
        field.characterLimit = 12;
        field.targetGraphic = img;

        return field;
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
