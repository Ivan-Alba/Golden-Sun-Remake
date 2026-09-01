using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuController : MonoBehaviour
{
    public static MenuController Instance { get; private set; }

    [Header("Menu Root View")]
    [SerializeField] private GameObject menuRootPanel;

    [SerializeField] private GameObject playerCanvas;

    private int cursorIndex;
    private bool animating;
    private bool onSubMenu;

    //-------------UI elements----------------//
    private GameObject optionsPanel;
    private GameObject heroPanel;
    private List<GameObject> menuOptions = new List<GameObject>();
    private TextMeshProUGUI goldEarned;
    private TextMeshProUGUI timePlayed;
    private TextMeshProUGUI location;
    private GameObject cursorImage;
    private GameObject botPage;
    private GameObject topPage;

    // Menu options objects
    private GameObject inventory;
    private GameObject equipment;
    private GameObject saveGame;

    private void Awake()
    {
        // Singleton pattern initialization
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Si no se asignó en el Inspector, intenta buscar el panel raíz o usa el transform
        if (menuRootPanel == null && transform.childCount > 0)
        {
            menuRootPanel = transform.GetChild(0).gameObject;
        }

        Transform searchRoot = menuRootPanel != null ? menuRootPanel.transform : transform;

        botPage = searchRoot.Find("Back").Find("Bot").gameObject;
        topPage = searchRoot.Find("Back").Find("Top").gameObject;

        goldEarned = botPage.transform.Find("GoldEarned").GetComponent<TextMeshProUGUI>();
        timePlayed = botPage.transform.Find("TotalTime").GetComponent<TextMeshProUGUI>();
        location = botPage.transform.Find("Location").GetComponent<TextMeshProUGUI>();

        optionsPanel = searchRoot.Find("OptionsPanel").gameObject;
        heroPanel = searchRoot.Find("HeroPanel").gameObject;
        cursorImage = optionsPanel.transform.Find("Cursor").gameObject;

        menuOptions.Clear();
        for (int i = 0; i < optionsPanel.transform.Find("Options").transform.childCount; i++)
        {
            menuOptions.Add(optionsPanel.transform.Find("Options").GetChild(i).gameObject);
        }

        // Submenus
        inventory = searchRoot.Find("Inventory").gameObject;
        equipment = searchRoot.Find("Equipment").gameObject;
        saveGame = searchRoot.Find("Save").gameObject;
    }

    #region Public Visibility & Toggle Methods

    /// <summary>
    /// Opens and initializes the main menu.
    /// </summary>
    public void ShowMenu()
    {
        if (menuRootPanel != null)
        {
            menuRootPanel.SetActive(true);
        }

        ResetMenuState();

        if (GameManager.Instance != null)
        {
            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.InMenu);
            SetHeros();
            RefreshGold();
            RefreshTime();
            RefreshLocation();
        }
    }

    /// <summary>
    /// Safely closes the menu and resets its internal state.
    /// </summary>
    public void CloseMenu()
    {
        ResetMenuState();

        if (menuRootPanel != null)
        {
            menuRootPanel.SetActive(false);
        }

        if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameManager.GameState.InMenu)
        {
            GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
        }

    }

    /// <summary>
    /// Toggles menu visibility based on current state.
    /// </summary>
    public void ToggleMenu()
    {
        bool isCurrentlyActive = (menuRootPanel != null) ? menuRootPanel.activeSelf : false;

        if (isCurrentlyActive)
        {
            CloseMenu();
        }
        else
        {
            ShowMenu();
        }
    }

    /// <summary>
    /// Returns whether the menu UI is currently open.
    /// </summary>
    public bool IsMenuOpen()
    {
        return menuRootPanel != null && menuRootPanel.activeSelf;
    }

    #endregion

    void Update()
    {/*
        // Solamente procesar inputs y lógica si la interfaz gráfica está visible
        if (!IsMenuOpen()) return;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.TotalPlayed += Time.unscaledDeltaTime;
            RefreshTime();
            RefreshGold();
        }

        if (!animating && !onSubMenu)
        {
            // Options Movement Down
            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                menuOptions[cursorIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>()?.PlaySound(GameManager.MOVE_SOUND);
                menuOptions[cursorIndex].GetComponent<TextMeshProUGUI>().color = Color.white;

                cursorIndex = (cursorIndex + 1 >= menuOptions.Count) ? 0 : cursorIndex + 1;

                SetCursorPosition();
                menuOptions[cursorIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
                menuOptions[cursorIndex].transform.Find("Highlight").gameObject.SetActive(true);
            }

            // Options Movement Up
            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                menuOptions[cursorIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>()?.PlaySound(GameManager.MOVE_SOUND);
                menuOptions[cursorIndex].GetComponent<TextMeshProUGUI>().color = Color.white;

                cursorIndex = (cursorIndex - 1 < 0) ? menuOptions.Count - 1 : cursorIndex - 1;

                SetCursorPosition();
                menuOptions[cursorIndex].transform.Find("Highlight").gameObject.SetActive(true);
                menuOptions[cursorIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
            }

            // SELECT AN OPTION FROM MENU
            if (Input.GetKeyDown(GameManager.Instance.KeyConfirm))
            {
                GameManager.Instance.GetComponent<Sounds>()?.PlaySound(GameManager.CONFIRM_SOUND);
                StartCoroutine(OpenSubMenuRoutine());
            }

            // CLOSE MAIN MENU
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(GameManager.Instance.KeyCancel))
            {
                GameManager.Instance.GetComponent<Sounds>()?.PlaySound(GameManager.NEGATIVE_SOUND);
                animating = true;
                menuRootPanel.GetComponent<Animator>()?.SetTrigger("exit");
            }
        }*/
    }

    /// <summary>
    /// Resets all submenus, UI flags, and panel visibilities back to root main menu state.
    /// </summary>
    public void ResetMenuState()
    {
        animating = false;
        onSubMenu = false;
        cursorIndex = 0;

        // Hide all submenus
        if (inventory != null) inventory.SetActive(false);
        if (equipment != null) equipment.SetActive(false);
        if (saveGame != null) saveGame.SetActive(false);

        // Show main panels
        if (heroPanel != null) heroPanel.SetActive(true);
        if (optionsPanel != null) optionsPanel.SetActive(true);

        // Reset title
        if (topPage != null)
        {
            TextMeshProUGUI topText = topPage.GetComponentInChildren<TextMeshProUGUI>();
            if (topText != null) topText.SetText("Menú principal");
        }

        // Reset options highlight colors
        for (int i = 0; i < menuOptions.Count; i++)
        {
            if (menuOptions[i] != null)
            {
                TextMeshProUGUI txt = menuOptions[i].GetComponent<TextMeshProUGUI>();
                if (txt != null) txt.color = (i == 0) ? Color.yellow : Color.white;

                Transform hl = menuOptions[i].transform.Find("Highlight");
                if (hl != null) hl.gameObject.SetActive(i == 0);
            }
        }

        SetCursorPosition();
    }

    private IEnumerator OpenSubMenuRoutine()
    {
        animating = true;
        menuRootPanel.GetComponent<Animator>()?.SetTrigger("enterSub");
        yield return new WaitForSecondsRealtime(0.7f);
        OpenSubmenu(cursorIndex);
    }

    public void BackFromSubmenu()
    {
        SetHeros();
        RefreshGold();
        heroPanel.SetActive(true);
        optionsPanel.SetActive(true);

        // Hide active submenus
        if (inventory != null) inventory.SetActive(false);
        if (equipment != null) equipment.SetActive(false);
        if (saveGame != null) saveGame.SetActive(false);

        menuRootPanel.GetComponent<Animator>()?.SetTrigger("exitSub");
        onSubMenu = false;

        if (topPage != null)
        {
            topPage.GetComponentInChildren<TextMeshProUGUI>()?.SetText("Menú principal");
        }
    }

    private void SetHeros()
    {
        List<GameObject> party = GameManager.Instance.GetParty();
        for (int i = 0; i < party.Count; i++)
        {
            GameObject heroUI = heroPanel.transform.GetChild(i).gameObject;
            HeroStats stats = party[i].GetComponent<HeroStats>();

            if (stats != null)
            {
                heroUI.transform.Find("Image").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/MainMenu/{party[i].name}_avatar");
                heroUI.transform.Find("Name").GetComponent<TextMeshProUGUI>().SetText(stats.Name);
                heroUI.transform.Find("HP").GetComponent<TextMeshProUGUI>().SetText($"{stats.CurrHp}/{stats.MaxHp}");
                heroUI.transform.Find("PP").GetComponent<TextMeshProUGUI>().SetText($"{stats.CurrPp}/{stats.MaxPp}");
                heroUI.transform.Find("Level").GetComponent<TextMeshProUGUI>().SetText($"Nivel: {stats.Level}");
                heroUI.transform.Find("NextLevel").GetComponent<TextMeshProUGUI>().SetText($"Siguiente nivel:\n{stats.NextLevelXp}");

                // Sliders
                Slider hpBar = heroUI.transform.Find("HPBar").GetComponent<Slider>();
                hpBar.maxValue = stats.MaxHp;
                hpBar.value = stats.CurrHp;

                Slider ppBar = heroUI.transform.Find("PPBar").GetComponent<Slider>();
                ppBar.maxValue = stats.MaxPp;
                ppBar.value = stats.CurrPp;

                Slider xpBar = heroUI.transform.Find("XPBar").GetComponent<Slider>();
                xpBar.maxValue = stats.NextLevelXp;
                xpBar.value = stats.CurrentXp;

                heroUI.SetActive(true);
            }
        }
    }

    private void SetCursorPosition()
    {
        if (cursorImage != null && menuOptions.Count > cursorIndex && menuOptions[cursorIndex] != null)
        {
            cursorImage.transform.position = new Vector3(
                cursorImage.transform.position.x,
                menuOptions[cursorIndex].transform.position.y,
                cursorImage.transform.position.z
            );
        }
    }

    private void OpenSubmenu(int index)
    {
        switch (index)
        {
            case 0: // Inventory
                inventory.SetActive(true);
                topPage.GetComponentInChildren<TextMeshProUGUI>()?.SetText("Inventario");
                break;
            case 2: // Equipment
                equipment.SetActive(true);
                topPage.GetComponentInChildren<TextMeshProUGUI>()?.SetText("Equipamiento");
                break;
            case 7: // SaveGame
                saveGame.SetActive(true);
                topPage.GetComponentInChildren<TextMeshProUGUI>()?.SetText("Guardar");
                break;
        }

        heroPanel.SetActive(false);
        optionsPanel.SetActive(false);
        onSubMenu = true;
    }

    private void RefreshLocation()
    {
        if (location != null)
        {
            location.SetText($"Localización: <color=#FFEB04>{SceneManager.GetActiveScene().name}");
        }
    }

    private void RefreshGold()
    {
        if (goldEarned != null && GameManager.Instance != null)
        {
            goldEarned.SetText($"Oro: <color=#FFEB04>{GameManager.Instance.TotalGold} monedas");
        }
    }

    private void RefreshTime()
    {
        if (timePlayed != null && GameManager.Instance != null)
        {
            TimeSpan time = TimeSpan.FromSeconds(GameManager.Instance.TotalPlayed);
            timePlayed.SetText($"Tiempo de juego: <color=#FFEB04>{time.ToString("hh':'mm':'ss")}");
        }
    }

    public void ExitMenu()
    {
        CloseMenu();
        GameEvents.GameState.RequestGameStateChange(GameManager.GameState.WorldExploration);
    }

    public void FinishAnimation()
    {
        animating = false;
    }
}