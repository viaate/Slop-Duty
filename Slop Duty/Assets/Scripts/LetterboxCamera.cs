using UnityEngine;

// Pins the camera to a fixed aspect ratio and puts bars around whatever is left over.
//
// Without this, an orthographic camera keeps its HEIGHT fixed and lets its WIDTH float
// with the window, so a wider screen simply reveals more world. That is why the counter
// stops reaching the edges in fullscreen: it is 18 units wide, which exactly covers a
// 16:9 view at orthographic size 5, and nothing wider.
//
// Fixing it here rather than by stretching the counter keeps every pixel square. Scaling
// a 36 by 9 sprite horizontally to fit an ultrawide would make its pixels into rectangles
// while everything else in the scene stayed square.
[RequireComponent(typeof(Camera))]
public class LetterboxCamera : MonoBehaviour
{
    [Tooltip("The aspect the game is composed for. 16:9 is 1.7778.")]
    [SerializeField] private float targetAspect = 16f / 9f;

    [Tooltip("Fills the area outside the bars. Without this the leftover region can smear " +
             "whatever was last in the frame buffer.")]
    [SerializeField] private Color barColor = Color.black;

    private Camera cam;
    private Camera bars;
    private int lastW, lastH;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        BuildBarCamera();
        Apply();
    }

    // Cheap enough to poll. Unity has no reliable resolution-changed event, and the
    // WebGL canvas can resize at any time when the player changes browser zoom.
    private void Update()
    {
        if (Screen.width == lastW && Screen.height == lastH) return;
        Apply();
    }

    private void Apply()
    {
        lastW = Screen.width;
        lastH = Screen.height;

        if (lastH <= 0 || targetAspect <= 0f) return;

        float windowAspect = (float)lastW / lastH;
        float scale = windowAspect / targetAspect;

        if (scale < 1f)
        {
            // Window is narrower than the target, so bars go top and bottom.
            cam.rect = new Rect(0f, (1f - scale) * 0.5f, 1f, scale);
        }
        else
        {
            // Window is wider, so bars go left and right.
            float width = 1f / scale;
            cam.rect = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }
    }

    private void BuildBarCamera()
    {
        GameObject go = new GameObject("Letterbox Bars");
        go.transform.SetParent(transform, false);

        bars = go.AddComponent<Camera>();
        bars.clearFlags = CameraClearFlags.SolidColor;
        bars.backgroundColor = barColor;
        bars.cullingMask = 0;                 // draws nothing, only clears
        bars.orthographic = true;
        bars.depth = cam.depth - 1;           // behind the real camera
        bars.rect = new Rect(0f, 0f, 1f, 1f); // always the whole window
    }
}
