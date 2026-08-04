using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelTimer : MonoBehaviour
{
    public float timeRemaining = 60f;
    public float positiveTimeScale = 1f;
    public float negativeTimeScale = 1f;

    private void Update()
    {
        if (timeRemaining > 0)
        {
            timeRemaining -= Time.deltaTime;
        }
        else
        {
            timeRemaining = 0;
            print("Game Ober");
            // Handle Game Over / Time Out logic here
        }
    }

    public void AddTime(float timeToAdd)
    {
        timeRemaining += timeToAdd * positiveTimeScale;
    }

    public void SubtractTime(float timeToSubtract)
    {
        timeRemaining -= timeToSubtract * negativeTimeScale;
    }

    // Optional getters/setters for progression scaling
    public void SetTimeScales(float posScale, float negScale)
    {
        positiveTimeScale = posScale;
        negativeTimeScale = negScale;
    }
}