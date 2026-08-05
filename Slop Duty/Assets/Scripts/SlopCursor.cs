using UnityEngine;

// Tints the mouse pointer to whichever slop is currently scooped up, so the color you
// are holding is always right where you are looking instead of down on the counter.
//
// The arrow is drawn in code rather than loaded from a file. Unity cannot read the
// operating system's own cursor bitmap, so "keep the normal arrow but recolor it" means
// drawing an arrow of our own. This keeps the familiar shape, a dark outline around a
// fill that takes the slop color.
public class SlopCursor : MonoBehaviour
{
    [SerializeField] private Color emptyColor = Color.white;
    [SerializeField] private Color outlineColor = new Color(0.08f, 0.07f, 0.10f);

    [Tooltip("Pixel size of the cursor. 2 is a good match for a pixel art game. " +
             "Windows caps hardware cursors at 32x32, and this arrow is 12x18, so 2 is " +
             "the highest that still fits.")]
    [SerializeField, Range(1, 2)] private int scale = 2;

    // X is outline, o is fill, space is transparent. Row 0 is the top, and the tip at
    // (0,0) is the click point.
    private static readonly string[] Arrow =
    {
        "X           ",
        "XX          ",
        "XoX         ",
        "XooX        ",
        "XoooX       ",
        "XooooX      ",
        "XoooooX     ",
        "XooooooX    ",
        "XoooooooX   ",
        "XooooooooX  ",
        "XoooooooooX ",
        "XooooooXXXXX",
        "XooXooX     ",
        "XoX XooX    ",
        "XX  XooX    ",
        "X    XooX   ",
        "     XooX   ",
        "      XX    ",
    };

    private Texture2D current;
    private Color applied;
    private bool everApplied;

    private void Start() => Apply(emptyColor);

    private void Update()
    {
        Color want = emptyColor;

        SlopLogic logic = SlopLogic.Instance;
        if (logic != null)
        {
            Slop held = logic.GetSelectedSlop();
            if (held != null && held.GetIsSelected()) want = held.GetColor();
        }

        if (everApplied && want == applied) return;

        Apply(want);
    }

    private void Apply(Color fill)
    {
        Texture2D previous = current;

        current = Build(fill);
        applied = fill;
        everApplied = true;

        Cursor.SetCursor(current, Vector2.zero, CursorMode.Auto);

        // Built fresh each time the color changes, so the old one has to go or the
        // textures pile up for the whole run.
        if (previous != null) Destroy(previous);
    }

    private Texture2D Build(Color fill)
    {
        int w = Arrow[0].Length * scale;
        int h = Arrow.Length * scale;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color clear = new Color(0f, 0f, 0f, 0f);
        Color[] pixels = new Color[w * h];

        for (int y = 0; y < h; y++)
        {
            // Texture2D counts up from the bottom, the art table reads down from the top.
            int row = Arrow.Length - 1 - (y / scale);

            for (int x = 0; x < w; x++)
            {
                char c = Arrow[row][x / scale];

                pixels[(y * w) + x] = c == 'X' ? outlineColor
                                    : c == 'o' ? fill
                                    : clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return tex;
    }

    private void OnDisable()
    {
        // Hand the pointer back, otherwise the tinted arrow survives into the editor
        // after you stop playing.
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

        if (current == null) return;

        Destroy(current);
        current = null;
        everApplied = false;
    }
}
