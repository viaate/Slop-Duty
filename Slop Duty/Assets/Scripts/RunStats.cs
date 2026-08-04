[System.Serializable]
public class RunStats
{
    public int served;
    public int wrongColour;
    public int walkedOut;
    public int daysCleared;

    public int Total => served + wrongColour + walkedOut;
    public float Accuracy => Total == 0 ? 0f : served / (float)Total;

    public void Reset()
    {
        served = 0;
        wrongColour = 0;
        walkedOut = 0;
        daysCleared = 0;
    }
}
