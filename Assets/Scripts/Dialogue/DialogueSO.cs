using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public enum DialogueChoiceType
{
    StandardText,   // Menú de opciones de texto libre
    BinaryYesNo     // Cuadro de decisión rápida Sí/No (Iconos estilo Golden Sun)
}

[System.Serializable]
public class DialogueChoice
{
    [Tooltip("Clave traducible para el texto de la opción.")]
    public LocalizedString localizedChoiceText;

    [Tooltip("Rama a la que salta el diálogo al seleccionar esta opción.")]
    public DialogueSO nextDialogue;

    [Header("Flag Conditions & Mutations")]
    [Tooltip("FlagScriptableObject requerida para que esta respuesta aparezca disponible (opcional).")]
    public FlagSO requiredFlag;

    [Tooltip("Slug alternativo de la flag si no se asigna el ScriptableObject directamente.")]
    public string requiredFlagSlug;

    [Tooltip("FlagScriptableObject que se activa globalmente si el jugador elige esta opción.")]
    public FlagSO setFlagOnSelect;

    [Tooltip("Slug alternativo para activar si no se asigna el ScriptableObject directamente.")]
    public string setFlagSlugOnSelect;
}

[System.Serializable]
public class DialogueNode
{
    [Tooltip("Clave traducible para la frase principal.")]
    public LocalizedString localizedText;

    [Tooltip("Retrato alternativo para esta frase en concreto.")]
    public Sprite overridePortrait;

    [Tooltip("Nombre de hablante alternativo si habla otro personaje en el mismo diálogo.")]
    public LocalizedString overrideSpeakerName;

    [Tooltip("Tipo de interfaz a desplegar al llegar a este nodo.")]
    public DialogueChoiceType choiceType = DialogueChoiceType.StandardText;

    [Tooltip("Lista de ramificaciones disponibles para el jugador.")]
    public List<DialogueChoice> choices = new List<DialogueChoice>();

    [Header("Completion Flag")]
    [Tooltip("FlagScriptableObject que se activa automáticamente al reproducir esta frase.")]
    public FlagSO setFlagOnComplete;

    [Tooltip("Slug alternativo de la flag a activar al completar esta frase.")]
    public string setFlagSlugOnComplete;
}

/// <summary>
/// ScriptableObject defining dialogue node trees, localized text lines, choices,
/// and automated story flag mutations upon execution or read completion.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "GoldenSun/Dialogue/Dialogue Asset")]
public class DialogueSO : ScriptableObject
{
    [Header("Basic Dialogue Metadata")]
    public string dialogueId;

    [Tooltip("Si está activo, al terminar el diálogo se activará la bandera de lectura asociada.")]
    public bool markAsReadOnEnd = true;

    [Tooltip("FlagScriptableObject explícita que se marcará como leída al terminar este diálogo.")]
    public FlagSO readFlag;

    [Header("Node Content")]
    public List<DialogueNode> nodes = new List<DialogueNode>();
}