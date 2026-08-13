using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using TreeEditor;
using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Reflection;

public class SaveLoadUI : MonoBehaviour
{
    [SerializeField] private MenuController menuController;

    int optionsIndex;
    int fileIndex;
    bool animating;
    bool onSave = true;
    bool onLoad;
    bool selectingOption;
    bool selectingFile;

    List<Transform> options = new List<Transform>();
    List<GameObject> fileList = new List<GameObject>();

    GameObject saveFilePrefab;
    GameObject newSavePanelPrefab;
    Transform content;
    Transform contentSave;
    Transform contentLoad;
    Transform savePanel;
    Transform loadPanel;
    Transform scrollBar;

    Dictionary<string, Sprite> heroSprites = new Dictionary<string, Sprite>();

    private void Awake()
    {
        for (int i = 0; i < transform.Find("Options").childCount; i++)
        {
            options.Add(transform.Find("Options").GetChild(i));
        }
        options[0].transform.Find("Highlight").gameObject.SetActive(true);
        options[0].GetComponent<TextMeshProUGUI>().color = Color.yellow;
        scrollBar = transform.Find("SavePanel").Find("FileScrollView").Find("Scrollbar Vertical");
        contentSave = transform.Find("SavePanel").Find("FileScrollView").Find("Viewport").Find("Content");
        contentLoad = transform.Find("LoadPanel").Find("FileScrollView").Find("Viewport").Find("Content");
        saveFilePrefab = Resources.Load<GameObject>("Prefabs/UIComponents/SaveFile");
        newSavePanelPrefab = Resources.Load<GameObject>("Prefabs/UIComponents/NewSavePanel");

        savePanel = transform.Find("SavePanel");
        loadPanel = transform.Find("LoadPanel");

        heroSprites.Add("Isaac", Resources.Load<Sprite>("Images/MainMenu/Isaac_avatar"));
        heroSprites.Add("Garet", Resources.Load<Sprite>("Images/MainMenu/Garet_avatar"));
    }

    private void OnEnable()
    {
        animating = false;
        savePanel.gameObject.SetActive(true);
        loadPanel.gameObject.SetActive(false);
        selectingOption = true;
        optionsIndex = 0;
        fileIndex = 0;
        options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
        options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(true);
        content = contentSave;
        RefreshFiles();
    }

    private void OnDisable()
    {
        animating = false;
        ResetValues();
    }


    private void RefreshFiles()
    {
        //RESET LIST AND SCROLL CONTENT
        for (int i = 0; i < fileList.Count; i++)
        {
            Destroy(fileList[i]);
        }

        fileList = new List<GameObject>();


        //IF ON SAVE: ADD NEWFILEPANEL
        if (onSave)
        {
            GameObject newFile = Instantiate(newSavePanelPrefab, savePanel);
            fileList.Add(newFile);
        }

        //LOAD ALL FILES AND SHOW DETAIL
        for (int i = 0; i < SaveManager.CountFiles(); i++)
        {
            GameObject newFile = Instantiate(saveFilePrefab, content);
            //------FILE DETAIL--------
            GameData gameData = SaveManager.Load(i);
            SaveManager.loadedData = null;
            for (int x = 0; x < gameData.party.Count; x++)
            {
                newFile.transform.Find("Detail").Find($"Hero{x}").gameObject.SetActive(true);
                newFile.transform.Find("Detail").Find($"Hero{x}").Find("Avatar").GetComponent<Image>().sprite = heroSprites[gameData.party[x]];
                newFile.transform.Find("Detail").Find($"Hero{x}").Find("Level").GetComponent<TextMeshProUGUI>().text = $"Nivel {gameData.heroLevelAndXp[x][0]}";
                newFile.transform.Find("ScenePanel").Find("Name").GetComponent<TextMeshProUGUI>().text = gameData.currentSceneName;
                newFile.transform.Find("GoldPanel").Find("Gold").GetComponent<TextMeshProUGUI>().text = $"{gameData.totalGold} monedas";
                TimeSpan time = TimeSpan.FromSeconds(gameData.totalTime);
                newFile.transform.Find("TimePanel").Find("Time").GetComponent<TextMeshProUGUI>().text = time.ToString("hh':'mm':'ss");
            }
            fileList.Add(newFile);
        }
    }

    private void Update()
    {/*

        if (animating)
        {
            return;
        }
        //-----PRESS CONFIRM KEY------
        if (Input.GetKeyDown(GameManager.Instance.KeyConfirm))
        {
            GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);
            if (selectingFile)
            {
                if (onSave)
                {
                    if (fileIndex == 0) //NEW FILE
                    {
                        SaveManager.Save(fileList.Count - 1);
                        RefreshFiles();
                    }
                    else //OVERWRITE A FILE
                    {
                        //ASK SURE?
                        SaveManager.Save(fileIndex - 1);
                        RefreshFiles();
                    }
                    fileList[fileIndex].transform.Find("Highlight").gameObject.SetActive(true);
                }
                else if (onLoad)
                {
                    GameManager.Instance.SetGameState(GameManager.GameState.Loading);
                    GameData gameData = SaveManager.Load(fileIndex);
                    SceneTransitionManager.Instance.LoadSceneWithTransition(gameData.currentSceneName);
                }
            }
            else if (selectingOption)
            {
                //SELECTING OPTION
                selectingOption = false;
                switch (optionsIndex)
                {
                    case 2:
                        GameManager.Instance.SetGameState(GameManager.GameState.Loading);
                        SceneTransitionManager.Instance.LoadSceneWithTransition("Title");
                        break;
                }
                options[optionsIndex].transform.Find("Highlight").GetComponent<Animator>().enabled = false;
                fileList[fileIndex].transform.Find("Highlight").gameObject.SetActive(true);
                selectingFile = true;
            }
        }


        //-----PRESS BACK KEY------
        if (Input.GetKeyDown(GameManager.Instance.KeyCancel))
        {
            GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.NEGATIVE_SOUND);
            if (selectingFile)
            {
                //SELECTING FILE
                selectingFile = false;
                fileList[fileIndex].transform.Find("Highlight").gameObject.SetActive(false);
                options[optionsIndex].transform.Find("Highlight").GetComponent<Animator>().enabled = true;
                fileIndex = 0;
                selectingOption = true;
            }
            else if (selectingOption)
            {
                //BACK TO MAIN MENU
                animating = true;
                StartCoroutine(BackToMainMenu());
            }
        }


        //------PRESS DOWN KEY-------
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if (selectingFile)
            {
                //Move between files
                fileList[fileIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                if (fileIndex + 1 >= fileList.Count)
                {
                    fileIndex = 0;
                    scrollBar.GetComponent<Scrollbar>().value = 1;
                }
                else
                {
                    fileIndex += 1;
                }

                fileList[fileIndex].transform.Find("Highlight").gameObject.SetActive(true);
            }
            else if (selectingOption)
            {
                options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.white;
                if (optionsIndex + 1 >= options.Count)
                {
                    optionsIndex = 0;
                }
                else
                {
                    optionsIndex += 1;
                }

                options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
                options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(true);

                if (optionsIndex == 0) //SAVE OPTION
                {
                    if (loadPanel.gameObject.activeSelf)
                    {
                        content = contentSave;
                        onLoad = false;
                        onSave = true;
                        loadPanel.gameObject.SetActive(false);
                        savePanel.gameObject.SetActive(true);
                        RefreshFiles();
                    }
                }
                else if (optionsIndex == 1) //LOAD OPTION
                {
                    if (savePanel.gameObject.activeSelf)
                    {
                        content = contentLoad;
                        onSave = false;
                        onLoad = true;
                        savePanel.gameObject.SetActive(false);
                        loadPanel.gameObject.SetActive(true);
                        RefreshFiles();
                    }
                }
            }
        }


        //-----PRESS UP KEY-------
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if (selectingFile)
            {
                //Move between files
                fileList[fileIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                if (fileIndex - 1 < 0)
                {
                    fileIndex = fileList.Count - 1;
                    scrollBar.GetComponent<Scrollbar>().value = 0;
                }
                else
                {
                    fileIndex -= 1;
                }

                fileList[fileIndex].transform.Find("Highlight").gameObject.SetActive(true);
            }

            else if (selectingOption)
            {
                options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(false);
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.MOVE_SOUND);
                options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.white;

                if (optionsIndex - 1 < 0)
                {
                    optionsIndex = options.Count - 1;
                }
                else
                {
                    optionsIndex -= 1;
                }

                if (optionsIndex == 0) //SAVE OPTION
                {
                    if (loadPanel.gameObject.activeSelf)
                    {
                        content = contentSave;
                        onLoad = false;
                        onSave = true;
                        loadPanel.gameObject.SetActive(false);
                        savePanel.gameObject.SetActive(true);
                        RefreshFiles();
                    }
                }
                else if (optionsIndex == 1) //LOAD OPTION
                {
                    if (savePanel.gameObject.activeSelf)
                    {
                        content = contentLoad;
                        onSave = false;
                        onLoad = true;
                        savePanel.gameObject.SetActive(false);
                        loadPanel.gameObject.SetActive(true);
                        RefreshFiles();
                    }
                }

                options[optionsIndex].transform.Find("Highlight").gameObject.SetActive(true);
                options[optionsIndex].GetComponent<TextMeshProUGUI>().color = Color.yellow;
            }
        }*/
    }

    private IEnumerator BackToMainMenu()
    {
        gameObject.GetComponent<Animator>().SetTrigger("exit");

        yield return new WaitForSecondsRealtime(0.7f);

        foreach (Transform option in options)
        {
            option.Find("Highlight").gameObject.SetActive(false);
            option.GetComponent<TextMeshProUGUI>().color = Color.white;
        }
        gameObject.SetActive(false);
        menuController.BackFromSubmenu();
    }

    private void ResetValues()
    {
        optionsIndex = 0;
        selectingFile = false;
        fileIndex = 0;
        selectingOption = false;
        optionsIndex = 0;
        fileIndex = 0;
        for (int i = 0; i < options.Count; i++)
        {
            options[i].GetComponent<TextMeshProUGUI>().color = Color.white;
            options[i].transform.Find("Highlight").gameObject.SetActive(false);
        }
    }

}
