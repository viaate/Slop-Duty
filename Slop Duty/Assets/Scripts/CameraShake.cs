using UnityEngine;

// Knocks the camera about for a moment. Used when a boss lands.
//
// Offsets a child of whatever the camera is parented under rather than the camera's own
// transform, so nothing else that moves the camera has to know this exists and the shake
// always settles back to exactly where it started rather than drifting.
public class CameraShake : MonoBehaviour
{
    private Vector3 home;
    private float strength;
    private float remaining;
    private float total;

    // Found or added on demand, so callers do not need a reference and the component does
    // not have to be on the camera before it is first used.
    public static void Shake(float amount, float seconds)
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            foreach (Camera c in Camera.allCameras)
            {
                if (c.cullingMask == 0) continue;

                cam = c;
                break;
            }
        }

        if (cam == null) return;

        CameraShake shake = cam.GetComponent<CameraShake>();
        if (shake == null) shake = cam.gameObject.AddComponent<CameraShake>();

        shake.Begin(amount, seconds);
    }

    private void Begin(float amount, float seconds)
    {
        // Only captured when a shake is not already running, or a second knock mid shake
        // would take the shaken position as home and leave the camera permanently offset.
        if (remaining <= 0f) home = transform.localPosition;

        strength = Mathf.Max(strength, amount);
        total = Mathf.Max(0.01f, seconds);
        remaining = total;
    }

    // Unscaled, so a knock still reads if it lands on the frame something freezes time.
    private void Update()
    {
        if (remaining <= 0f) return;

        remaining -= Time.unscaledDeltaTime;

        if (remaining <= 0f)
        {
            transform.localPosition = home;
            strength = 0f;
            return;
        }

        // Falls off over the shake rather than stopping dead, which is the difference
        // between an impact and a glitch.
        float falloff = remaining / total;
        float amount = strength * falloff * falloff;

        // Perlin rather than Random, so the camera travels a continuous path instead of
        // teleporting somewhere new every frame. Two different rows keep the axes apart.
        float t = Time.unscaledTime * 40f;
        float x = (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * amount;
        float y = (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * amount;

        transform.localPosition = home + new Vector3(x, y, 0f);
    }
}
