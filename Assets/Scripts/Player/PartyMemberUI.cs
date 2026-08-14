using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyMemberUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image avatarImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI ppText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private Slider ppSlider;

    // Updates the visual elements of a single party member card
    public void UpdateData(HeroStats stats, string heroName)
    {
        if (stats == null) return;

        if (avatarImage != null)
        {
            avatarImage.sprite = Resources.Load<Sprite>($"Images/MainMenu/{heroName}_avatar");
        }

        if (nameText != null) nameText.SetText(stats.Name);
        if (hpText != null) hpText.SetText($"{stats.CurrHp}/{stats.MaxHp}");
        if (ppText != null) ppText.SetText($"{stats.CurrPp}/{stats.MaxPp}");
        if (levelText != null) levelText.SetText($"Nivel: {stats.Level}");

        if (hpSlider != null)
        {
            hpSlider.maxValue = stats.MaxHp;
            hpSlider.value = stats.CurrHp;
        }

        if (ppSlider != null)
        {
            ppSlider.maxValue = stats.MaxPp;
            ppSlider.value = stats.CurrPp;
        }

        gameObject.SetActive(true);
    }
}