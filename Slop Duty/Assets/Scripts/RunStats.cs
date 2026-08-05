[System.Serializable]
public class RunStats
{
    public int served;
    public int wrongColor;
    public int walkedOut;
    public int daysCleared;

    public int Total => served + wrongColor + walkedOut;
    public float Accuracy => Total == 0 ? 0f : served / (float)Total;

    public void Reset()
    {
        served = 0;
        wrongColor = 0;
        walkedOut = 0;
        daysCleared = 0;
    }
}
