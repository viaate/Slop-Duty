using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The sorry-we're-out control. Pressing it dismisses the kid at the front of the line
// immediately, rather than arming a mode that then needs a second click on a specific kid.
//
// Works either as a world sprite with a Collider2D, or as a UI Button on a Canvas. Those
// are two completely separate click systems in Unity: OnMouseDown only fires on world
// colliders and never on UI, while UI goes through the EventSystem and Button.onClick.
//
// LOOK: built to match a slop pan rather than to match the HUD. A pan is a pale plate with
// a darker olive edge and a hard black drop shadow down and to the right. Earlier versions
// of this button were flat UI rectangles, which shared no vocabulary with anything else on
// screen and read as a web widget parked on the game. This is made of the same parts.
//
// Field names carry a plate prefix because two earlier versions used names like fillColor
// and borderColor with different meanings, and Unity restores serialized values by field
// name, so any reused name silently keeps the old color.
//
// NOTE: the filename must stay SlopOutButton.cs. Unity requires a MonoBehaviour's
// filename to match its class name.
public class SlopOutButton : MonoBehaviour
{
    [Header("Look")]
    [Tooltip("Off if you would rather style the button by hand in the Inspector.")]
    [SerializeField] private bool autoStyle = true;

    [Tooltip("Same pixel font asset you put on GameUI.")]
    [SerializeField] private TMP_FontAsset pixelFont;

    [SerializeField] private string label = "WE'RE OUT";

    [Tooltip("Largest the text will go. It shrinks on its own if the button is narrower.")]
    [SerializeField] private int maxFontSize = 26;

    [Header("Plate, matched to the slop pan art")]
    [Tooltip("The pale interior, taken from slop-container.")]
    [SerializeField] private Color plateColor = new Color(0.800f, 0.859f, 0.714f);

    [Tooltip("Flash on press.")]
    [SerializeField] private Color platePressed = new Color(1f, 1f, 0.92f);

    [Tooltip("The darker rim, taken from slop-container.")]
    [SerializeField] private Color plateEdge = new Color(0.275f, 0.376f, 0.082f);
    [SerializeField] private float plateEdgeWidth = 4f;

    [Tooltip("Hard offset shadow, the same trick the pans use. No softness on purpose.")]
    [SerializeField] private Color plateShadow = new Color(0f, 0f, 0f, 0.85f);
    [SerializeField] private Vector2 plateShadowOffset = new Vector2(10f, -10f);

    [Tooltip("Dark text on a pale plate needs no shadow of its own.")]
    [SerializeField] private Color plateInk = new Color(0.043f, 0.047f, 0.031f);

    [SerializeField] private float flashSeconds = 0.12f;

    private SpriteRenderer buttonRenderer;   // world sprite version
    private Image buttonImage;               // UI version, the raycast target
    private Image plateImage;                // what actually shows the color
    private StudentQueue queue;
    private float flashUntil;

    private void Awake()
    {
        buttonRenderer = GetComponent<SpriteRenderer>();
        buttonImage = GetComponent<Image>();

        Button uiButton = GetComponent<Button>();
        if (uiButton != null)
        {
            uiButton.onClick.AddListener(SendFrontAway);

            // Forced off rather than trusting it to be set by hand. Unity's default Color
            // Tint transition overwrites Image.color on hover and press, which would stomp
            // the flash below and make the button look dead.
            if (autoStyle) uiButton.transition = Selectable.Transition.None;
        }

        if (autoStyle) ApplyStyle();
    }

    private void Start()
    {
        queue = FindFirstObjectByType<StudentQueue>();
        Refresh();
    }

    private void ApplyStyle()
    {
        TextMeshProUGUI text = GetComponentInChildren<TextMeshProUGUI>();

        if (buttonImage != null)
        {
            // The root goes invisible but keeps taking clicks. A child always draws in
            // front of its parent's own Image, so the drop shadow could not sit behind the
            // plate if the root were the plate.
            buttonImage.sprite = null;
            buttonImage.type = Image.Type.Simple;
            buttonImage.color = Color.clear;
            buttonImage.raycastTarget = true;

            BuildPlate();
        }

        if (text == null) return;

        if (pixelFont != null) text.font = pixelFont;

        text.text = label;
        text.color = plateInk;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;

        // Auto sizing so the label can never overflow, whatever size the button ends up.
        text.enableAutoSizing = true;
        text.fontSizeMin = 8f;
        text.fontSizeMax = maxFontSize;

        // Small inset so the text is not flush against the rim.
        RectTransform tr = text.rectTransform;
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 6f);
        tr.offsetMax = new Vector2(-12f, -6f);

        text.transform.SetAsLastSibling();
    }

    private void BuildPlate()
    {
        // Drawn first, so it sits behind everything else.
        Image shadow = MakeLayer("Drop Shadow", plateShadow);
        RectTransform sr = shadow.rectTransform;
        sr.offsetMin = plateShadowOffset;
        sr.offsetMax = plateShadowOffset;

        Image rim = MakeLayer("Plate Edge", plateEdge);

        plateImage = MakeLayer("Plate", plateColor, rim.rectTransform);
        RectTransform pr = plateImage.rectTransform;
        pr.offsetMin = new Vector2(plateEdgeWidth, plateEdgeWidth);
        pr.offsetMax = new Vector2(-plateEdgeWidth, -plateEdgeWidth);
    }

    private Image MakeLayer(string name, Color color, RectTransform parent = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent != null ? parent : transform, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        return img;
    }

    // World-object path. Requires a Collider2D on this object.
    private void OnMouseDown() => SendFrontAway();

    // UI path, and safe to call from anywhere.
    public void SendFrontAway()
    {
        if (queue == null) queue = FindFirstObjectByType<StudentQueue>();
        if (queue == null) return;

        Student front = queue.Front;
        if (front == null) return;

        IndividualStudent kid = front.GetComponent<IndividualStudent>();
        if (kid == null || kid.IsResolved) return;

        kid.AnswerOutOfStock();

        flashUntil = Time.time + flashSeconds;
        Refresh();
    }

    private void Update()
    {
        if (flashUntil <= 0f || Time.time < flashUntil) return;

        flashUntil = 0f;
        Refresh();
    }

    private void Refresh()
    {
        Color c = Time.time < flashUntil ? platePressed : plateColor;

        if (buttonRenderer != null) buttonRenderer.color = c;
        if (plateImage != null) plateImage.color = c;
    }
}
