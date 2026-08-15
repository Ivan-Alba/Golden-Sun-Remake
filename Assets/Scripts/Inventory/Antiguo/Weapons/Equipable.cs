using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[JsonObject(MemberSerialization = MemberSerialization.Fields)]
public class Equipable : Item
{
    string[] statName;
    int[] statValue;
    int criticalRate = 1;
    int basePower = 10;
    int elementalPower = 0;

    [JsonConverter(typeof(StringEnumConverter))]
    Psynergy.Elements element = Psynergy.Elements.NoElemental;


    public string[] StatName { get => statName; set => statName = value; }
    public int[] StatValue { get => statValue; set => statValue = value; }
    public int CriticalRate { get => criticalRate; set => criticalRate = value; }
    public int BasePower { get => basePower; set => basePower = value; }
    public int ElementalPower { get => elementalPower; set => elementalPower = value; }
    public Psynergy.Elements Element { get => element; set => element = value; }
}
