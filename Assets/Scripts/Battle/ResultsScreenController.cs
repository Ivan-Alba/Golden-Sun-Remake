using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResultsScreenController : MonoBehaviour
{
    TextMeshProUGUI xpText, phText, goldText, heroName, heroLevel, heroNextLevel, confirmText;
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
        confirmText = transform.Find("PressConfirm").gameObject.GetComponent<TextMeshProUGUI>();
        confirmText.SetText($"Pulsa {GameManager.instance.KeyConfirm} para continuar");

        resultSounds = gameObject.GetComponent<Sounds>();
    }


    // Update is called once per frame
    void Update()
    {
        if (waitingConfirmation)
        {
            if (Input.GetKeyDown(GameManager.instance.KeyConfirm))
            {
                GameManager.instance.GetComponent<Sounds>().PlaySound(GameManager.CONFIRM_SOUND);
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
    public IEnumerator ShowResults(List<GameObject> heroList, int xpEarned, int phEarned, int goldEarned)
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


        //RCOMPENSAS DE XP, ITEMS, ETC


        //-------------------ANIMATE NUMBERS AND BAR CONFIGURATION---------------------------
        int previousValue = 0;
        int stepAmount;
        int countFPS = 30;
        float duration = 0.4f;
        WaitForSeconds Wait = new WaitForSeconds(duration / countFPS);

        //-----------------XP----------------------
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

                } else
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
                        heroStats.LevelUp();
                        heroStats.Xp += surplus;
                        //REFRESH BAR VALUES
                        xpBar.value = heroStats.Xp;
                        xpBar.maxValue = heroStats.NextLevelXp;
                        //REFRESH HERO LEVEL TEXT
                        heroLevel = transform.Find("HeroPanel").Find($"Hero{i}").Find("LevelText").GetComponent<TextMeshProUGUI>();
                        heroLevel.SetText($"NV: {heroStats.Level.ToString()}");
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
        GameManager.instance.TotalGold += goldEarned;

        //SHOW EXIT MESSAGE
        waitingConfirmation = true;
        confirmText.gameObject.SetActive(true);
    }

    public void Reset()
    {
        resultsFinished = false;
        gameObject.SetActive(false);
        for (int i = 0; i < heroList.Count; i++)
        {
            transform.Find("HeroPanel").Find($"Hero{i}").gameObject.SetActive(false);
            transform.Find("HeroPanel").Find($"Hero{i}").GetComponent<CanvasGroup>().alpha = 1;
        }
    }
}
