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

    // So the tutorial can borrow it rather than needing the same asset dragged into a
    // second slot, which is a slot somebody will eventually forget and then wonder why one
    // part of the game is suddenly in Unity's default sans.
    public TMP_FontAsset Font => pixelFont;

    [Header("Palette")]
    [SerializeField] private Color ink = new Color(0.851f, 0.898f, 0.769f);
    [SerializeField] private Color inkDim = new Color(0.541f, 0.596f, 0.427f);
    [SerializeField] private Color accent = new Color(0.788f, 0.843f, 0.290f);
    [SerializeField] private Color danger = new Color(0.878f, 0.298f, 0.239f);
    [SerializeField] private Color shadowInk = new Color(0.043f, 0.047f, 0.031f, 0.85f);

    [Tooltip("The perk name only. A cold color on purpose: everything else on the HUD is in " +
             "the warm end of the palette, so a boost reads as something else entirely " +
             "rather than as another line of the same status text.")]
    [SerializeField] private Color boostInk = new Color(0.416f, 0.831f, 0.902f);

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

    [Tooltip("How fast the displayed number catches up to the real one. Higher is snappier. " +
             "12 spins a five second penalty out over roughly a third of a second.")]
    [SerializeField] private float rollSpeed = 12f;

    [Tooltip("Below this the display stops chasing and reads the clock exactly. Without it " +
             "the label can still say 3 while the run is already over, which shows the " +
             "player a death they had no way to see coming.")]
    [SerializeField] private float snapBelowSeconds = 5f;

    [Header("Time popups")]
    [Tooltip("Height above the student the number starts at, in screen pixels at 1080p.")]
    [SerializeField] private float popupHeadroom = 150f;

    [Tooltip("Rewards happen every single serve, so this is deliberately the smaller and " +
             "faster of the two. If it were as loud as the penalty it would be exhausting " +
             "inside a minute.")]
    [SerializeField] private int popupGainSize = 44;
    [SerializeField] private float popupGainLife = 0.75f;
    [SerializeField] private float popupGainRise = 90f;

    [Tooltip("Penalties are rare and matter, so this one is allowed to take up space.")]
    [SerializeField] private int popupLossSize = 66;
    [SerializeField] private float popupLossLife = 1.15f;
    [SerializeField] private float popupLossRise = 130f;

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
    private PixelText bossLabel;
    private PixelText bossPrize;
    private PixelText boostLabel;
    private BossDirector bossDirector;
    private bool searchedForBoss;

    private GameObject hudRoot;
    private RectTransform popupLayer;
    private RectTransform canvasRect;
    private Camera worldCam;

    // The number on screen, which lags the real one on purpose. See the clock block in
    // Update. rollReady is false until the first frame of a shift, so a new day snaps to
    // its starting time instead of winding up to it from whatever was left of the last.
    private float shownTime;
    private bool rollReady;

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

        // Both setters bail out when nothing changed, and that is the single biggest win in
        // this file.
        //
        // The HUD assigns most of these every frame. Handing TextMeshPro a string rebuilds
        // its mesh whether or not the string differs, and writing a Graphic's color marks
        // the canvas dirty, which makes Unity rebuild and rebatch the entire UI. So the day
        // name, the week, the quota and the clock tint were each paying for a full rebuild
        // sixty times a second to display exactly what they displayed last frame.
        public string Text
        {
            set
            {
                if (main.text == value) return;

                main.text = value;
                shadow.text = value;
            }
        }

        public Color Tint
        {
            set
            {
                if (main.color == value) return;
                main.color = value;
            }
        }
        public GameObject Root => root.gameObject;
    }

    private bool built;

    private void Awake() => EnsureBuilt();

    // Called from Awake and again from Update. If anything ever stops Awake from finishing,
    // the interface rebuilds itself on the next frame instead of throwing a null every
    // frame forever with no way to recover.
    private void EnsureBuilt()
    {
        if (built) return;

        built = true;
        Build();

        if (dayLabel == null)
        {
            Debug.LogError($"{name}: GameUI finished building but its labels are null. " +
                           "Something threw inside Build, so look for an earlier exception " +
                           "in the console above this one.", this);
        }
    }

    private void Start()
    {
        game = GameManager.Instance;
        if (game == null) game = FindFirstObjectByType<GameManager>();

        timer = FindFirstObjectByType<LevelTimer>();

        if (game == null) return;

        game.DayStarted += ShowDayCard;
        game.RunEnded += ShowGameOver;
        game.TimeChanged += ShowTimePopup;

        ShowDayCard(game.Day);
    }

    private void OnDestroy()
    {
        // Dropped separately from the manager's, since this one is subscribed lazily the
        // first frame a director is found rather than in Start.
        if (bossDirector != null) bossDirector.Resolved -= OnBossResolved;

        if (game == null) return;

        game.DayStarted -= ShowDayCard;
        game.RunEnded -= ShowGameOver;
        game.TimeChanged -= ShowTimePopup;
    }

    // Unscaled throughout, because the run ends by freezing timeScale and a game over
    // screen that cannot animate itself in is worse than none.
    private void Update()
    {
        EnsureBuilt();

        RefreshHud();
        TickCard();
        PollBoard();
    }

    // --- HUD ---

    private void RefreshHud()
    {
        // Guarded individually rather than assuming Build succeeded, because a single null
        // here used to throw every frame and bury whatever the real first error was.
        if (game != null && dayLabel != null)
        {
            dayLabel.Text = DayConfig.NameFor(game.Day);
            weekLabel.Text = game.Day <= 0 ? "TRAINING" : $"WEEK {DayConfig.WeekFor(game.Day)}";
            quotaLabel.Text = $"{game.ResolvedToday}/{game.QuotaToday}";
        }

        if (timer != null && clockLabel != null)
        {
            float left = timer.TimeRemaining;

            // The label chases the real value rather than printing it. A five second
            // penalty applied instantly is a jump from 34 to 29, and a jump is close to
            // invisible: there is no frame in which anything is moving. Spun over a third
            // of a second it becomes motion, which is the one thing peripheral vision is
            // genuinely good at catching.
            //
            // An exponential chase rather than a fixed tween because penalties can land on
            // top of each other. This retargets smoothly mid-roll, where a tween would
            // have to restart from wherever it had got to.
            if (!rollReady)
            {
                shownTime = left;
                rollReady = true;
            }
            else if (left <= snapBelowSeconds || !timer.Running)
            {
                shownTime = left;
            }
            else
            {
                float catchUp = 1f - Mathf.Exp(-rollSpeed * Time.unscaledDeltaTime);
                shownTime = Mathf.Lerp(shownTime, left, catchUp);
            }

            clockLabel.Text = Mathf.CeilToInt(Mathf.Max(0f, shownTime)).ToString();

            // Colour and pulse read the true value, never the displayed one. The warning
            // that you are nearly out has to be honest even while the digits are still
            // catching up to a penalty.
            bool low = left <= lowTimeSeconds && timer.Running;
            clockLabel.Tint = low ? danger : ink;

            // Snapped to whole steps rather than a smooth sine, so it ticks like a
            // sprite animation instead of breathing like a web page.
            float step = low && (Mathf.FloorToInt(Time.unscaledTime * 6f) % 2 == 0) ? 1.08f : 1f;

            // Only written when it actually changed. A transform write on a canvas child
            // dirties the whole canvas, and this value is 1 for all but the last ten seconds
            // of a day and then alternates six times a second, so almost every frame was
            // paying for a UI rebuild to set 1 to 1.
            if (!Mathf.Approximately(clockLabel.root.localScale.x, step))
                clockLabel.root.localScale = Vector3.one * step;
        }

        RefreshBoss();

        // No mess warning. The puddle growing and the vignette closing in already say it,
        // and a line of text shouting over the top was redundant.
    }

    // The boss banner: who is here, how many of their orders are done, and how long is left.
    //
    // Without a visible countdown the encounter has no tension, because the only way to
    // learn the limit exists is to hit it. The regular clock is still up in the corner doing
    // its own job, so this deliberately sits somewhere else and in the warning color.
    private void RefreshBoss()
    {
        if (bossLabel == null) return;

        // Searched ONCE, not every frame.
        //
        // A failed search left the field null and the next frame tried again, so in any
        // scene without a BossDirector this was a scene-wide FindFirstObjectByType sixty
        // times a second, forever. It is one of the most expensive calls in Unity.
        if (!searchedForBoss)
        {
            searchedForBoss = true;

            bossDirector = FindFirstObjectByType<BossDirector>();
            if (bossDirector != null) bossDirector.Resolved += OnBossResolved;
        }

        // The perk currently running, all week, every day. Separate from the boss banner
        // because the two answer different questions: this one is "what am I playing with",
        // the other is "what am I playing for".
        if (boostLabel != null && game != null)
        {
            // The name in the cold color, the effect in the ordinary accent. Both in one
            // tint they were indistinguishable, which is why a line meaning "BIG TIPS, and
            // here is what that does" read as a single sentence nobody finishes. Two colors
            // separate them without either half being hard to read.
            boostLabel.Text = WeekBoost.ActiveOn(game.Day)
                ? $"<color=#{ColorUtility.ToHtmlStringRGB(boostInk)}>{WeekBoost.Describe()}</color>" +
                  $"   {WeekBoost.Explain(WeekBoost.Kind)}"
                : string.Empty;
        }

        BossVisitor boss = bossDirector != null ? bossDirector.Active : null;

        if (boss == null || boss.Finished)
        {
            bossLabel.Text = string.Empty;
            if (bossPrize != null) bossPrize.Text = string.Empty;
            return;
        }

        bossLabel.Text = $"{boss.Owner}   {boss.Served}/{boss.Total}   {Mathf.CeilToInt(boss.Remaining)}";

        if (bossPrize != null) bossPrize.Text = $"WIN: {WeekBoost.Describe(boss.Prize)}";
    }

    // The moment it is won, on the same card a new day arrives on. Reusing that animation
    // rather than inventing a second one keeps the two announcements feeling like the same
    // game talking, and the card is already the place the player looks for news.
    private void OnBossResolved(BossVisitor boss)
    {
        if (boss == null) return;

        if (!boss.Won)
        {
            RaiseCard("TOO SLOW", $"{boss.Owner} LEFT WITHOUT THEIR ORDER", inkDim);
            cardDay.Tint = danger;
            return;
        }

        RaiseCard(WeekBoost.Describe(boss.Prize), WeekBoost.Explain(boss.Prize), accent);

        // The headline takes the boost color too, so the moment it is won and the reminder
        // that runs all week are recognisably the same thing.
        cardDay.Tint = boostInk;
    }

    // --- floating time numbers ---

    private void ShowTimePopup(float delta, Vector2 worldPoint)
    {
        if (popupLayer == null || canvasRect == null) return;

        if (worldCam == null) worldCam = FindWorldCamera();
        if (worldCam == null) return;

        // A reward the clock cap swallowed whole. Saying so outright is more use than a
        // "+0" or a silent nothing, both of which read as the feature being broken rather
        // than as the clock being full.
        bool wasted = Mathf.Abs(delta) < 0.05f;
        bool loss = delta < 0f && !wasted;

        string label = wasted ? "MAX" : (loss ? "-" : "+") + Mathf.Abs(delta).ToString("0.#");

        // One decimal, and only when there is one. Rewards are fractional on most days, so
        // rounding 2.5 up to "+3" would promise a second the clock never gave.
        Color tint = wasted ? inkDim : (loss ? danger : accent);

        // Screen space overlay, so the camera argument is deliberately null. Passing the
        // world camera there applies its projection a second time and puts every number in
        // the wrong place.
        Vector2 screen = worldCam.WorldToScreenPoint(worldPoint);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null,
                                                                out Vector2 local);

        PixelText text = MakeText("Time Popup", popupLayer, loss ? popupLossSize : popupGainSize,
                              TextAlignmentOptions.Center, tint, new Vector2(0.5f, 0.5f),
                              local + new Vector2(0f, popupHeadroom), new Vector2(400f, 90f));
        text.Text = label;

        TimePopup popup = text.Root.AddComponent<TimePopup>();
        popup.Play(loss ? popupLossRise : popupGainRise,
                   loss ? popupLossLife : popupGainLife,
                   loss ? 1.7f : 1.35f);
    }

    // Camera.main depends on the MainCamera tag, which is easy to lose on a scene edit.
    // The fallback skips any camera that draws nothing, because LetterboxCamera parents a
    // second camera whose only job is clearing the bars and whose projection would place
    // every popup wrongly.
    private Camera FindWorldCamera()
    {
        if (Camera.main != null) return Camera.main;

        foreach (Camera c in Camera.allCameras)
        {
            if (c.cullingMask != 0) return c;
        }

        return null;
    }

    // --- day card ---

    private void ShowDayCard(int day)
    {
        // Snaps the clock display to whatever the new shift starts on. Left chasing, it
        // would spend the first half second winding between the old day's leftover time
        // and the new one, which looks like the clock is still counting the last day.
        rollReady = false;

        if (cardDay == null) return;

        // Reminded on the card as well as kept in the corner. A perk announced once and then
        // hidden is indistinguishable from no perk by about Wednesday.
        if (WeekBoost.ActiveOn(day))
        {
            RaiseCard(DayConfig.NameFor(day),
                      $"WEEK {DayConfig.WeekFor(day)}   {WeekBoost.Describe()}", accent);
            return;
        }

        RaiseCard(DayConfig.NameFor(day),
                  day <= 0 ? "NOBODY COMES IN ON A SUNDAY" : $"WEEK {DayConfig.WeekFor(day)}",
                  inkDim);
    }

    // One entry point for anything that wants the big centre card, so a new day and a won
    // perk animate identically instead of drifting apart.
    private void RaiseCard(string headline, string note, Color noteTint)
    {
        if (cardDay == null) return;

        FitLine(cardDay, headline, 130, 1360f);
        FitLine(cardWeek, note, 30, 1360f);

        // Reset every time so a caller that wants a different headline color has to say so.
        // Without this a single boss win left every day name after it tinted like a perk.
        cardDay.Tint = ink;
        cardWeek.Tint = noteTint;

        cardTimer = 0f;
    }

    // Steps a card line down until it fits on ONE line.
    //
    // The card was built for day names, which are short. Perk names are not: SIMPLE PALETTE
    // at the day size wrapped, and a wrapped headline drops its second line straight onto
    // the note underneath. Wrapping is switched off so it cannot happen at all, and the size
    // comes down in whole steps so the pixel font stays on whole pixels.
    //
    // Measured every time rather than only when it overflows, so a long perk name on Monday
    // does not leave TUESDAY shrunk for the rest of the week.
    private static void FitLine(PixelText text, string value, int points, float width)
    {
        text.Text = value;

        text.main.enableWordWrapping = false;
        text.shadow.enableWordWrapping = false;

        for (; points > 24; points -= 6)
        {
            text.main.fontSize = points;
            if (text.main.GetPreferredValues(value).x <= width) break;
        }

        text.shadow.fontSize = text.main.fontSize;
    }

    private void TickCard()
    {
        if (cardGroup == null) return;

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

        // Shown as-is. Leaderboard already phrases these for a player to read, and
        // prefixing "SCOREBOARD OFFLINE" onto a rejected name would be nonsense. This is
        // also the only place the message is visible in a WebGL build, where there is no
        // console to check.
        if (!string.IsNullOrEmpty(board.LastError))
        {
            overBoard.Text = board.LastError;
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
        SceneFader.Go(SceneManager.GetActiveScene().name);
    }

    private void QuitToMenu()
    {
        Time.timeScale = 1f;
        SceneFader.Go("MainMenu");
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
        scaler.matchWidthOrHeight = 1f;   // lock to height, see note in MenuUI

        canvasGo.AddComponent<GraphicRaycaster>();

        RectTransform root = canvasGo.GetComponent<RectTransform>();
        canvasRect = root;

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

        // Top centre, between the day on the left and the clock on the right. Empty and
        // invisible until somebody drops in.
        bossLabel = MakeText("Boss", root, 40, TextAlignmentOptions.Top, danger,
                         new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(900f, 52f));

        // What is at stake, under the countdown. Without it the encounter is a timer with
        // no stated reward, and nobody pushes for a prize they have not been told about.
        bossPrize = MakeText("Boss Prize", root, 22, TextAlignmentOptions.Top, accent,
                         new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(900f, 30f));

        // The running perk, kept up all week under the week number. The day card announces
        // it once, and once is not enough for something that changes how five days play.
        // -120, not -112. The week label above runs to -114, so the old value overlapped it
        // by a couple of pixels, which on a pixel font is enough to look like a mistake.
        // Accent, not the dim ink. The dim was legible on paper and not on brick: the
        // background behind the HUD is a mid-tone wall, and a low contrast status line
        // disappears into it exactly where somebody has to read it at a glance.
        boostLabel = MakeText("Boost", root, 22, TextAlignmentOptions.TopLeft, accent,
                          new Vector2(0f, 1f), new Vector2(40f, -120f), new Vector2(1200f, 30f));

        // Never wraps. It was folding onto a second line and landing on the week number
        // above it, and a status line that grows downward into other status lines is worse
        // than one that runs long. Widened to match, so in practice it does neither.
        boostLabel.main.enableWordWrapping = false;
        boostLabel.shadow.enableWordWrapping = false;

        // Created last, so its children draw over the labels above. Sibling order is what
        // decides overlap on a canvas, and a popup passing behind the clock would look
        // like a glitch rather than a layer.
        GameObject layer = new GameObject("Time Popups", typeof(RectTransform));
        layer.transform.SetParent(root, false);

        popupLayer = layer.GetComponent<RectTransform>();
        Stretch(popupLayer, Vector2.zero, Vector2.zero);
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

        // The shadow ignores color tags and stays its own dark.
        //
        // Both halves of a PixelText carry the SAME string, so a <color> tag meant for the
        // writing recolored the shadow with it. The result was two bright copies a few
        // pixels apart, which reads as a smeared bold rather than as text with a shadow.
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
