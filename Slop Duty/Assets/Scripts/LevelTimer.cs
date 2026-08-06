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

    // Both of these return the change they actually made, signed, which is not the same
    // as the change they were asked for: the scales stretch it and maxTime can swallow
    // most of a reward outright. The floating "+3" is built from this return value rather
    // than from the request, so it can never claim time the clock did not really get.
    public float AddTime(float timeToAdd)
    {
        if (!Running) return 0f;

        float before = TimeRemaining;
        TimeRemaining = Mathf.Min(TimeRemaining + (timeToAdd * positiveTimeScale), maxTime);

        return TimeRemaining - before;
    }

    public float SubtractTime(float timeToSubtract)
    {
        if (!Running) return 0f;

        float before = TimeRemaining;
        Drain(timeToSubtract * negativeTimeScale);

        return TimeRemaining - before;
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
