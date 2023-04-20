using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Xml;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultsScreenController : MonoBehaviour
{
    TextMeshProUGUI xpText, phText, goldText, heroName, heroLevel, heroNextLevel, confirmText, abilityLearnedName;
    GameObject itemsPanel;
    Slider xpBar;
    Sounds resultSounds;
    List<GameObject> heroList = new List<GameObject>();
    bool waitingConfirmation;
    bool resultsFinished;

    public bool ResultsFinished { get => resultsFinished; set => resultsFinished = value; }

    private void Awake()
    {
        xpText = transform.Find("InfoPanel").Find("XpPanel").Find("XpText").GetComponent<TextMeshProUGUI>();
        phText = transform.Find("InfoPanel").Find("PhPanel").Find("PhText").GetComponent<TextMeshProUGUI>();
        goldText = transform.Find("InfoPanel").Find("GoldPanel").Find("GoldText").GetComponent<TextMeshProUGUI>();
        itemsPanel = transform.Find("InfoPanel").Find("ItemPanel").gameObject;
        confirmText = transform.Find("PressConfirm").gameObject.GetComponent<TextMeshProUGUI>();
        confirmText.SetText($"Pulsa {GameManager.Instance.KeyConfirm} para continuar");

        resultSounds = gameObject.GetComponent<Sounds>();
    }


    // Update is called once per frame
    void Update()
    {
        if (waitingConfirmation)
        {
            if (Input.GetKeyDown(GameManager.Instance.KeyConfirm))
            {
                GameManager.Instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);
                waitingConfirmation = false;
                ResultsFinished = true;
            }
        }
    }

    private void SetInitConfig()
    {
        //SET INIT CONFIGURATIONS
        gameObject.GetComponent<CanvasGroup>().alpha = 0;
        gameObject.SetActive(true);
        confirmText.gameObject.SetActive(false);
        xpText.SetText("0");
        phText.SetText("0");
        goldText.SetText("0");

        //SET INIT HERO INFO
        for (int i = 0; i < heroList.Count; i++)
        {
            transform.Find("HeroPanel").Find($"Hero{i}").gameObject.SetActive(true);
            transform.Find("HeroPanel").Find($"Hero{i}").Find("Mask").Find("Avatar").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/MainMenu/{heroList[i].name}_avatar");
            heroName = transform.Find("HeroPanel").Find($"Hero{i}").Find("NameText").GetComponent<TextMeshProUGUI>();
            heroLevel = transform.Find("HeroPanel").Find($"Hero{i}").Find("LevelText").GetComponent<TextMeshProUGUI>();
            heroNextLevel = transform.Find("HeroPanel").Find($"Hero{i}").Find("NextLevelText").GetComponent<TextMeshProUGUI>();

            heroName.SetText(heroList[i].GetComponent<FighterStats>().Name);
            heroLevel.SetText($"NV: {heroList[i].GetComponent<FighterStats>().Level}");
            heroNextLevel.SetText((heroList[i].GetComponent<FighterStats>().NextLevelXp
                - heroList[i].GetComponent<FighterStats>().Xp).ToString());

            xpBar = transform.Find("HeroPanel").Find($"Hero{i}").Find("XpBar").GetComponent<Slider>();
            xpBar.value = heroList[i].GetComponent<FighterStats>().Xp;
            xpBar.maxValue = heroList[i].GetComponent<FighterStats>().NextLevelXp;
        }

    }
    public IEnumerator ShowResults(List<GameObject> heroList, int xpEarned, int phEarned, int goldEarned, Dictionary<Item, int> totalItemsObtained)
    {

        this.heroList = heroList;
        SetInitConfig();

        //CROSSFADE
        while (gameObject.GetComponent<CanvasGroup>().alpha < 1)
        {
            gameObject.GetComponent<CanvasGroup>().alpha += 0.2f;
            yield return new WaitForSeconds(0.01f);
        }
        yield return new WaitForSeconds(1f);


        //----------------------RECOMPENSAS DE XP, ITEMS, ETC--------------------------------

        //------------------ITEMS------------------
        int initY = 130;
        int offsetY = 60;
        int iterat = 0;
        foreach (KeyValuePair<Item, int> entry in totalItemsObtained)
        {
            // do something with entry.Value or entry.Key
            Debug.Log($"Enemigo deja caer: {entry.Key.ItemName}");
            GameObject newItem = Instantiate(Resources.Load("Prefabs/UIComponents/ResultScreenItem"), itemsPanel.transform) as GameObject;
            newItem.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, 0, 290);
            newItem.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, 0, initY + (offsetY * iterat));
            newItem.transform.Find("Name").GetComponent<TextMeshProUGUI>().text = entry.Key.ItemName;
            newItem.transform.Find("ElementIcon").gameObject.SetActive(false);
            newItem.transform.Find("Icon").GetComponent<Image>().sprite = entry.Key.Icon;
            newItem.transform.Find("Number").GetComponent<TextMeshProUGUI>().text = entry.Value.ToString();
            for (int i = 0; i < entry.Value; i++)
            {
                InventoryController.Instance.AddItem(entry.Key.ItemSlug);
            }
            iterat++;
        }



        //--ANIMATE NUMBERS AND BAR CONFIGURATION--
        int previousValue = 0;
        int stepAmount;
        int countFPS = 30;
        float duration = 0.4f;
        WaitForSeconds Wait = new WaitForSeconds(duration / countFPS);

        //-------------------XP--------------------
        resultSounds.PlaySound(0);
        stepAmount = Mathf.CeilToInt((xpEarned - previousValue) / (countFPS * duration));

        //XP OBTAINED VALUE ANIMATION
        while (previousValue < xpEarned)
        {
            previousValue += stepAmount;

            if (previousValue > xpEarned)
            {
                stepAmount -= previousValue - xpEarned;
                previousValue = xpEarned;
            }

            //HERO NEXT LEVEL VALUE AND XP BAR
            for (int i = 0; i < heroList.Count; i++)
            {
                if (heroList[i].GetComponent<FighterStats>().CurrHp == 0) //IF HERO IS DEAD DOESNT WIN XP
                {
                    transform.Find("HeroPanel").Find($"Hero{i}").GetComponent<CanvasGroup>().alpha = 0.5f;

                }
                else
                {
                    FighterStats heroStats = heroList[i].GetComponent<FighterStats>();
                    xpBar = transform.Find("HeroPanel").Find($"Hero{i}").Find("XpBar").GetComponent<Slider>();
                    heroNextLevel = transform.Find("HeroPanel").Find($"Hero{i}").Find("NextLevelText").GetComponent<TextMeshProUGUI>();

                    //INCREMENT BAR AND HERO XP
                    xpBar.value += stepAmount;
                    heroStats.Xp += stepAmount;


                    if (heroStats.Xp >= heroStats.NextLevelXp) //LEVEL UP?
                    {
                        int surplus = heroStats.Xp - heroStats.NextLevelXp; //EXCEDENT XP

                        string newPsynergyLearned = heroStats.LevelUp();
                        if (newPsynergyLearned != null)
                        {
                            StartCoroutine(AbilityLearned(i, newPsynergyLearned));
                        }
                        
                        heroStats.Xp += surplus;
                        //REFRESH BAR VALUES
                        xpBar.value = heroStats.Xp;
                        xpBar.maxValue = heroStats.NextLevelXp;
                        //REFRESH HERO LEVEL TEXT
                        heroLevel = transform.Find("HeroPanel").Find($"Hero{i}").Find("LevelText").GetComponent<TextMeshProUGUI>();
                        heroLevel.SetText($"NV: {heroStats.Level}");
                        heroLevel.color = new Color(1, 0.72f, 0.08f);
                    }

                    //NEXT LEVEL DROP XP
                    heroNextLevel.SetText((heroList[i].GetComponent<FighterStats>().NextLevelXp
                                   - heroList[i].GetComponent<FighterStats>().Xp).ToString());
                }

            }
            xpText.SetText(previousValue.ToString());
            yield return Wait;
        }
        resultSounds.StopSound();


        //-----------------PH----------------------
        resultSounds.PlaySound(0);

        previousValue = 0;
        stepAmount = Mathf.CeilToInt((phEarned - previousValue) / (countFPS * duration));

        while (previousValue < phEarned)
        {
            previousValue += stepAmount;
            if (previousValue > phEarned)
            {
                previousValue = phEarned;
            }
            phText.SetText(previousValue.ToString());

            yield return Wait;
        }
        resultSounds.StopSound();


        //-----------------GOLD----------------------
        resultSounds.PlaySound(0);

        previousValue = 0;
        stepAmount = Mathf.CeilToInt((goldEarned - previousValue) / (countFPS * duration));

        while (previousValue < goldEarned)
        {
            previousValue += stepAmount;
            if (previousValue > goldEarned)
            {
                previousValue = goldEarned;
            }
            goldText.SetText(previousValue.ToString());

            yield return Wait;
        }

        resultSounds.StopSound();

        //PUT GOLD ON GAME TOTAL AMOUNT
        GameManager.Instance.TotalGold += goldEarned;

        //SHOW EXIT MESSAGE
        waitingConfirmation = true;
        confirmText.gameObject.SetActive(true);
    }


    private IEnumerator AbilityLearned(int index, string psynergySlugName)
    {
        Transform abilityLearnedPanel = transform.Find("HeroPanel").Find($"Hero{index}").Find("LearnAbility");
        abilityLearnedName = abilityLearnedPanel.Find("AbilityName").GetComponent<TextMeshProUGUI>();

        abilityLearnedPanel.GetComponent<Image>().sprite = PsynergyDatabase.Instance.GetPsynergy(psynergySlugName).Icon;
        abilityLearnedName.text = PsynergyDatabase.Instance.GetPsynergy(psynergySlugName).Name;
        abilityLearnedPanel.gameObject.SetActive(true);
        yield return null;
    }

    public void Reset()
    {
        resultsFinished = false;
        gameObject.SetActive(false);
        for (int i = 0; i < heroList.Count; i++)
        {
            transform.Find("HeroPanel").Find($"Hero{i}").gameObject.SetActive(false);
            transform.Find("HeroPanel").Find($"Hero{i}").GetComponent<CanvasGroup>().alpha = 1;
            transform.Find("HeroPanel").Find($"Hero{i}").Find("LevelText").GetComponent<TextMeshProUGUI>().color = Color.white;
            transform.Find("HeroPanel").Find($"Hero{i}").Find("LearnAbility").gameObject.SetActive(false);
        }
        for (int i = 0; i < itemsPanel.transform.childCount; i++)
        {
            if (itemsPanel.transform.GetChild(i).gameObject.name != "Items")
            {
                Destroy(itemsPanel.transform.GetChild(i).gameObject);
            }
        }
    }
}
