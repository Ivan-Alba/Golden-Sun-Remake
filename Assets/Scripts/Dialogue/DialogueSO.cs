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

    [Tooltip("Flag requerida para que esta respuesta aparezca en la lista (opcional).")]
    public string requiredFlag;

    [Tooltip("Flag que se activa globalmente si el jugador elige esta opción.")]
    public string setFlagOnSelect;
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

    [Tooltip("Flag que se activa automáticamente al reproducir esta frase (ej: 'TALKED_TO_GUARD_FIRST_TIME').")]
    public string setFlagOnComplete;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "Dialogues/Dialogue Node")]
public class DialogueSO : ScriptableObject
{
    public string dialogueId;

    [Tooltip("Si está activo, al terminar el diálogo se guardará automáticamente la flag 'SPOKEN_TO_<dialogueId>'.")]
    public bool markAsReadOnEnd = true;

    public List<DialogueNode> nodes = new List<DialogueNode>();
}