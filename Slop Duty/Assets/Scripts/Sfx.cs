using UnityEngine;

// Builds its own sound effects from raw samples at runtime instead of playing imported
// clips.
//
// Two reasons that beats a pair of WAV files for these particular cues. Nobody has to
// source, import and wire them, so there is no Inspector step to forget. And an
// unassigned AudioClip fails silently, which is the exact failure these sounds exist to
// prevent: the whole point of them is to be readable while you are looking somewhere
// else, so "it quietly stopped working" is indistinguishable from "it is working".
//
// Everything here is deliberately tiny. These fire on every single serve, so they have to
// sit under the music rather than on top of it.
public static class Sfx
{
    private const int SampleRate = 44100;

    private static AudioSource source;
    private static AudioClip gainClip;
    private static AudioClip lossClip;

    // Short, quiet and rising. Gains happen constantly, once per student, so this is
    // pitched to register without ever becoming the thing you notice.
    public static void TimeGained()
    {
        if (gainClip == null) gainClip = Tone("Time Gained", 520f, 980f, 0.11f, 0.20f, 4f);
        Play(gainClip);
    }

    // Longer, louder and falling. Losses are rare and matter, so this one is allowed to
    // take up space. The asymmetry is the point: if the reward sound were this big it
    // would be exhausting inside a minute.
    public static void TimeLost()
    {
        if (lossClip == null) lossClip = Tone("Time Lost", 400f, 110f, 0.34f, 0.34f, 3f);
        Play(lossClip);
    }

    private static AudioClip Tone(string clipName, float startHz, float endHz,
                                  float seconds, float volume, float decay)
    {
        int count = Mathf.CeilToInt(SampleRate * seconds);
        float[] data = new float[count];
        float phase = 0f;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)count;

            phase += Mathf.Lerp(startHz, endHz, t) / SampleRate;
            if (phase > 1f) phase -= 1f;

            // Square rather than sine. The extra harmonics are what make it read as a
            // game sound instead of a test tone, and they let it stay audible over the
            // music at a much lower volume than a pure sine would need.
            float wave = phase < 0.5f ? 1f : -1f;

            // A 4ms fade in, because asking a speaker to jump from silence to full
            // amplitude in one sample is precisely what a click is. Exponential decay
            // after that so it stops without a second click at the tail.
            float attack = Mathf.Clamp01(i / (SampleRate * 0.004f));
            float envelope = attack * Mathf.Exp(-decay * t);

            data[i] = wave * envelope * volume;
        }

        AudioClip clip = AudioClip.Create(clipName, count, 1, SampleRate, false);
        clip.SetData(data, 0);

        return clip;
    }

    private static void Play(AudioClip clip)
    {
        EnsureSource();

        if (source == null || clip == null) return;
        source.PlayOneShot(clip);
    }

    private static void EnsureSource()
    {
        // Unity's overloaded null catches the case where the object was destroyed by a
        // scene load, so this rebuilds rather than silently going quiet for the rest of
        // the session.
        if (source != null) return;

        GameObject go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);

        source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;

        // Fully 2D. Left at the default the clip is treated as a point at the world
        // origin and pans to whichever side of the camera that happens to be on.
        source.spatialBlend = 0f;
    }
}
