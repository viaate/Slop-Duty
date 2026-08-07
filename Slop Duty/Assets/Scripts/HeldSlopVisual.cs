using UnityEngine;
using UnityEngine.UI;

// Shows a scoop of whatever slop is currently selected, trailing the mouse pointer.
//
// This replaces the hand-drawn tinted cursor. Unity cannot read the operating system's arrow
// bitmap, so recoloring "the normal cursor" meant drawing a replacement arrow, and a pixel
// arrow at cursor size always reads as chunky next to the real one. Leaving the system cursor
// alone and putting the color beside it keeps the pointer crisp and shows more information:
// the actual slop art, not just a tint.
//
// It draws as a UI image on a canvas of its own, not as the world sprite it used to be.
//
// The WE'RE OUT button lives on a Screen Space Overlay canvas, and an overlay canvas is
// composited after the camera has finished drawing the world. No world sprite can appear in
// front of it at any sorting order. The old version sat at sortingOrder 1000, the highest in
// the scene, and still went behind the button, because this was never a sorting problem. The
// only way for the scoop to sit on top is for it to be UI too, on a canvas that sorts above
// the rest.
[RequireComponent(typeof(SpriteRenderer))]
public class HeldSlopVisual : MonoBehaviour
{
    [Tooltip("The slop blob art, usually slop.PNG. Recolored to whatever is scooped up.")]
    [SerializeField] private Sprite blobSprite;

    [Tooltip("Offset from the pointer, in world units. Down and right keeps it clear of the " +
             "arrow tip, which is the part you aim with.")]
    [SerializeField] private Vector2 offset = new Vector2(0.35f, -0.35f);

    [SerializeField] private float scale = 0.6f;

    [Tooltip("Hide entirely when nothing is scooped, so an empty hand reads as empty.")]
    [SerializeField] private bool hideWhenEmpty = true;

    [Tooltip("Above the scene canvas holding WE'RE OUT (0), the HUD (100) and the tutorial " +
             "tooltips (110), and below the scene fader (5000) so a wipe still covers it.")]
    [SerializeField] private int canvasOrder = 4000;

    private Camera cam;
    private Canvas canvas;
    private RectTransform slopRect;
    private Image slopImage;

    private void Awake()
    {
        cam = Camera.main;

        // The scene object still carries the original SpriteRenderer. Its art is the sensible
        // default for blobSprite, and then it is switched off: leaving it on would draw a
        // second scoop in the world, behind the button, which is the thing being fixed.
        SpriteRenderer worldSprite = GetComponent<SpriteRenderer>();

        if (worldSprite != null)
        {
            if (blobSprite == null) blobSprite = worldSprite.sprite;
            worldSprite.enabled = false;
        }

        BuildOverlay();
    }

    private void BuildOverlay()
    {
        GameObject canvasGo = new GameObject("Held Slop Canvas");

        canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasOrder;

        // Deliberately no CanvasScaler and no GraphicRaycaster. Without a scaler the canvas
        // scaleFactor stays at 1, so its RectTransforms measure in raw screen pixels and the
        // conversion below is a straight world-units-to-pixels multiply. Without a raycaster
        // nothing here can ever intercept a click.

        GameObject slopGo = new GameObject("Held Slop", typeof(RectTransform));
        slopGo.transform.SetParent(canvasGo.transform, false);

        slopRect = (RectTransform)slopGo.transform;
        slopImage = slopGo.AddComponent<Image>();

        // Not optional. This image is under the pointer every single frame, and Unity
        // suppresses OnMouseDown while the pointer is over a raycast-blocking graphic, so
        // leaving this on would stop the player picking up slop or pressing WE'RE OUT at all.
        slopImage.raycastTarget = false;
    }

    // Built as a root object so an overlay canvas is not nested under a moving world
    // transform, which means it does not go away with this component on its own.
    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }

    // LateUpdate so it lands on the pointer after everything else has moved for the frame.
    // In Update it can trail a frame behind on a fast flick.
    private void LateUpdate()
    {
        if (slopImage == null) return;

        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Slop held = SlopLogic.Instance != null ? SlopLogic.Instance.GetSelectedSlop() : null;
        bool holding = held != null && held.GetIsSelected();

        // Gone the moment the shift ends.
        //
        // The scoop sorts above the HUD on purpose, which also puts it above the game over
        // panel, and that panel is deliberately opaque so nothing bleeds through it. The
        // selection survives a serve by design and the clock running out does not clear it,
        // so without this a blob of slop follows the pointer around on top of SHIFT OVER,
        // the score and the leaderboard for the whole screen. Polled rather than driven off
        // the RunEnded event so there is no subscription to get wrong.
        bool over = GameManager.Instance != null && GameManager.Instance.RunOver;

        bool show = !over && (holding || !hideWhenEmpty);

        // SetActive on the object rather than enabled on the image. The second color of a
        // double is a child image, and disabling a parent image does not hide its children,
        // which used to leave half a slop following the pointer after the player put a double
        // down. SetActive takes the whole subtree.
        if (slopImage.gameObject.activeSelf != show) slopImage.gameObject.SetActive(show);

        if (!show || blobSprite == null) return;

        // World units to screen pixels.
        //
        // cam.pixelHeight rather than Screen.height, because LetterboxCamera hands the camera
        // a sub-viewport on any window that is not 16:9. Measuring against the whole window
        // would make the scoop drift out of scale with the game inside the black bars.
        float pxPerUnit = cam.pixelHeight / Mathf.Max(0.0001f, cam.orthographicSize * 2f);

        // Both serialized values keep the meaning they had as a world sprite, so nothing in
        // the inspector needs re-tuning after the move to UI.
        Vector2 size = (Vector2)blobSprite.bounds.size * scale * pxPerUnit;
        if (slopRect.sizeDelta != size) slopRect.sizeDelta = size;

        slopRect.position = (Vector2)Input.mousePosition + (offset * pxPerUnit);

        if (!holding) return;

        MixPainter.Paint(slopImage, blobSprite,
                         SlopLogic.Instance != null ? SlopLogic.Instance.CounterRightHalf : null,
                         held.GetMix());
    }
}
