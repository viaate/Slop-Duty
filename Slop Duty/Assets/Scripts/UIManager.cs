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
    public bool tutorialMode ;
    void Start()
    {
        playButton.onClick.AddListener(startGame(false)) ;
        tutorialButton.onClick.AddListener(startGame(true)) ;
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void startGame(bool isTutorial)
    {
        tutorialMode = isTutorial ;
        SceneManager.LoadScene("SlopDuty") ;
    }
}
