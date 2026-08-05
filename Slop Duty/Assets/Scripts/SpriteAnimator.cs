using UnityEngine;

// Cycles a SpriteRenderer through a list of frames. Unity cannot play a GIF, it only
// ever imports the first frame, so animation here means several sprites plus something
// that swaps them. This is that, without the Animator and Animation Clip setup, which is
// far more machinery than a 3 frame bubbling loop needs.
//
// It only ever writes SpriteRenderer.sprite and never touches .color, so PukePuddle's
// opacity fade stacks on top of this cleanly.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(0.1f)] private float framesPerSecond = 6f;
    [SerializeField] private bool loop = true;

    [Tooltip("Start on a random frame, so several copies on screen do not pulse in unison.")]
    [SerializeField] private bool randomStartFrame = true;

    private SpriteRenderer visual;
    private float timer;
    private int index;

    private void Awake()
    {
        visual = GetComponent<SpriteRenderer>();

        if (frames == null || frames.Length == 0) return;

        if (randomStartFrame) index = Random.Range(0, frames.Length);
        visual.sprite = frames[index];
    }

    private void Update()
    {
        if (frames == null || frames.Length < 2) return;

        float frameTime = 1f / framesPerSecond;

        timer += Time.deltaTime;
        if (timer < frameTime) return;

        int advance = Mathf.FloorToInt(timer / frameTime);
        timer -= advance * frameTime;
        index += advance;

        if (index >= frames.Length)
        {
            if (loop)
            {
                index %= frames.Length;
            }
            else
            {
                index = frames.Length - 1;
                enabled = false;
            }
        }

        visual.sprite = frames[index];
    }
}
