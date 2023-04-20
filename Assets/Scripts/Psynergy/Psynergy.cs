using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using UnityEngine;
using System;

[Serializable]
public class Psynergy
{

    string name, description, slugName;
    int ppCost, targetSize;
    bool isWorldPsynergy, isBattlePsynergy;
    Elements element;
    Types type;
    TargetTypes targetType;
    [NonSerialized]
    Sprite icon;
    [NonSerialized]
    Sprite elementIcon;

   


    public string Name { get => name; set => name = value; }
    public string Description { get => description; set => description = value; }
    public string SlugName { get => slugName; set => slugName = value; }
    public int PpCost { get => ppCost; set => ppCost = value; }
    public bool IsWorldPsynergy { get => isWorldPsynergy; set => isWorldPsynergy = value; }
    public bool IsBattlePsynergy { get => isBattlePsynergy; set => isBattlePsynergy = value; }
    public Sprite Icon { get => icon; set => icon = value; }
    public Sprite ElementIcon { get => elementIcon; set => elementIcon = value; }


    [JsonConverter(typeof(StringEnumConverter))]
    public Types Type { get => type; set => type = value; }
    public Elements Element { get => element; set => element = value; }
    public TargetTypes TargetType { get => targetType; set => targetType = value; }
    public int TargetSize { get => targetSize; set => targetSize = value; }

    public enum Elements
    {
        Venus,
        Mars,
        Jupiter,
        Mercury,
        NoElemental
    }

    public enum Types
    {
        ATTACK,
        SUPPORT,
        HEALING,
        WORLD
    }

    public enum TargetTypes
    {
        USER_TARGET,
        ONE_TARGET,
        MULTI_TARGET,
        ALL,
        NO_TARGET
    }

    //JSON CONSTRUCTOR
    public Psynergy()
    {

    }

    public Psynergy(string name, string description, string slugName, int ppCost, bool isWorldPsynergy,
        bool isBattlePsynergy, Types type, Elements elementType, TargetTypes targetType, int targetSize = 0)
    {
        this.name = name;
        this.description = description;
        this.slugName = slugName;
        this.ppCost = ppCost;
        this.isWorldPsynergy = isWorldPsynergy;
        this.isBattlePsynergy = isBattlePsynergy;
        this.type = type;
        this.element = elementType;
        this.targetType = targetType;
        this.targetSize = targetSize;
    }

    public void SetElementIcon()
    {
        elementIcon = Resources.Load<Sprite>($"Images/Icons/{this.element}_Star");
    }

    public override string ToString()
    {
        return $"{name}: {description}. PP: {ppCost}, {type.ToString()}, {element}, {targetType}";
    }

    
}


