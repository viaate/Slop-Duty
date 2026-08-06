using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One tooltip: a speech bubble with writing inside it.
//
// The bubble is drawn as ONE image at ONE uniform whole-number scale, exactly as it was
// painted. No nine slicing, no tiling, no stretching to fit.
//
// Three earlier attempts each changed the drawing's proportions and each looked wrong in
// its own way. The bubbles carry single-pixel specks along their edges to look worn.
// Slicing blew every speck into a wide bar. Tiling repeated the strip, so cracks appeared
// at even intervals where the artist never drew one. Stretching squashed the whole shape
// and made the pixels rectangles while every other pixel on screen stayed square.
//
// Scaling by a whole number does none of that. The cost is that the writing has to fit the
// shape the bubble already is, so a squarish bubble gets a squarish block of text rather
// than one wide line, and long wording wants the wide bubble.
public class SpeechBubble : MonoBehaviour
{
    // Which part of the 32x32 PNG is the bubble, in source pixels with y counted from the
    // BOTTOM, which is how Unity indexes textures and the opposite of an image editor.
    //
    // Use the -alt art, the versions with no tail. The tailed ones have their bottom row
    // eaten by the tail junction.
    //
    //   bubble 1   body (1, 7, 30, 25)   inset (3, 3, 3, 3)
    //   bubble 2   body (0, 12, 32, 12)  inset (3, 2, 3, 2)
    //   bubble 3   body (0, 11, 32, 19)  inset (5, 4, 5, 7)   cloud, its dome eats the top
    [System.Serializable]
    public class Style
    {
        public Sprite art;

        [Tooltip("The bubble inside the sprite, in source pixels. Trims the empty space " +
                 "around it so the drawing fills the tooltip rather than floating in it.")]
        public Rect body = new Rect(1f, 7f, 30f, 25f);

        [Tooltip("How far in from each edge the writing has to start, in source pixels: " +
                 "left, bottom, right, top. This is the painted outline plus a little air, " +
                 "so it is SMALL, usually two or three. It is not a nine slice border and " +
                 "big numbers here inflate the bubble enormously.")]
        public Vector4 inset = new Vector4(3f, 3f, 3f, 3f);
    }

    private const float ScreenMargin = 28f;

    private RectTransform rect;
    private CanvasGroup group;
    private TextMeshProUGUI label;

    private Vector4 inset;
    private Vector2 bodySize;
    private float maxWidth;
    private int extraSteps;
    private string content = string.Empty;
    private int refits;
    private bool closing;

    public void Build(RectTransform parent, Style style, TMP_FontAsset font, Color textColor,
                      int fontSize, float widthLimit, int room)
    {
        maxWidth = widthLimit;
        extraSteps = Mathf.Max(0, room);
        inset = style.inset;
        bodySize = new Vector2(Mathf.Max(1f, style.body.width), Mathf.Max(1f, style.body.height));

        rect = GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;   // a tooltip must never eat a click meant for a pan

        GameObject art = new GameObject("Bubble", typeof(RectTransform));
        art.transform.SetParent(rect, false);

        Image img = art.AddComponent<Image>();
        img.sprite = Cut(style);
        img.type = Image.Type.Simple;
        img.raycastTarget = false;

        RectTransform ar = img.rectTransform;
        ar.anchorMin = Vector2.zero;
        ar.anchorMax = Vector2.one;
        ar.offsetMin = Vector2.zero;
        ar.offsetMax = Vector2.zero;

        label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(rect, false);

        if (font != null) label.font = font;

        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = true;
    }

    // Trimmed to the bubble itself. Sprite.Create does not need the texture to be readable,
    // unlike GetPixels, so nothing has to change in the import settings.
    private Sprite Cut(Style style)
    {
        if (style.art == null || style.art.texture == null)
        {
            Debug.LogWarning("SpeechBubble: a tooltip style has no sprite assigned, so the " +
                             "bubble behind the text will be missing. Assign the -alt " +
                             "speech-bubble art to Tutorial's Styles list.");
            return null;
        }

        // Offset by the sprite's own rect so this still works if the art ends up inside a
        // packed atlas, where sprite.texture is the whole sheet rather than one bubble.
        Rect src = new Rect(style.art.rect.x + style.body.x, style.art.rect.y + style.body.y,
                            style.body.width, style.body.height);

        return Sprite.Create(style.art.texture, src, new Vector2(0.5f, 0.5f), 100f,
                             0, SpriteMeshType.FullRect);
    }

    public void SetText(string text)
    {
        content = text;
        refits = 0;

        Fit();
    }

    private void Fit()
    {
        if (label == null) return;

        string text = content;
        label.text = text;

        // Forces the glyphs to be built before anything is measured. Without it the very
        // first measurement in a scene can come back from a cold font atlas and be wrong,
        // which mis-sizes whichever bubble happens to be first while every later one is
        // fine. Update re-fits for a couple of frames as well, since even this is not a
        // guarantee on the frame a material is first created.
        label.ForceMeshUpdate();

        // ONE uniform whole-number scale for both axes. Nothing is stretched, so the bubble
        // keeps the exact proportions it was drawn at and its pixels stay square.
        float innerCols = Mathf.Max(1f, bodySize.x - inset.x - inset.z);
        float innerRows = Mathf.Max(1f, bodySize.y - inset.y - inset.w);

        int biggest = Mathf.Max(2, Mathf.FloorToInt(maxWidth / bodySize.x));

        // The writing is laid out to the same SHAPE as the space it has to sit in, and the
        // bubble is then sized to that.
        //
        // Two earlier goes at this both failed on shape. Taking the first size the text
        // merely fitted gave a cramped column stranded in a wide bubble. Forcing it onto
        // one line gave the opposite: a 4.3 to 1 box holding a single 17 to 1 line, mostly
        // empty above and below, wrapping raggedly the moment it ran out of width and
        // leaving one word alone on the second row.
        //
        // Neither is a padding problem. A block of text has a shape, the hole has a shape,
        // and they have to be the same shape. For a line of total length L split over k
        // rows the block is L/k wide by k line-heights tall, so matching that to the box
        // solves to k = sqrt(L / (aspect * lineHeight)). Splitting at L/k then falls out
        // evenly, which is what kills the orphan.
        float oneLine = label.GetPreferredValues(text).x;
        float lineHeight = Mathf.Max(1f, label.GetPreferredValues("Ag").y);
        float boxAspect = Mathf.Max(0.05f, innerCols / innerRows);

        int lines = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(oneLine / (boxAspect * lineHeight))));

        // A little slack, because words are indivisible and an exact split usually pushes
        // the last one over.
        float wrapWidth = (oneLine / lines) * 1.08f;
        int scale = Mathf.Clamp(Mathf.CeilToInt(wrapWidth / innerCols), 2, biggest);

        // Backstop for wording long enough that the estimate lands short.
        while (scale < biggest &&
               label.GetPreferredValues(text, innerCols * scale, 0f).y > innerRows * scale)
        {
            scale++;
        }

        // Grown past the tightest fit so the writing sits in the bubble rather than filling
        // it to the edges. Whole steps only, because a fractional one would put the picture
        // back on a half pixel and undo the reason the scale is an integer at all.
        //
        // The text is untouched by this: it keeps its own size and simply gains air around
        // it, since the label is inset from a bigger rect.
        scale = Mathf.Min(scale + extraSteps, biggest);

        rect.sizeDelta = new Vector2(bodySize.x * scale, bodySize.y * scale);

        RectTransform lr = label.rectTransform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = new Vector2(inset.x * scale, inset.y * scale);
        lr.offsetMax = new Vector2(-inset.z * scale, -inset.w * scale);
    }

    // Centred on the point and kept fully on screen.
    public void PlaceOn(Vector2 canvasLocal, Vector2 canvasSize)
    {
        if (rect == null) return;

        float w = rect.sizeDelta.x;
        float h = rect.sizeDelta.y;

        float halfW = canvasSize.x * 0.5f;
        float halfH = canvasSize.y * 0.5f;

        float x = Mathf.Clamp(canvasLocal.x, -halfW + (w * 0.5f) + ScreenMargin,
                                              halfW - (w * 0.5f) - ScreenMargin);
        float y = Mathf.Clamp(canvasLocal.y, -halfH + (h * 0.5f) + ScreenMargin,
                                              halfH - (h * 0.5f) - ScreenMargin);

        rect.anchoredPosition = new Vector2(x, y);
    }

    public void Close() => closing = true;

    // Unscaled, like the rest of the interface, because the run ends by freezing timeScale
    // and a tooltip that cannot fade out would sit on top of the game over screen.
    private void Update()
    {
        // Re-measured for the first couple of frames. TMP settles its layout a frame after
        // a label is created, so the size worked out during Build can be stale. Cheap, and
        // it happens while the bubble is still fading in so nothing visibly jumps.
        if (refits < 2)
        {
            refits++;
            Fit();
        }

        float target = closing ? 0f : 1f;
        float fade = Mathf.MoveTowards(group != null ? group.alpha : 0f, target,
                                       Time.unscaledDeltaTime * 6f);

        if (group != null) group.alpha = fade;

        if (closing && fade <= 0f) Destroy(gameObject);
    }
}
