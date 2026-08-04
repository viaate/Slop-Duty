using System;
using UnityEngine;

public class LevelTimer : MonoBehaviour
{
    [SerializeField] private float startTime = 60f;

    [Tooltip("Banked time is capped here. Without a cap a good player stockpiles minutes " +
             "during the easy days and the late-game bleed can never catch them, which " +
             "makes the run unlosable.")]
    [SerializeField] private float maxTime = 90f;
    public GameObject ui ;

    public float positiveTimeScale = 1f;
    public float negativeTimeScale = 1f;

    // Fires exactly once, when the clock reaches zero.
    public event Action Expired;

    public float TimeRemaining { get; private set; }
    public float MaxTime => maxTime;
    public bool Running { get; private set; }

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
        // through negativeTimeScale, which is only meant to scale explicit penalties.
        Drain(Time.deltaTime);
    }

    private void Drain(float seconds)
    {
        TimeRemaining -= seconds;
        ui.gameObject.GetComponent<UIManager>().showTime() ;
        if (TimeRemaining > 0f) return;

        TimeRemaining = 0f;
        Running = false;
        Expired?.Invoke();
    }
}
