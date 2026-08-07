using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// A character who pops up in a corner of the menu while the pointer is over him, and ducks
// back down when it leaves.
//
// Built for the teachers, one per bottom corner. Taneim was drawn as two sequences, seven
// frames rising and eight ducking, which is not a walk cycle: he sinks out of shot and
// comes back. So the menu treats him as somebody peeking up to check on you, which is the
// only reading that uses the art as drawn.
//
// The second teacher has no frames, and rather than leaving him as a portrait stuck to the
// wall while the other one plays, he gets the SAME behaviour done a different way: he
// slides up from below the screen edge. Sliding is the animation, so no frames are needed,
// and the two read as a pair doing the same thing rather than as one animated character
// and one afterthought.
//
// The whole thing is ONE number, how far up he is, moved toward wherever the pointer says
// it should be. It started out as a state machine with a phase for rising and another for
// ducking, and that version snapped horribly when the pointer crossed the edge quickly,
// because entering restarted the rise from its first frame no matter where he had got to.
// A position that is only ever moved toward a target cannot do that: reversing mid way
// just changes which way it is travelling from exactly where it is.
public class MenuPeeker : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private RectTransform artRect;
    private Image art;
    private CanvasGroup nameGroup;

    private Sprite[] rise;
    private Sprite[] duck;
    private Sprite[] arrived;

    private float fps = 12f;
    private float slideSeconds = 0.45f;
    private float height;
    private float restPeek;

    // 0 is as hidden as he gets, 1 is fully up. Everything below is derived from it.
    private float progress;
    private bool hovered;

    // Where the arrived sequence has got to, and the leftover time since it last stepped.
    private int arrivedIndex;
    private float arrivedTimer;

    // Frames if there are any, sliding if there are not. Decided once here rather than
    // checked at every branch below.
    private bool Framed => rise != null && rise.Length > 0 && duck != null && duck.Length > 0;

    private bool HasArrived => arrived != null && arrived.Length > 0;

    // Each direction takes as long as its own sequence, so the seven frame rise and the
    // eight frame duck both play at their drawn speed rather than being forced to match.
    private float RiseRate => Framed ? fps / Mathf.Max(1f, rise.Length) : 1f / slideSeconds;
    private float DuckRate => Framed ? fps / Mathf.Max(1f, duck.Length) : 1f / slideSeconds;

    public void Setup(RectTransform image, CanvasGroup label, Sprite[] riseFrames,
                      Sprite[] duckFrames, Sprite[] arrivedFrames, Sprite stillFrame,
                      float framesPerSecond, float artHeight, float restingPeek)
    {
        artRect = image;
        art = image != null ? image.GetComponent<Image>() : null;
        nameGroup = label;

        rise = riseFrames;
        duck = duckFrames;
        arrived = arrivedFrames;

        fps = Mathf.Max(1f, framesPerSecond);
        height = artHeight;
        restPeek = Mathf.Clamp01(restingPeek);

        // The resting picture.
        //
        // The first arrived frame beats the still, and deliberately so: it IS his resting
        // pose, being what he looks like the instant before he starts performing. Preferring
        // a separate still would draw one picture all the way up the slide and then swap to
        // a different one the moment he got to the top, which pops.
        if (!Framed && art != null)
        {
            if (HasArrived) SetSprite(arrived[0]);
            else if (stillFrame != null) SetSprite(stillFrame);
        }

        progress = 0f;
        arrivedIndex = 0;
        arrivedTimer = 0f;

        Apply();
    }

    // The pointer is caught by a fixed invisible box in the corner, not by the character.
    // He spends most of his time hidden, and something that has to be hovered before it
    // will appear cannot itself be the thing you hover.
    public void OnPointerEnter(PointerEventData eventData) => hovered = true;

    public void OnPointerExit(PointerEventData eventData) => hovered = false;

    // Unscaled, matching the rest of the interface. The menu never freezes time, but a
    // mixed convention is how a menu ends up frozen on some screen nobody tested.
    private void Update()
    {
        if (artRect == null) return;

        float target = hovered ? 1f : 0f;
        float rate = hovered ? RiseRate : DuckRate;

        progress = Mathf.MoveTowards(progress, target, rate * Time.unscaledDeltaTime);

        Apply();

        // Tied straight to how far up he is, so it arrives with him and leaves with him
        // without needing to be sequenced separately. A caption hanging in the corner over
        // an empty space is the giveaway that somebody is hiding just off screen.
        if (nameGroup != null) nameGroup.alpha = progress;

        // After Apply, so that on the frames where the sequence owns the picture it writes
        // last and wins.
        StepArrived();
    }

    // What he does once he has finished arriving: Al flicks his eyes about and then reaches
    // up to scratch his head.
    //
    // Kept out of Apply and off the progress number entirely, because the two are different
    // kinds of thing. Progress is a POSITION, and the reason the peek never snaps is that a
    // position can only ever be moved toward a target and so reverses from wherever it got
    // to. This is a PERFORMANCE, which has a first frame and a last one.
    //
    // So it gets the same treatment anyway. The frame index is also just moved toward a
    // target, the end of the sequence while he is up and the start of it while he is not,
    // which means taking the pointer away unwinds him back to a resting pose at the same
    // speed he played it instead of cutting his arm back to his side in one frame. There is
    // no drawing of the arm coming down, and this is what stands in for one.
    private void StepArrived()
    {
        if (!HasArrived || art == null) return;

        int want = progress >= 1f ? arrived.Length - 1 : 0;

        // Fully unwound and not going anywhere, so the picture goes back to whoever else
        // wants it. That is what lets a framed character play his own rise and duck frames
        // on the way in and out and still perform once he is up.
        if (want == 0 && arrivedIndex == 0)
        {
            arrivedTimer = 0f;
            return;
        }

        if (arrivedIndex == want) arrivedTimer = 0f;
        else
        {
            arrivedTimer += Time.unscaledDeltaTime;

            float frameTime = 1f / Mathf.Max(1f, fps);

            // A while loop rather than one step per frame, so a hitch skips frames instead
            // of stretching the whole sequence out.
            while (arrivedTimer >= frameTime && arrivedIndex != want)
            {
                arrivedTimer -= frameTime;
                arrivedIndex += arrivedIndex < want ? 1 : -1;
            }
        }

        SetSprite(arrived[Mathf.Clamp(arrivedIndex, 0, arrived.Length - 1)]);
    }

    // Guarded, because assigning to Image.sprite marks the canvas dirty and Unity then
    // rebuilds and rebatches the whole thing. Most frames of a 12fps sequence are a repeat
    // of the one before, and this runs every frame on a screen that is doing nothing else.
    private void SetSprite(Sprite next)
    {
        if (art == null || next == null || art.sprite == next) return;

        art.sprite = next;
    }

    private void Apply()
    {
        if (!Framed)
        {
            // Eased so he arrives rather than stopping dead, which is what sells it as
            // somebody leaning in.
            float eased = 1f - ((1f - progress) * (1f - progress));

            // Lerped up from restPeek rather than from nothing, so a sliver of him is
            // always showing. See the note on Resting Peek in MenuUI: a character who is
            // completely invisible until hovered is a secret, not a feature.
            float shown = Mathf.Lerp(restPeek, 1f, eased);
            artRect.anchoredPosition = new Vector2(artRect.anchoredPosition.x, (shown - 1f) * height);

            return;
        }

        if (art == null) return;

        // Indexed by how far up he is rather than by elapsed time. Which sequence to read
        // from depends on which way he is going, and because both are the same movement
        // drawn in opposite directions, the same position maps to the matching frame in
        // either one. That is what makes a reversal mid duck look continuous.
        Sprite[] frames = hovered ? rise : duck;
        float along = hovered ? progress : 1f - progress;

        int index = Mathf.Clamp(Mathf.RoundToInt(along * (frames.Length - 1)), 0, frames.Length - 1);
        SetSprite(frames[index]);
    }
}
