using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private AudioClip track;
    [SerializeField, Range(0f, 1f)] private float volume = 0.45f;

    [Header("Tempo ramp")]
    [Tooltip("Playback speed on day 1.")]
    [SerializeField] private float basePitch = 1f;

    [Tooltip("Added to the speed for every day survived.")]
    [SerializeField] private float pitchPerDay = 0.05f;

    [Tooltip("Ceiling. Much past 1.6 and it stops sounding like music.")]
    [SerializeField] private float maxPitch = 1.55f;

    [Tooltip("How fast it eases to a new tempo. Low values make day changes feel " +
             "like the track winding up rather than jumping.")]
    [SerializeField] private float rampSpeed = 0.25f;

    [Header("Game over")]
    [Tooltip("The track sags to this when the run ends.")]
    [SerializeField] private float gameOverPitch = 0.55f;

    private AudioSource source;

    private void Awake()
    {
        source = GetComponent<AudioSource>();

        source.loop = true;
        source.playOnAwake = false;
        source.volume = volume;

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
        source.Play();
    }

    private void Update()
    {
        // unscaledDeltaTime because GameManager sets Time.timeScale to 0 on game over.
        // Audio ignores timeScale, so without this the track would keep playing at full
        // speed while the ramp toward the game-over pitch sat frozen.
        source.pitch = Mathf.MoveTowards(source.pitch, TargetPitch(), rampSpeed * Time.unscaledDeltaTime);
    }

    private float TargetPitch()
    {
        GameManager game = GameManager.Instance;
        if (game == null) return basePitch;
        if (game.RunOver) return gameOverPitch;

        // Sunday is day 0 and should not run slower than Monday.
        int day = Mathf.Max(1, game.Day);
        return Mathf.Min(maxPitch, basePitch + (pitchPerDay * (day - 1)));
    }
}
