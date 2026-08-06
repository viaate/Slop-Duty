using UnityEngine;

// Player settings, saved between sessions.
//
// Static and PlayerPrefs backed rather than a component, because these are read from
// everywhere: the menu writes them, the palette generator and the pans read them, and none
// of that should depend on a particular object existing in a particular scene.
public static class Settings
{
    private const string VolumeKey = "SlopDuty.Volume";
    private const string VisionKey = "SlopDuty.ColorVision";

    private static float volume = 1f;
    private static ColorVision vision = ColorVision.Normal;
    private static bool loaded;

    public static float Volume
    {
        get { Load(); return volume; }
        set
        {
            Load();
            volume = Mathf.Clamp01(value);

            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.Save();

            Apply();
        }
    }

    public static ColorVision Vision
    {
        get { Load(); return vision; }
        set
        {
            Load();
            vision = value;

            PlayerPrefs.SetInt(VisionKey, (int)vision);
            PlayerPrefs.Save();
        }
    }

    // Nothing needs to repaint when this changes, because it can only be changed from the
    // menu and starting a shift loads a fresh scene which generates its palette from
    // scratch. If settings ever open mid-run, that stops being true.

    // True whenever the player has told us they cannot rely on color alone. Shapes ride
    // along with it rather than being a second switch: somebody who needs the palette
    // adjusted needs the shapes too, and a setting nobody would ever want half of is a
    // setting that should not be split in two.
    public static bool UseSymbols => Vision != ColorVision.Normal;

    // Applied before the first scene loads, so the volume is already right rather than
    // snapping a frame into the menu music.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        Load();
        Apply();
    }

    private static void Load()
    {
        if (loaded) return;
        loaded = true;

        volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        vision = (ColorVision)PlayerPrefs.GetInt(VisionKey, (int)ColorVision.Normal);
    }

    // One listener volume rather than a level on each source. Music and the serve sounds
    // both run through it, so there is nothing to keep in sync and nothing to forget when
    // a new sound is added later.
    private static void Apply() => AudioListener.volume = volume;
}
