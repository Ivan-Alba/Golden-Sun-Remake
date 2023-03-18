using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Equipable : Item
{
    string[] statName;
    int[] statValue;

    public string[] StatName { get => statName; set => statName = value; }
    public int[] StatValue { get => statValue; set => statValue = value; }
}
