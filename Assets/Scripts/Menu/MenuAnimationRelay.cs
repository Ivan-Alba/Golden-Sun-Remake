#if false
using UnityEngine;

public class MenuAnimationRelay : MonoBehaviour
{
    // Método para llamar desde el Animation Event al terminar las animaciones de entrada/salida de submenús
    public void FinishAnimation()
    {
        if (MenuController.Instance != null)
        {
            MenuController.Instance.FinishAnimation();
        }
    }

    // Método para llamar desde el Animation Event al terminar la animación de salida total del menú
    public void ExitMenu()
    {
        if (MenuController.Instance != null)
        {
            MenuController.Instance.ExitMenu();
        }
    }
}
#endif