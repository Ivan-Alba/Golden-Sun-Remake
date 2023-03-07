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
    [SerializeField] string itemRareDrop;
    [SerializeField] string itemVeryRareDrop;

    public int Xp { get => xp; set => xp = value; }
    public int Gold { get => gold; set => gold = value; }
    public int Ph { get => ph; set => ph = value; }
}
