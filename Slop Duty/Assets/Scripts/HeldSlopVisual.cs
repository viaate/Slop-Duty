using UnityEngine;

// Shows a scoop of whatever slop is currently selected, trailing the mouse pointer.
//
// This replaces the hand-drawn tinted cursor. Unity cannot read the operating system's
// arrow bitmap, so recoloring "the normal cursor" meant drawing a replacement arrow, and
// a pixel arrow at cursor size always reads as chunky next to the real one. Leaving the
// system cursor alone and putting the color beside it keeps the pointer crisp and shows
// more information: the actual slop art, not just a tint.
[RequireComponent(typeof(SpriteRenderer))]
public class HeldSlopVisual : MonoBehaviour
{
    [Tooltip("The slop blob art, usually slop.PNG. Recolored to whatever is scooped up.")]
    [SerializeField] private Sprite blobSprite;

    [Tooltip("Offset from the pointer, in world units. Down and right keeps it clear of " +
             "the arrow tip, which is the part you aim with.")]
    [SerializeField] private Vector2 offset = new Vector2(0.35f, -0.35f);

    [SerializeField] private float scale = 0.6f;

    [Tooltip("Hide entirely when nothing is scooped, so an empty hand reads as empty.")]
    [SerializeField] private bool hideWhenEmpty = true;

    private SpriteRenderer visual;
    private Camera cam;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();
        cam = Camera.main;

        if (blobSprite == null) blobSprite = visual.sprite;
        transform.localScale = Vector3.one * scale;
    }

    // LateUpdate so it lands on the pointer after everything else has moved for the frame.
    // In Update it can trail a frame behind on a fast flick.
    private void LateUpdate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 world = cam.ScreenToWorldPoint(Input.mousePosition);
        world.z = transform.position.z;
        transform.position = world + new Vector3(offset.x, offset.y, 0f);

        Slop held = SlopLogic.Instance != null ? SlopLogic.Instance.GetSelectedSlop() : null;
        bool holding = held != null && held.GetIsSelected();

        visual.enabled = holding || !hideWhenEmpty;
        if (!holding || blobSprite == null) return;

        Sprite painted = SpriteRecolor.For(blobSprite, held.GetColor());

        if (painted != null)
        {
            visual.sprite = painted;
            visual.color = Color.white;
            return;
        }

        // Texture not readable, so fall back to tinting rather than showing nothing.
        visual.sprite = blobSprite;
        visual.color = held.GetColor();
    }
}
