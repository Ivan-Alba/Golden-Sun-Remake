using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    int cursorIndex;
    bool animating;
    bool onSubMenu;


    //-------------UI elements----------------//
    GameObject optionsPanel;
    GameObject heroPanel;
    List<GameObject> menuOptions = new List<GameObject>();
    TextMeshProUGUI goldEarned;
    TextMeshProUGUI timePlayed;
    TextMeshProUGUI location;
    GameObject cursorImage;
    GameObject botPage;
    GameObject topPage;

    //Minimap
    GameObject minimap;

    //Menu options objects
    GameObject inventory;
    GameObject equipment;
    GameObject saveGame;

    PlayerController playerController;
    void Awake()
    {
        minimap = GameObject.FindGameObjectWithTag("Minimap");
       
        botPage = transform.Find("Back").Find("Bot").gameObject;
        topPage = transform.Find("Back").Find("Top").gameObject;

        goldEarned = botPage.transform.Find("GoldEarned").GetComponent<TextMeshProUGUI>();
        timePlayed = botPage.transform.Find("TotalTime").GetComponent<TextMeshProUGUI>();
        location = botPage.transform.Find("Location").GetComponent<TextMeshProUGUI>();

        optionsPanel = transform.Find("OptionsPanel").gameObject;
        heroPanel = transform.Find("HeroPanel").gameObject;
        cursorImage = optionsPanel.transform.Find("Cursor").gameObject;
        for (int i = 0; i < optionsPanel.transform.Find("Options").transform.childCount; i++)
        {
            menuOptions.Add(optionsPanel.transform.Find("Options").GetChild(i).gameObject);
        }

        //Menu options
        inventory = transform.Find("Inventory").gameObject;
        equipment = transform.Find("Equipment").gameObject;
        saveGame = transform.Find("Save").gameObject;

        //Player controller
        playerController = GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerController>();
    }

    private void RefreshLocation()
    {
        location.SetText($"Localización: <color=#FFEB04>{SceneManager.GetActiveScene().name}");
    }

    private void RefreshGold()
    {
        goldEarned.SetText($"Oro: <color=#FFEB04>{GameManager.Instance.TotalGold} monedas");

    }

    private void RefreshTime()
    {
        //TimeSpan time = TimeSpan.FromSeconds(GameManager.instance.TotalPlayed);
        TimeSpan time = TimeSpan.FromSeconds(GameManager.Instance.TotalPlayed);
        timePlayed.SetText($"Tiempo de juego: <color=#FFEB04>{time.ToString("hh':'mm':'ss")}");
    }

    private void OnEnable()
    {
        animating = true;
        cursorIndex = 0;
        menuOptions[0].GetComponent<TextMeshProUGUI>().color = Color.yellow;
        menuOptions[0].transform.Find("Highlight").gameObject.SetActive(true);
        SetCursorPosition();


        if (GameManager.Instance != null)
        {
            SetHeros();
            RefreshGold();
            RefreshTime();
            RefreshLocation();
            Time.timeScale = 0;
        }
    }

    private void OnDisable()
    {
        foreach (GameObject option in menuOptions)
        {
            option.GetComponent<TextMeshProUGUI>().color = Color.white;
            option.transform.Find("Highlight").gameObject.SetActive(false);
        }
        Time.timeScale = 1;
        minimap.transform.parent.gameObject.SetActive(true);
        playerController.StopPlayerController = false;
        
    }

    // Update is called once per frame
    void Update()
    {
        GameManager.Instance.TotalPlayed += Time.unscaledDeltaTime;
        RefreshTime();
        RefreshGold();



        if (!animating && !onSubMenu)
        {
            //Options Movement
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                menuOptions[cursorIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                menuOptions[cursorIndex].GetComponent<TextMeshProUGUI>().color = Color.white;
                if (cursorIndex + 1 >= menuOptions.Count)
                {
                    cursorIndex = 0;
                }
                else
                {
                    cursorIndex += 1;
                }

                SetCursorPosition();

                menuOptions[cursorIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
                menuOptions[cursorIndex].transform.Find("Highlight").gameObject.SetActive(true);

            }

            //Options Movement
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                menuOptions[cursorIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                menuOptions[cursorIndex].GetComponent<TextMeshProUGUI>().color = Color.white;
                if (cursorIndex - 1 < 0)
                {
                    cursorIndex = menuOptions.Count - 1;
                }
                else
                {
                    cursorIndex -= 1;
                }

                SetCursorPosition();
                menuOptions[cursorIndex].transform.Find("Highlight").gameObject.SetActive(true);
                menuOptions[cursorIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
            }

            //SELECT AN OPTION FROM MENU
            if (Input.GetKeyDown(GameManager.Instance.KeyConfirm))
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);
                StartCoroutine(OpenSubMenu());
                Debug.Log($"Opcion seleccionada: {menuOptions[cursorIndex].GetComponent<TextMeshProUGUI>().text}");
            }

            //CLOSE MAIN MENU
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
                animating = true;
                gameObject.GetComponent<Animator>().SetTrigger("exit");

            }

            if (Input.GetKeyDown(GameManager.Instance.KeyCancel))
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
                animating = true;
                gameObject.GetComponent<Animator>().SetTrigger("exit");

            }
        }
    }

    private IEnumerator OpenSubMenu()
    {
        animating = true;
        gameObject.GetComponent<Animator>().SetTrigger("enterSub");
        yield return new WaitForSecondsRealtime(0.7f);
        OpenSubmenu(cursorIndex);
    }

    public void BackFromSubmenu()
    {
        SetHeros();
        RefreshGold();
        heroPanel.SetActive(true);
        optionsPanel.SetActive(true);
        gameObject.GetComponent<Animator>().SetTrigger("exitSub");
        onSubMenu = false;
        topPage.GetComponentInChildren<TextMeshProUGUI>().SetText("Menú principal");
    }

    private void SetHeros()
    {
        List<GameObject> heroItem = new List<GameObject>();


        for (int i = 0; i < GameManager.Instance.GetParty().Count; i++)
        {
            heroItem.Add(heroPanel.transform.GetChild(i).gameObject);
            //Set hero info
            heroItem[i].transform.Find("Image").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/MainMenu/{GameManager.Instance.GetParty()[i].name}_avatar");
            heroItem[i].transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText(GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().Name);
            heroItem[i].transform.Find("HP").GetComponent<TextMeshProUGUI>().SetText
                ($"{GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().CurrHp}/{GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().MaxHp}");
            heroItem[i].transform.Find("PP").GetComponent<TextMeshProUGUI>().SetText
                              ($"{GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().CurrPp}/{GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().MaxPp}");
            heroItem[i].transform.Find("Level").GetComponent<TextMeshProUGUI>().SetText($"Nivel: {GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().Level}");
            heroItem[i].transform.Find("NextLevel").GetComponent<TextMeshProUGUI>().SetText($"Siguiente nivel:\n{GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().NextLevelXp}");


            //Bars
            heroItem[i].transform.Find("HPBar").GetComponent<Slider>().maxValue = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().MaxHp;
            heroItem[i].transform.Find("HPBar").GetComponent<Slider>().value = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().CurrHp;

            heroItem[i].transform.Find("PPBar").GetComponent<Slider>().maxValue = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().MaxPp;
            heroItem[i].transform.Find("PPBar").GetComponent<Slider>().value = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().CurrPp;

            heroItem[i].transform.Find("XPBar").GetComponent<Slider>().maxValue = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().NextLevelXp;
            heroItem[i].transform.Find("XPBar").GetComponent<Slider>().value = GameManager.Instance.GetParty()[i].GetComponent<FighterStats>().Xp;

            heroItem[i].SetActive(true);
        }



    }

    private void SetCursorPosition()
    {
        cursorImage.transform.position = new Vector3(cursorImage.transform.position.x,
            menuOptions[cursorIndex].transform.position.y, cursorImage.transform.position.z);
    }

    private void OpenSubmenu(int cursorIndex)
    {
        switch (cursorIndex)
        {
            case 0: //Inventory
                inventory.SetActive(true);
                topPage.GetComponentInChildren<TextMeshProUGUI>().SetText("Inventario");
                break;

            case 2: //Equipment
                equipment.SetActive(true);
                topPage.GetComponentInChildren<TextMeshProUGUI>().SetText("Equipamiento");
                break;
            case 7: //SaveGame
                saveGame.SetActive(true);
                topPage.GetComponentInChildren<TextMeshProUGUI>().SetText("Guardar");
                break;
        }
        heroPanel.SetActive(false);
        optionsPanel.SetActive(false);
        onSubMenu = true;
    }

    public void ExitMenu()
    {
        gameObject.SetActive(false);
    }


    public void FinishAnimation()
    {
        animating = false;
    }
}
