using TMPro;
using UnityEngine;

// A "+3" or "-5" that rises off the student it belongs to and fades out.
//
// This exists because the clock is in the top right corner and nobody is looking at the
// corner. During play your eyes are on the queue and the pans, so a number that changes
// in the corner is information delivered to a place you are not watching. Putting it
// where the thing happened means the feedback lands where you were already looking.
//
// GameUI builds the text and drives this. Everything is created at runtime, so there is
// nothing to wire up and nothing to keep in sync in a prefab.
public class TimePopup : MonoBehaviour
{
    private RectTransform rect;
    private TextMeshProUGUI[] texts;
    private float[] baseAlpha;

    private Vector2 from;
    private float rise;
    private float life;
    private float punch;
    private float age;

    public void Play(float riseDistance, float lifetime, float punchScale)
    {
        rect = GetComponent<RectTransform>();
        texts = GetComponentsInChildren<TextMeshProUGUI>();

        // The drop shadow is deliberately not full opacity, so fading has to scale each
        // piece against what it started at rather than driving everything to the same
        // alpha and flattening the shadow into the text.
        baseAlpha = new float[texts.Length];
        for (int i = 0; i < texts.Length; i++) baseAlpha[i] = texts[i].color.a;

        from = rect.anchoredPosition;
        rise = riseDistance;
        life = Mathf.Max(0.01f, lifetime);
        punch = punchScale;
        age = 0f;

        Apply(0f);
    }

    // Unscaled, like the rest of the interface. The run ends by freezing timeScale, so on
    // scaled time the popup for the mistake that killed you would hang on screen forever.
    private void Update()
    {
        if (rect == null) return;

        age += Time.unscaledDeltaTime;

        float t = age / life;

        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        Apply(t);
    }

    private void Apply(float t)
    {
        // Decelerating rise. Most of the travel happens in the first few frames, so the
        // number is already moving fast at the instant it appears. Easing in instead
        // would spend that moment barely moving, which is where it is easiest to miss.
        float ease = 1f - ((1f - t) * (1f - t));
        rect.anchoredPosition = from + new Vector2(0f, rise * ease);

        // Overshoots on the opening fifth of its life, then settles. The snap is what
        // catches peripheral vision.
        float grow = t < 0.2f ? Mathf.Lerp(punch, 1f, t / 0.2f) : 1f;
        rect.localScale = Vector3.one * grow;

        // Holds full opacity for the first half so there is time to actually read it,
        // then fades over the second half. Fading across the whole life would mean it is
        // already dimming while you are still looking at it.
        float alpha = t < 0.5f ? 1f : 1f - ((t - 0.5f) / 0.5f);

        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i] == null) continue;

            Color c = texts[i].color;
            c.a = baseAlpha[i] * alpha;
            texts[i].color = c;
        }
    }
}
