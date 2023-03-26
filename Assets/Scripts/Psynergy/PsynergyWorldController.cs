using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PsynergyWorldController : MonoBehaviour
{
    public static PsynergyWorldController Instance { get; set; }

    public Dictionary<GameObject, Psynergy[]> heroShortcuts;

    //UI
    GameObject psynergyPanel;
    Transform psynergyChoosePanel;
    int chooseIndex;

    public Dictionary<GameObject, Psynergy[]> HeroShortcuts { get => heroShortcuts; set => heroShortcuts = value; }

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
            heroShortcuts = new Dictionary<GameObject, Psynergy[]>();
        }
    }

    private void Start()
    {
        if (heroShortcuts.Count == 0)
        {
            Initialize();
        }
    }

    public void Initialize()
    {

        for (int i = 0; i < GameManager.Instance.GetParty().Count; i++)
        {
            Psynergy[] newPsynergyLayer = new Psynergy[4];
            heroShortcuts.Add(GameManager.Instance.GetParty()[i], newPsynergyLayer);
        }
        ResfreshShortcutsUI(GameManager.Instance.ActiveHero);
    }

    public void SetShortcut(GameObject hero, Psynergy psynergy, int position)
    {
        if (HeroShortcuts.ContainsKey(hero))
        {
            //Recuperamos psinergías ya asignadas

            for (int i = 0; i < 4; i++)
            {
                Psynergy currentShortcut = (Psynergy)HeroShortcuts[hero].GetValue(i);
                if (i == position)
                {
                    //Cambiamos psinergía posición
                    currentShortcut = psynergy;
                }
                Debug.Log(hero);
                //Introducimos el array en el dictionary de heroes con la psinergía cambiada
                HeroShortcuts[hero].SetValue(currentShortcut, i);

            }
        }

        ResfreshShortcutsUI(hero);
    }


    public void UsePsynergy(int position, GameObject target)
    {
        Psynergy usedPsynergy = (Psynergy)HeroShortcuts[GameManager.Instance.ActiveHero].GetValue(position);
        if (usedPsynergy != null)
        {
            if (target != null)
            {
                Debug.Log($"Has usado la psinergía: {usedPsynergy.SlugName} en {target.name}");
                //Comprobamos si el tag coincide con la psinergía usada
                if (target.tag == $"{usedPsynergy.SlugName}Reactive")
                {
                    //LLAMAR SCRIPT REACTIVO PARA ACTUAR
                    target.GetComponent<PsynergyReactive>().React();

                }

            } else
            {
                //AUTOTARGET?
            }
        }
    }

    public void ChoosePanelMoveUp()
    {
        chooseIndex--;
        RefreshPsynergyChoosePanel();
    }

    public void ChoosePanelMoveDown()
    {
        chooseIndex++;
        RefreshPsynergyChoosePanel();
    }
    public void RefreshPsynergyChoosePanel()
    {
        List<Psynergy> allPsynergies = GameManager.Instance.ActiveHero.GetComponent<FighterStats>().Psynergies;
        List<Psynergy> worldPsynergies = new List<Psynergy>();

        for (int i = 0; i < allPsynergies.Count; i++)
        {
            if (allPsynergies[i].IsWorldPsynergy)
            {
                worldPsynergies.Add(allPsynergies[i]);
            }
        }

        if (chooseIndex < 0)
        {
            chooseIndex = worldPsynergies.Count - 1;
        }
        else if (chooseIndex >= worldPsynergies.Count)
        {
            chooseIndex = 0;
        }

        if (worldPsynergies.Count >= 3)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i == 2)
                {
                    if (chooseIndex - 1 < 0)
                    {
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[worldPsynergies.Count - 1].SlugName}");
                    }
                    else
                    {
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[chooseIndex - 1].SlugName}");

                    }
                }
                else if (i == 1)
                {
                    if (chooseIndex + 1 == worldPsynergies.Count)
                    {
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[0].SlugName}");

                    }
                    else
                    {
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[chooseIndex + 1].SlugName}");

                    }
                }
                else
                {
                    psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[chooseIndex].SlugName}");
                }
                psynergyChoosePanel.Find("Mask").Find($"Icon{i}").gameObject.SetActive(true);
            }

        }
        else if (worldPsynergies.Count == 2)
        {
            for (int i = 0; i < 3; i++)
            {
                if (i == 0)
                {
                    psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[chooseIndex].SlugName}");
                    psynergyChoosePanel.Find("Mask").Find($"Icon{i}").gameObject.SetActive(true);
                }
                else if (i == 1)
                {
                    if (chooseIndex + 1 >= worldPsynergies.Count)
                    {
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[0].SlugName}");
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").gameObject.SetActive(true);
                    }
                    else
                    {
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[chooseIndex + 1].SlugName}");
                        psynergyChoosePanel.Find("Mask").Find($"Icon{i}").gameObject.SetActive(true);
                    }
                }

            }
        }
        else if (worldPsynergies.Count == 1)
        {
            psynergyChoosePanel.Find("Mask").Find("Icon0").GetComponent<Image>().sprite = Resources.Load<Sprite>($"Images/Psynergy/{worldPsynergies[0].SlugName}");
            psynergyChoosePanel.Find("Mask").Find("Icon0").gameObject.SetActive(true);
        }

    }

    public void ShowPsynergyChoosePanel(bool active, int position)
    {

        List<Psynergy> allPsynergies = GameManager.Instance.ActiveHero.GetComponent<FighterStats>().Psynergies;
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
            if (worldPsynergies.Count > 0)
            {
                SetShortcut(GameManager.Instance.ActiveHero, worldPsynergies[chooseIndex], position);
            }

            psynergyChoosePanel.gameObject.SetActive(false);
            chooseIndex = 0;
        }
    }

    public void ResfreshShortcutsUI(GameObject hero)
    {


        for (int i = 0; i < 4; i++)
        {
            GameObject iconObject = psynergyPanel.transform.Find($"Icon{i}").gameObject;

            if (heroShortcuts[hero] != null)
            {
                Psynergy currentPsynergy = (Psynergy)HeroShortcuts[hero].GetValue(i);

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
