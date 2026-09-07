using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Debug utility to toggle game speed quickly for testing purposes using the new Input System.
/// </summary>
public class GameSpeedDebugController : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField, Tooltip("Tecla del teclado físico para alternar la velocidad.")]
    private Key toggleKey = Key.F1;

    [SerializeField, Tooltip("Velocidad acelerada del juego al activar el debug.")]
    private float fastSpeed = 4.0f;

    [SerializeField, Tooltip("Velocidad normal del juego.")]
    private float normalSpeed = 1.0f;

    private bool isFastForwardActive = false;

    private void Update()
    {
        // Comprobación segura usando el nuevo Input System (Keyboard.current)
        if (Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            isFastForwardActive = !isFastForwardActive;
            float targetSpeed = isFastForwardActive ? fastSpeed : normalSpeed;

            // Disparamos el evento tal como lo solicita tu arquitectura
            GameEvents.GameState.RequestChangeGameSpeed(targetSpeed);

            Debug.Log($"<color=cyan>[GameSpeedDebugController] Velocidad de juego cambiada a: {targetSpeed}x (FastForward: {isFastForwardActive})</color>");
        }
    }
}