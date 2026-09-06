using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleMenu : MonoBehaviour
{

    public void NewGameButton()
    {
        GameEvents.Scene.RequestStartNewGame("Llanura de Tale", "initial_spawn");
    }

    public void ContinueButton()
    {
        GameData dataSaved = SaveManager.Load(0);

        if (dataSaved == null)
        {
            Debug.LogWarning("[ContinueButton] No saved data found at slot 0.");
            return;
        }

        // Charge screen via decoupled event bus
        GameEvents.Scene.RequestLoadScene(dataSaved.worldState.currentSceneName);
    }
}
