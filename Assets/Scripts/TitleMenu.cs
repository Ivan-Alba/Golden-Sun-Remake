using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenu : MonoBehaviour
{

    public void NewGameButton()
    {
        SceneManager.LoadScene("Llanura de Tale");
    }

    public void ContinueButton()
    {
       GameData dataSaved = SaveManager.Load(0);

        //CHARGE SCREEN

        //CHARGE SCENE
        SceneManager.LoadScene(dataSaved.currentSceneName); 
    }
}
