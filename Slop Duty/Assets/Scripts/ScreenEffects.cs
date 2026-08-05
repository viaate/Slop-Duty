using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A vignette, and nothing else.
//
// Most post-processing actively hurts pixel art, because every effect works by blending
// neighbouring pixels and hard edges are the whole point of the art. Bloom softens the
// outlines, chromatic aberration puts colored fringes on them, film grain fights the flat
// color fields. A vignette is the exception: it only darkens the corners, so it pulls the
// eye to the counter without touching a single edge.
//
// It also does one job for the design. Intensity rises with how filthy the floor is, so
// the screen visibly closes in as you fall behind. That gives the mess a presence even
// when you are staring at the pans and not the floor.
//
// Built in code so there is no Volume Profile asset for three people to fight over.
public class ScreenEffects : MonoBehaviour
{
    [Header("Vignette")]
    [SerializeField, Range(0f, 1f)] private float baseIntensity = 0.25f;

    [Tooltip("Added on top at maximum filth. Keep the total well under 0.6 or it stops " +
             "reading as atmosphere and starts reading as a broken screen.")]
    [SerializeField, Range(0f, 0.5f)] private float filthIntensity = 0.20f;

    [SerializeField, Range(0.01f, 1f)] private float smoothness = 0.45f;
    [SerializeField] private Color vignetteColor = new Color(0.02f, 0.02f, 0.01f);

    [Tooltip("How fast it eases toward the target, so a fresh puddle darkens the corners " +
             "over a moment rather than snapping.")]
    [SerializeField] private float responseSpeed = 1.2f;

    private Vignette vignette;
    private float current;

    private void Awake()
    {
        // Post-processing is off by default on a fresh URP camera, and a Volume with
        // nothing rendering it is a silent no-op that looks exactly like a broken effect.
        Camera cam = Camera.main;
        if (cam != null)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null) data.renderPostProcessing = true;
        }

        GameObject holder = new GameObject("Vignette Volume");
        holder.transform.SetParent(transform, false);

        Volume volume = holder.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "Runtime Vignette";
        volume.profile = profile;

        vignette = profile.Add<Vignette>(true);
        vignette.color.Override(vignetteColor);
        vignette.smoothness.Override(smoothness);
        vignette.intensity.Override(baseIntensity);

        current = baseIntensity;
    }

    private void Update()
    {
        if (vignette == null) return;

        float filth = PukeManager.Instance != null ? PukeManager.Instance.Filth : 0f;
        float target = baseIntensity + (filth * filthIntensity);

        current = Mathf.MoveTowards(current, target, responseSpeed * Time.unscaledDeltaTime);
        vignette.intensity.Override(current);
    }
}
