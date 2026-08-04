using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro ;
using UnityEngine.UI ;
using UnityEngine.SceneManagement ;

public class UIManager : MonoBehaviour
{
    // Start is called before the first frame update
    public Button playButton, tutorialButton ;
    public bool tutorialMode;
    public TMP_Text timeRemaining ;
    public GameObject timer ;
    void Start()
    {
        playButton.onClick.AddListener(startGame) ;
        tutorialButton.onClick.AddListener(startGameTut);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void startGame()
    {
        tutorialMode = false ;
        SceneManager.LoadScene("SlopDuty") ;
    }
    public void startGameTut()
    {
        tutorialMode = true;
        SceneManager.LoadScene("SlopDuty");
    }
    public void showTime()
    {
        timeRemaining.text = "Time: " + Mathf.FloorToInt(timer.gameObject.GetComponent<LevelTimer>().TimeRemaining) ;
    }
}
