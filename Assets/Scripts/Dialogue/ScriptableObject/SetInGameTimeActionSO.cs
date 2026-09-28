using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Action_SetInGameTime", menuName = "GoldenSun/Dialogue/Actions/Set InGame Time")]
public class SetInGameTimeActionSO : DialogueActionSO
{
    [Header("Time Configuration")]
    [Tooltip("Target hour in 24-hour format (e.g. 6.0 = Morning, 12.0 = Noon, 18.0 = Sunset, 22.0 = Night)")]
    [Range(0f, 24f)]
    [SerializeField] private float targetInGameTimeHours = 12.0f;

    [Tooltip("If true, the dialogue will pause until the time transition sequence completes.")]
    [SerializeField] private bool waitForTransition = false;

    public override bool RequiresWaiting => waitForTransition;

    public override void ExecuteAction(Action onComplete)
    {
        // Si requiere espera (ej. para cutscenes de posadas con fundido a negro), 
        // escuchamos la finalización de la transición antes de invocar onComplete
        if (waitForTransition)
        {
            Action timeChangeFinishedListener = null;
            timeChangeFinishedListener = () =>
            {
                GameEvents.Environment.OnTimeTransitionCompleted -= timeChangeFinishedListener;
                onComplete?.Invoke();
            };

            GameEvents.Environment.OnTimeTransitionCompleted += timeChangeFinishedListener;
        }

        GameEvents.Environment.RequestSetInGameTime(targetInGameTimeHours);

        if (!waitForTransition)
        {
            onComplete?.Invoke();
        }
    }

    public override void ExecuteAction()
    {
        GameEvents.Environment.RequestSetInGameTime(targetInGameTimeHours);
    }
}