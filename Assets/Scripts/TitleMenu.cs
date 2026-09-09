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
        GameEvents.UI.RequestOpenSaveLoadBook(GameEvents.UI.SaveBookMode.Load);
    }
}
