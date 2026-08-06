using UnityEngine;

// Keeps the game filling the window at any shape, with no bars, by zooming instead of
// padding.
//
// The problem it solves: an orthographic camera holds its HEIGHT fixed and lets its WIDTH
// float with the window. So a wider window does not scale the game up, it reveals more
// world off the sides, which is why the counter stops reaching the edges in fullscreen.
// The counter is 18 units across, which exactly covers a 16:9 view at orthographic size 5
// and nothing wider.
//
// There are only two honest fixes for that, and they cannot both be had:
//
//   Letterbox   hold 16:9 exactly and put black bars in the leftover space. The
//               composition is always identical, and the bars are always visible on any
//               window that is not 16:9.
//   Cover       zoom until the window is full and let the excess axis crop. No bars ever,
//               and a very wide window sees slightly less floor and ceiling.
//
// This is Cover, which is what a game that should "just scale" wants. LetterboxCamera in
// this same folder is the other one. Only one of them belongs on the camera, and nothing
// currently uses LetterboxCamera, so it is safe to delete.
[RequireComponent(typeof(Camera))]
public class CameraFit : MonoBehaviour
{
    [Tooltip("World width the scene is composed for. The counter is 18 units across, so " +
             "anything wider than this starts showing past its ends.")]
    [SerializeField] private float designWidth = 18f;

    [Tooltip("World height the scene is composed for. 10 is orthographic size 5 doubled, " +
             "which is what the background art covers.")]
    [SerializeField] private float designHeight = 10f;

    private Camera cam;
    private int lastW, lastH;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        Apply();
    }

    // Cheap enough to poll. Unity has no reliable resolution-changed event, and a WebGL
    // canvas can resize at any moment when the player changes browser zoom.
    private void Update()
    {
        if (Screen.width == lastW && Screen.height == lastH) return;
        Apply();
    }

    private void Apply()
    {
        lastW = Screen.width;
        lastH = Screen.height;

        if (cam == null || !cam.orthographic || lastH <= 0) return;

        float aspect = (float)lastW / lastH;
        if (aspect <= 0f) return;

        // Whichever constraint bites first. Half the design height keeps the top and bottom
        // of the art in frame; half the design width divided by the aspect keeps the ends
        // of the counter in frame. Taking the smaller guarantees the view never extends
        // past the art on either axis, which is the same thing as never showing a gap.
        float byHeight = designHeight * 0.5f;
        float byWidth = (designWidth * 0.5f) / aspect;

        cam.orthographicSize = Mathf.Max(0.01f, Mathf.Min(byHeight, byWidth));

        // Anything a letterbox setup left behind. A rect narrower than the full window is
        // exactly what draws the bars, and leaving one here would undo all of the above.
        cam.rect = new Rect(0f, 0f, 1f, 1f);
    }
}
