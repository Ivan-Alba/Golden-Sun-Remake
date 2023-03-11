using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDrop : MonoBehaviour
{
    [Header("Enemy Drop")]
    [SerializeField] int xp;
    [SerializeField] int gold;
    [SerializeField] int ph;
    [SerializeField] string itemEasyDrop;
    [SerializeField] float itemEasyRatio;
    [SerializeField] string itemRareDrop;
    [SerializeField] float itemRareRatio;
    [SerializeField] string itemVeryRareDrop;
    [SerializeField] float itemVeryRareRatio;


    public int Xp { get => xp; set => xp = value; }
    public int Gold { get => gold; set => gold = value; }
    public int Ph { get => ph; set => ph = value; }
    public string ItemEasyDrop { get => itemEasyDrop; set => itemEasyDrop = value; }
    public string ItemRareDrop { get => itemRareDrop; set => itemRareDrop = value; }
    public string ItemVeryRareDrop { get => itemVeryRareDrop; set => itemVeryRareDrop = value; }
    public float ItemEasyRatio { get => itemEasyRatio; set => itemEasyRatio = value; }
    public float ItemRareRatio { get => itemRareRatio; set => itemRareRatio = value; }
    public float ItemVeryRareRatio { get => itemVeryRareRatio; set => itemVeryRareRatio = value; }
}
