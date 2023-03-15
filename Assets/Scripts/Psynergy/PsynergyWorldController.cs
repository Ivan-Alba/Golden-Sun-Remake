using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PsynergyWorldController : MonoBehaviour
{
    public static PsynergyWorldController Instance { get; set; }

    public Dictionary<string, Psynergy[]> heroShortcuts;

    //UI
    GameObject psynergyPanel;
    Transform psynergyChoosePanel;
    int chooseIndex;

    public Dictionary<string, Psynergy[]> HeroShortcuts { get => heroShortcuts; set => heroShortcuts = value; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            psynergyPanel = GameObject.Find("PsynergyPanel");
            psynergyChoosePanel = psynergyPanel.transform.Find("PsynergyChoosePanel");
            heroShortcuts = new Dictionary<string, Psynergy[]>();
        }
    }

    private void Start()
    {
        Initialize();
    }

    public void Initialize()
    {
        
        for (int i = 0; i < GameManager.instance.GetParty().Count; i++)
        {
            Psynergy[] newPsynergyLayer = new Psynergy[4];
            heroShortcuts.Add(GameManager.instance.GetParty()[i].name, newPsynergyLayer);
        }
        ResfreshShortcutsUI(GameManager.instance.ActiveHero);
    }

    public void SetShortcut(GameObject hero, Psynergy psynergy, int position)
    {
        if (HeroShortcuts.ContainsKey(hero.name))
        {
            //Recuperamos psinergías ya asignadas

            for (int i = 0; i < 4; i++)
            {
                Psynergy currentShortcut = (Psynergy)HeroShortcuts[hero.name].GetValue(i);
                if (i == position)
                {
                    //Cambiamos psinergía posición
                    currentShortcut = psynergy;
                }
                Debug.Log(hero.name);
                //Introducimos el array en el dictionary de heroes con la psinergía cambiada
                HeroShortcuts[hero.name].SetValue(currentShortcut, i);

            }
        }

        ResfreshShortcutsUI(hero);
    }

    public void ChoosePanelMoveUp()
    {

    }

    public void ShowPsynergyChoosePanel(bool active, int position)
    {
        List<Psynergy> allPsynergies = GameManager.instance.ActiveHero.GetComponent<FighterStats>().Psynergies;
        List<Psynergy> worldPsynergies = new List<Psynergy>();

        for (int i = 0; i < allPsynergies.Count; i++)
        {
            if (allPsynergies[i].IsWorldPsynergy)
            {
                worldPsynergies.Add(allPsynergies[i]);
            }
        }

        if (active)
        {
            if (worldPsynergies.Count >= 3)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (i == 2)
                    {
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[worldPsynergies.Count - 1].SlugName}");
                    }
                    else
                    {
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[i].SlugName}");
                    }
                    psynergyChoosePanel.Find("Mask").Find($"Icon{i}").gameObject.SetActive(true);
                }

            }
            else if (worldPsynergies.Count == 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (i != 2)
                    {
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[i].SlugName}");
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").gameObject.SetActive(true);
                    }

                }
            }
            else if (worldPsynergies.Count == 1)
            {
                psynergyChoosePanel.Find("Mask").Find("Icon0").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[0].SlugName}");
                psynergyChoosePanel.Find("Mask").Find("Icon0").gameObject.SetActive(true);
            }

            psynergyChoosePanel.gameObject.SetActive(true);
        }
        else
        {
            SetShortcut(GameManager.instance.ActiveHero, worldPsynergies[chooseIndex], position);
            psynergyChoosePanel.gameObject.SetActive(false);
        }
    }

    public void ResfreshShortcutsUI(GameObject hero)
    {


        for (int i = 0; i < 4; i++)
        {
            GameObject iconObject = psynergyPanel.transform.Find($"Icon{i}").gameObject;

            if (heroShortcuts[hero.name] != null)
            {
                Psynergy currentPsynergy = (Psynergy)HeroShortcuts[hero.name].GetValue(i);

                if (currentPsynergy != null)
                {
                    iconObject.GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{currentPsynergy.SlugName}");
                    iconObject.SetActive(true);
                }
                else
                {
                    iconObject.SetActive(false);
                }
            }
            else
            {
                iconObject.SetActive(false);
            }
        }

    }
}
