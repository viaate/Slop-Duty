using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    public Button playButton, tutorialButton;
    public bool tutorialMode;
    public TMP_Text timeRemaining;
    public GameObject timer;

    private LevelTimer levelTimer;

    void Start()
    {
        // Null guarded because this same component lives in both scenes. In SlopDuty
        // there are no menu buttons, and the unguarded version threw a
        // NullReferenceException on the first line every time the game scene loaded.
        if (playButton != null) playButton.onClick.AddListener(startGame);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(startGameTut);

        if (timer != null) levelTimer = timer.GetComponent<LevelTimer>();
        if (levelTimer == null) levelTimer = FindFirstObjectByType<LevelTimer>();
    }

    void Update()
    {
        showTime();
    }

    public void startGame()
    {
        tutorialMode = false;
        PlayerPrefs.SetInt(GameManager.TutorialPrefKey, 0);
        SceneManager.LoadScene("SlopDuty");
    }

    public void startGameTut()
    {
        tutorialMode = true;

        // Written to PlayerPrefs because this object is destroyed along with the menu
        // scene, so a plain bool never survived to reach the game.
        PlayerPrefs.SetInt(GameManager.TutorialPrefKey, 1);
        SceneManager.LoadScene("SlopDuty");
    }

    public void showTime()
    {
        if (timeRemaining == null) return;

        GameManager game = GameManager.Instance;

        if (game != null && game.RunOver)
        {
            timeRemaining.text = $"GAME OVER   DAYS {game.Stats.daysCleared}   SERVED {game.Stats.served}";
            return;
        }

        string day = game == null
            ? ""
            : (game.Day <= 0 ? "SUNDAY" : $"DAY {game.Day}");

        string progress = game == null ? "" : $"   {game.ResolvedToday}/{game.QuotaToday}";

        float seconds = levelTimer != null ? levelTimer.TimeRemaining : 0f;
        string line = $"{day}{progress}   TIME {Mathf.CeilToInt(seconds)}";

        if (PukeManager.Instance != null && PukeManager.Instance.AnyBlockingPuddle)
            line += "   CLEAN THE PUKE";

        timeRemaining.text = line;
    }
}
