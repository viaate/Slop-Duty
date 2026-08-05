using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Menu easter egg: hover a team portrait and they animate.
//
// Takes a whole frame list rather than a fixed rest-plus-wave pair, because the three of
// you have different numbers of frames. Frame 0 is the resting pose and everything after
// it is the loop, so two frames and three frames both just work.
//
// Deliberately not built on SpriteAnimator, which drives a SpriteRenderer. These portraits
// are UI Images, a completely different component, which is why that one would not work.
//
// Runs on unscaled time so it keeps going regardless of what timeScale is doing.
[RequireComponent(typeof(Image))]
public class PortraitWave : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Image image;
    private Sprite[] frames;
    private float framesPerSecond = 2f;

    private bool hovering;
    private int index;
    private float timer;

    private int FrameCount => frames == null ? 0 : frames.Length;

    public void Setup(Sprite[] portraitFrames, float fps)
    {
        image = GetComponent<Image>();

        frames = portraitFrames;
        framesPerSecond = Mathf.Max(0.1f, fps);

        if (image == null) return;

        ShowFrame(0);

        // Only listens for the pointer when there is actually something to play. A
        // portrait with a single frame ignores hovers rather than silently swallowing
        // pointer events for no visible reason.
        image.raycastTarget = FrameCount > 1;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (FrameCount < 2) return;

        hovering = true;
        timer = 0f;

        // Straight to frame 1, so the arms move the instant you hover rather than after
        // a beat of nothing.
        ShowFrame(1);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovering = false;
        ShowFrame(0);
    }

    private void Update()
    {
        if (!hovering || FrameCount < 2) return;

        timer += Time.unscaledDeltaTime;

        float frameTime = 1f / framesPerSecond;
        if (timer < frameTime) return;

        timer -= frameTime;
        ShowFrame((index + 1) % FrameCount);
    }

    private void ShowFrame(int which)
    {
        if (FrameCount == 0) return;

        index = Mathf.Clamp(which, 0, FrameCount - 1);

        Sprite frame = frames[index];
        if (frame != null && image != null) image.sprite = frame;
    }
}
