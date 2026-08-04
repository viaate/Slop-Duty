using System;
using UnityEngine;

public class LevelTimer : MonoBehaviour
{
    [SerializeField] private float startTime = 45f;

    [Tooltip("Banked time is capped here. Without a cap a good player stockpiles minutes " +
             "during the easy days and the late-game bleed can never catch them, which " +
             "makes the run unlosable.")]
    [SerializeField] private float maxTime = 60f;

    public float positiveTimeScale = 1f;
    public float negativeTimeScale = 1f;

    // Fires exactly once, when the clock reaches zero.
    public event Action Expired;

    public float TimeRemaining { get; private set; }
    public float MaxTime => maxTime;
    public bool Running { get; private set; }

    // GameManager pushes these so the difficulty numbers all live in one file
    // instead of being split between code and whatever is typed into the Inspector.
    public void Configure(float start, float max)
    {
        startTime = start;
        maxTime = max;
    }

    public void Begin()
    {
        TimeRemaining = startTime;
        Running = true;
    }

    public void Stop() => Running = false;

    public void AddTime(float timeToAdd)
    {
        if (!Running) return;
        TimeRemaining = Mathf.Min(TimeRemaining + (timeToAdd * positiveTimeScale), maxTime);
    }

    public void SubtractTime(float timeToSubtract)
    {
        if (!Running) return;
        Drain(timeToSubtract * negativeTimeScale);
    }

    public void SetTimeScales(float posScale, float negScale)
    {
        positiveTimeScale = posScale;
        negativeTimeScale = negScale;
    }

    private void Update()
    {
        if (!Running) return;

        // The natural drain is always 1 second per second. It is deliberately not run
        // through negativeTimeScale, which only scales explicit penalties.
        Drain(Time.deltaTime);
    }

    // The UI is no longer pushed from here. UIManager polls in its own Update, which
    // removes a GetComponent call per frame and stops the timer needing to know that
    // any UI exists at all.
    private void Drain(float seconds)
    {
        TimeRemaining -= seconds;
        if (TimeRemaining > 0f) return;

        TimeRemaining = 0f;
        Running = false;
        Expired?.Invoke();
    }
}
