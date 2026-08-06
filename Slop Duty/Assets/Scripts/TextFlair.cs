using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public enum TextEffect
{
    Wave,    // rolls up and down
    Shake,   // rattles, for anything that is a warning
    Pop,     // breathes bigger and smaller
    Tilt,    // rocks side to side
}

// One colour and one movement. A list of these is what stops every tooltip looking the
// same: each marked word takes the next entry, so consecutive lines never share a look.
[System.Serializable]
public class Emphasis
{
    public Color color = new Color(0.647f, 0.212f, 0.145f);
    public TextEffect effect = TextEffect.Wave;
}

// Picks key words out of a line and makes them move.
//
// Anything wrapped in asterisks gets coloured and animated, so a tooltip reads as "click a
// PAN" rather than as a flat sentence somebody has to parse. One marker does both jobs on
// purpose: a word worth colouring is a word worth pointing at.
//
//   Click a *pan* to load that color.
//
// The colour half is a rich text tag, which TextMeshPro already understands. The movement
// half cannot be, because there is no tag for it: TMP builds every letter into one mesh, so
// making a few of them move means reaching into that mesh and shifting the four corners of
// each letter by hand, every frame.
public class TextFlair : MonoBehaviour
{
    private const char Marker = '*';

    private TMP_Text label;

    // Which emphasis each character belongs to, or -1 for ordinary text.
    private int[] styleOf;
    private Emphasis[] palette;

    // True when at least one word is actually marked, and the letters where TMP put them.
    // Both worked out once, when the wording changes, rather than every frame.
    private bool marked;
    private Vector3[][] resting;
    private int rebuilds;

    private float amplitude;
    private float speed = 5f;
    private float spread = 0.55f;

    // start offsets which entry the first marked word takes, so the tooltip after this one
    // does not open with the same colour this one closed on.
    public string Apply(string source, Emphasis[] styles, int start, float wobble, float wobbleSpeed)
    {
        label = GetComponent<TMP_Text>();

        palette = styles;
        amplitude = wobble;
        speed = wobbleSpeed;

        if (string.IsNullOrEmpty(source) || styles == null || styles.Length == 0)
        {
            styleOf = null;
            return source ?? string.Empty;
        }

        StringBuilder built = new StringBuilder(source.Length + 48);
        List<int> flags = new List<int>(source.Length);

        int current = -1;
        int cursor = start;

        foreach (char c in source)
        {
            if (c == Marker)
            {
                if (current >= 0)
                {
                    built.Append("</color>");
                    current = -1;
                }
                else
                {
                    current = ((cursor % styles.Length) + styles.Length) % styles.Length;
                    cursor++;

                    built.Append($"<color=#{ColorUtility.ToHtmlStringRGB(styles[current].color)}>");
                }

                continue;
            }

            built.Append(c);

            // One entry per character TMP will actually lay out. Tags are not characters as
            // far as TMP is concerned, which is why they are appended without adding a flag:
            // it keeps this list index-aligned with textInfo.characterInfo.
            flags.Add(current);
        }

        // An unbalanced marker would otherwise leak the colour into everything after it.
        if (current >= 0) built.Append("</color>");

        styleOf = flags.ToArray();

        marked = false;
        for (int i = 0; i < styleOf.Length && !marked; i++) marked = styleOf[i] >= 0;

        // New wording means the cached letter positions are stale. Four rebuilds rather than
        // one because SpeechBubble re-fits the bubble over the next couple of frames, which
        // moves every letter, and a cache taken before that settles would be wrong.
        resting = null;
        rebuilds = 4;

        return built.ToString();
    }

    // LateUpdate, not Update, so this runs after anything that might have relaid the text
    // out this frame.
    private void LateUpdate()
    {
        if (label == null || styleOf == null || palette == null || amplitude <= 0f) return;

        // Nothing marked means nothing to animate at all.
        if (!marked) return;

        // Laid out ONCE and then remembered, instead of rebuilt sixty times a second.
        //
        // This was the single most expensive thing in the game. Every frame began with
        // ForceMeshUpdate, a full re-layout of the text, purely to undo the previous frame's
        // letter offsets. Copying the resting positions back does the same job for the cost
        // of an array copy, and the layout only actually changes when the wording does.
        TMP_TextInfo info = label.textInfo;

        if (rebuilds > 0 || Stale(info))
        {
            rebuilds--;

            label.ForceMeshUpdate();
            info = label.textInfo;

            if (info == null) return;

            Remember(info);
        }
        else
        {
            Restore(info);
        }

        if (info == null) return;

        bool moved = false;
        int count = Mathf.Min(info.characterCount, styleOf.Length);

        for (int i = 0; i < count; i++)
        {
            int style = styleOf[i];
            if (style < 0 || style >= palette.Length) continue;

            TMP_CharacterInfo ch = info.characterInfo[i];

            // Spaces occupy an index but have no corners to move.
            if (!ch.isVisible) continue;

            Vector3[] verts = info.meshInfo[ch.materialReferenceIndex].vertices;
            int v = ch.vertexIndex;

            // Each letter sits a little further along than the one before, so a marked
            // phrase ripples instead of moving as one block.
            float phase = (Time.unscaledTime * speed) + (i * spread);

            Distort(palette[style].effect, verts, v, phase, i);
            moved = true;
        }

        if (moved) label.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
    }

    // True when the cache no longer lines up with what TMP is holding, which happens if
    // anything relaid the text without going through Apply. Cheaper to check than to be
    // wrong about, since being wrong means writing past the end of a vertex array.
    private bool Stale(TMP_TextInfo info)
    {
        if (info == null || resting == null) return true;
        if (resting.Length != info.meshInfo.Length) return true;

        for (int m = 0; m < resting.Length; m++)
        {
            if (resting[m] == null || resting[m].Length != info.meshInfo[m].vertices.Length) return true;
        }

        return false;
    }

    private void Remember(TMP_TextInfo info)
    {
        resting = new Vector3[info.meshInfo.Length][];

        for (int m = 0; m < resting.Length; m++)
        {
            Vector3[] source = info.meshInfo[m].vertices;

            resting[m] = new Vector3[source.Length];
            System.Array.Copy(source, resting[m], source.Length);
        }
    }

    private void Restore(TMP_TextInfo info)
    {
        for (int m = 0; m < resting.Length; m++)
        {
            System.Array.Copy(resting[m], info.meshInfo[m].vertices, resting[m].Length);
        }
    }

    private void Distort(TextEffect effect, Vector3[] verts, int v, float phase, int index)
    {
        switch (effect)
        {
            case TextEffect.Shake:
            {
                // Perlin rather than Random, so it rattles smoothly instead of strobing,
                // and so a given letter's path is repeatable rather than a new draw every
                // frame. Sampled on two different rows to keep x and y independent.
                float x = (Mathf.PerlinNoise(index * 7.1f, phase) - 0.5f) * amplitude * 2f;
                float y = (Mathf.PerlinNoise((index * 7.1f) + 33f, phase) - 0.5f) * amplitude * 2f;

                Offset(verts, v, new Vector3(x, y, 0f));
                break;
            }

            case TextEffect.Pop:
            {
                float scale = 1f + (Mathf.Sin(phase) * 0.16f);
                Scale(verts, v, scale);
                break;
            }

            case TextEffect.Tilt:
            {
                float angle = Mathf.Sin(phase) * 9f;
                Rotate(verts, v, angle);
                break;
            }

            default:
                Offset(verts, v, new Vector3(0f, Mathf.Sin(phase) * amplitude, 0f));
                break;
        }
    }

    private static void Offset(Vector3[] verts, int v, Vector3 shift)
    {
        verts[v + 0] += shift;
        verts[v + 1] += shift;
        verts[v + 2] += shift;
        verts[v + 3] += shift;
    }

    // Both of the below work about the letter's own middle. Around the origin instead, a
    // letter at the end of a line would be flung across the bubble rather than growing or
    // rocking where it stands.
    private static void Scale(Vector3[] verts, int v, float scale)
    {
        Vector3 mid = Middle(verts, v);

        for (int k = 0; k < 4; k++) verts[v + k] = mid + ((verts[v + k] - mid) * scale);
    }

    private static void Rotate(Vector3[] verts, int v, float degrees)
    {
        Vector3 mid = Middle(verts, v);
        Quaternion turn = Quaternion.Euler(0f, 0f, degrees);

        for (int k = 0; k < 4; k++) verts[v + k] = mid + (turn * (verts[v + k] - mid));
    }

    private static Vector3 Middle(Vector3[] verts, int v) => (verts[v + 0] + verts[v + 2]) * 0.5f;
}
