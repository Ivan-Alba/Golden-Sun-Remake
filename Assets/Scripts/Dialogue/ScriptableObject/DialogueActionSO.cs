using System;
using UnityEngine;

/// <summary>
/// Base class for modular dialogue actions. 
/// Allows ScriptableObject-driven events to be triggered from dialogue choices without scene references.
/// Supports both instant and asynchronous/blocking actions.
/// </summary>
public abstract class DialogueActionSO : ScriptableObject
{
    /// <summary>
    /// If true, the DialogueController will pause its flow until the action invokes its completion callback.
    /// </summary>
    public virtual bool RequiresWaiting => false;

    /// <summary>
    /// Executes the action asynchronously or with a completion callback.
    /// </summary>
    public virtual void ExecuteAction(Action onComplete)
    {
        ExecuteAction();
        onComplete?.Invoke();
    }

    /// <summary>
    /// Default synchronous execution fallback.
    /// </summary>
    public abstract void ExecuteAction();
}