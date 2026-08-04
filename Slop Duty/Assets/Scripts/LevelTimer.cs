using System;
using UnityEngine;

public class LevelTimer : MonoBehaviour
{
    public float timeRemaining = 60f;
    public float positiveTimeScale = 1f;
    public float negativeTimeScale = 1f;

    public bool IsGameOver { get; private set; }
    public event Action OnGameOver;

    private void Update()
    {
        if (IsGameOver) return;

        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
        }
        else
        {
            timeRemaining = 0;
            IsGameOver = true;
            OnGameOver?.Invoke(); // hook a game-over screen / UIManager to this
        }
    }

    public void AddTime(float timeToAdd)
    {
        if (IsGameOver) return;
        timeRemaining += timeToAdd * positiveTimeScale;
    }

    public void SubtractTime(float timeToSubtract)
    {
        if (IsGameOver) return;
        timeRemaining -= timeToSubtract * negativeTimeScale;
    }

    // Optional getters/setters for progression scaling
    public void SetTimeScales(float posScale, float negScale)
    {
        positiveTimeScale = posScale;
        negativeTimeScale = negScale;
    }
}
