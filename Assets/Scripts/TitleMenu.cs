using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenu : MonoBehaviour
{

    public void NewGameButton()
    {
        GameManager.Instance.StartNewGame("Llanura de Tale", "Initial_Spawn");
    }

    public void ContinueButton()
    {
       GameData dataSaved = SaveManager.Load(0);

        //CHARGE SCREEN
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.LoadSceneWithTransition(dataSaved.currentSceneName);
        }
        else
        {
            // Fallback in case SceneTransitionManager is not present
            SceneManager.LoadScene(dataSaved.currentSceneName);
        }
    }
}
