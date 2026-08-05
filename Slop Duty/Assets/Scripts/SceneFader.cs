using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Iris transition. The screen closes to a shrinking circle, the scene loads behind the
// black, then the circle opens again. Fast on purpose: it exists to hide the hitch of a
// scene load, not to be admired.
//
// Survives the load itself via DontDestroyOnLoad, and creates itself on demand, so there
// is nothing to place in either scene.
//
// The mask is a texture regenerated per frame rather than a scaled sprite with a hole in
// it. A fixed donut sprite cannot both close to nothing and still cover the screen corners,
// because its hole and its black border scale together. Redrawing is cheap here: the
// texture is deliberately low resolution, which also gives the chunky pixel edge that
// suits the rest of the game.
public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance { get; private set; }

    [Tooltip("Seconds to close. Short, because the player is waiting on it.")]
    [SerializeField] private float closeSeconds = 0.25f;

    [SerializeField] private float openSeconds = 0.32f;

    [Tooltip("Width of the mask texture. Lower is chunkier. 320 across a 1920 screen is " +
             "a 6x pixel, which matches the art.")]
    [SerializeField, Range(64, 640)] private int maskWidth = 320;

    [SerializeField] private Color curtain = new Color(0.043f, 0.047f, 0.031f);

    // Largest slice of time any single frame may contribute to an animation, so one long
    // frame cannot swallow a whole transition.
    private const float MaxStep = 1f / 30f;

    private Canvas canvas;
    private Image image;
    private Texture2D mask;
    private Color32[] pixels;
    private int texW, texH;
    private bool busy;

    public bool Busy => busy;

    // Call this instead of SceneManager.LoadScene anywhere you want the wipe.
    public static void Go(string sceneName)
    {
        Ensure().Begin(sceneName);
    }

    public static SceneFader Ensure()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("Scene Fader");
        return go.AddComponent<SceneFader>();
    }

    // Deliberately does nothing on game start. An earlier version irised in on whatever
    // scene the game launched into, which reads as an odd stutter on the main menu: there
    // is nothing to transition away from, so the wipe has no meaning. The iris only ever
    // plays when it is actually covering a scene change.

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        Build();
        SetRadius(1f);
        Show(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Begin(string sceneName)
    {
        if (busy) return;

        busy = true;
        StartCoroutine(Run(sceneName));
    }

    private IEnumerator Run(string sceneName)
    {
        Show(true);

        yield return Animate(1f, 0f, closeSeconds);

        SceneManager.LoadScene(sceneName);

        // Two frames, not one. The first is the load itself and is enormously long; the
        // second is the first normal frame of the new scene. Starting the reveal on the
        // long frame is what made it snap.
        yield return null;
        yield return null;

        yield return Animate(0f, 1f, openSeconds);

        Show(false);
        busy = false;
    }

    private IEnumerator Animate(float from, float to, float seconds)
    {
        float t = 0f;
        float length = Mathf.Max(0.01f, seconds);

        while (t < length)
        {
            // Unscaled, because the game over screen freezes timeScale and the menu button
            // there still needs this to run.
            //
            // Clamped, because the frame straight after a scene load can be several hundred
            // milliseconds long. Unclamped, that one frame advances t past the whole
            // duration and the open animation finishes instantly, which looks exactly like
            // the scene snapping in with no wipe at all.
            t += Mathf.Min(Time.unscaledDeltaTime, MaxStep);

            SetRadius(Mathf.Lerp(from, to, t / length));
            yield return null;
        }

        SetRadius(to);
    }

    private void Show(bool on)
    {
        if (canvas != null) canvas.enabled = on;
        if (image != null) image.raycastTarget = on;   // swallows clicks mid-wipe
    }

    // radius01: 1 is fully open with the hole past the corners, 0 is fully closed.
    private void SetRadius(float radius01)
    {
        if (mask == null) return;

        float cx = texW * 0.5f;
        float cy = texH * 0.5f;
        float maxR = Mathf.Sqrt((cx * cx) + (cy * cy));
        float r = Mathf.Clamp01(radius01) * maxR;
        float rSq = r * r;

        Color32 solid = curtain;
        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < texH; y++)
        {
            float dy = y + 0.5f - cy;
            float dySq = dy * dy;
            int row = y * texW;

            for (int x = 0; x < texW; x++)
            {
                float dx = x + 0.5f - cx;
                pixels[row + x] = (dx * dx) + dySq <= rSq ? clear : solid;
            }
        }

        mask.SetPixels32(pixels);
        mask.Apply(false);
    }

    private void Build()
    {
        GameObject canvasGo = new GameObject("Fader Canvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above every other canvas in the project, so nothing can draw over the wipe.
        canvas.sortingOrder = 5000;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Sized to the screen's aspect so the hole renders round rather than as an
        // ellipse once the texture is stretched across the display.
        float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 16f / 9f;
        texW = Mathf.Max(16, maskWidth);
        texH = Mathf.Max(16, Mathf.RoundToInt(texW / Mathf.Max(0.1f, aspect)));

        mask = new Texture2D(texW, texH, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };

        pixels = new Color32[texW * texH];

        GameObject imageGo = new GameObject("Iris", typeof(RectTransform));
        imageGo.transform.SetParent(canvasGo.transform, false);

        RectTransform rt = imageGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        image = imageGo.AddComponent<Image>();
        image.sprite = Sprite.Create(mask, new Rect(0f, 0f, texW, texH), new Vector2(0.5f, 0.5f),
                                     100f, 0, SpriteMeshType.FullRect);
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
    }
}
