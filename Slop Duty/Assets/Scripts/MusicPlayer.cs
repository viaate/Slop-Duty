using UnityEngine;

// One track, faded in on start and faded out on request. Used in both scenes: the menu
// puts a lobby track on it with the tempo ramp switched off, the game scene uses it for
// the shift music with the ramp on.
//
// The handover between scenes is a fade out then a fade in rather than a true crossfade.
// A real crossfade would need one music object surviving the scene load via
// DontDestroyOnLoad, which drags the whole menu's lifetime into the game scene and is a
// lot of machinery for a gap you cannot hear on a scene load this small.
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [Header("Lifetime")]
    [Tooltip("Keeps this object and its track alive across scene loads, so the same music " +
             "runs through the menu and the shift with no break at all. Needs to be a root " +
             "GameObject in the hierarchy.")]
    [SerializeField] private bool persistAcrossScenes = true;

    [SerializeField] private AudioClip track;

    [Tooltip("Volume once faded in.")]
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;

    [SerializeField] private float fadeInSeconds = 1.2f;

    [Header("Tempo ramp")]
    [Tooltip("Off for menu music, on for the shift. When off, the track just plays at " +
             "Base Pitch and ignores the day entirely.")]
    [SerializeField] private bool followDayTempo = true;

    [Tooltip("Playback speed on day 1.")]
    [SerializeField] private float basePitch = 1f;

    [Tooltip("Added to the speed for every day survived.")]
    [SerializeField] private float pitchPerDay = 0.05f;

    [Tooltip("Ceiling. Much past 1.6 and it stops sounding like music.")]
    [SerializeField] private float maxPitch = 1.55f;

    [Tooltip("How fast it eases to a new tempo. Low values make day changes feel like the " +
             "track winding up rather than jumping.")]
    [SerializeField] private float rampSpeed = 0.25f;

    [Header("Game over")]
    [Tooltip("The track sags to this when the run ends.")]
    [SerializeField] private float gameOverPitch = 0.55f;

    public static MusicPlayer Instance { get; private set; }

    public bool Persistent => persistAcrossScenes;

    private AudioSource source;
    private float targetVolume;
    private float fadeRate;

    private void Awake()
    {
        if (persistAcrossScenes)
        {
            // The second scene's copy stands down rather than starting its own track over
            // the top. Disabled as well as destroyed, because Destroy only takes effect at
            // the end of the frame and Start would otherwise still fire on it.
            if (Instance != null && Instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            Instance = this;

            // DontDestroyOnLoad silently refuses to work on a child object.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        source = GetComponent<AudioSource>();

        source.loop = true;
        source.playOnAwake = false;

        if (track != null) source.clip = track;
    }

    private void Start()
    {
        if (source.clip == null)
        {
            Debug.LogWarning($"{name}: no music clip assigned, so nothing will play.", this);
            enabled = false;
            return;
        }

        source.pitch = basePitch;
        source.volume = 0f;
        source.Play();

        FadeTo(volume, fadeInSeconds);
    }

    // Ramps toward a volume over the given time. Call with 0 to fade out.
    public void FadeTo(float target, float seconds)
    {
        targetVolume = Mathf.Clamp01(target);

        float distance = Mathf.Abs((source != null ? source.volume : 0f) - targetVolume);
        fadeRate = distance / Mathf.Max(0.01f, seconds);
    }

    public void FadeOut(float seconds) => FadeTo(0f, seconds);

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (source == null) return;

        // unscaledDeltaTime because GameManager sets Time.timeScale to 0 on game over.
        // Audio ignores timeScale, so without this a fade would sit frozen mid-way.
        source.volume = Mathf.MoveTowards(source.volume, targetVolume, fadeRate * Time.unscaledDeltaTime);
        source.pitch = Mathf.MoveTowards(source.pitch, TargetPitch(), rampSpeed * Time.unscaledDeltaTime);
    }

    private float TargetPitch()
    {
        if (!followDayTempo) return basePitch;

        GameManager game = GameManager.Instance;
        if (game == null) return basePitch;
        if (game.RunOver) return gameOverPitch;

        // Sunday is day 0 and should not run slower than Monday.
        int day = Mathf.Max(1, game.Day);
        return Mathf.Min(maxPitch, basePitch + (pitchPerDay * (day - 1)));
    }
}
