using System;

/// <summary>
/// Contenedor de evento de input que permite el consumo (Handled) 
/// para evitar propagación en cascada en el mismo frame.
/// </summary>
public class InputEventArgs : EventArgs
{
    /// <summary>
    /// Indica si el input ya ha sido procesado y consumido por un controlador.
    /// </summary>
    public bool Handled { get; set; } = false;

    /// <summary>
    /// Marca el evento como consumido para detener su procesamiento posterior.
    /// </summary>

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Use()
    {
        Handled = true;
    }
}