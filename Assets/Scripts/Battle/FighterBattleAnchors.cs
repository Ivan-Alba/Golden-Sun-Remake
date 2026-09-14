using UnityEngine;

/// <summary>
/// Component attached to Hero and Enemy prefabs to explicitly define 3D spatial anchors 
/// for UI presentation (names, damage numbers, command menus) and in-world visual elements (3D selection pointers).
/// </summary>
public class FighterBattleAnchors : MonoBehaviour
{
    [Header("3D Selection Pointer Anchor")]
    [Tooltip("Target transform where the 3D Pointer is attached/positioned.")]
    [SerializeField] private Transform pointerAnchor;

    [Header("UI Floating Text Offsets")]
    [Tooltip("World space offset from target base used for Damage Numbers.")]
    [SerializeField] private Vector3 damageNumberOffset = new Vector3(0f, 1.8f, 0f);

    [Tooltip("World space offset from target base used for Name & Target Info cards.")]
    [SerializeField] private Vector3 targetInfoOffset = new Vector3(0f, 2.2f, 0f);

    [Header("Hero UI Menu Offsets (Used for active heroes)")]
    [Tooltip("World space offset from target base for spawning the 3D Command Cross menu.")]
    [SerializeField] private Vector3 commandCrossOffset = new Vector3(0f, 1.2f, 0f);

    [Tooltip("World space offset from target base for displaying action selection submenus (Psynergy/Items/Djinn).")]
    [SerializeField] private Vector3 subMenuOffset = new Vector3(0f, 1.5f, 0f);

    #region Public Properties

    public Transform PointerAnchor => pointerAnchor != null ? pointerAnchor : transform;
    public Vector3 DamageNumberOffset => damageNumberOffset;
    public Vector3 TargetInfoOffset => targetInfoOffset;
    public Vector3 CommandCrossOffset => commandCrossOffset;
    public Vector3 SubMenuOffset => subMenuOffset;

    #endregion

    #region Public Helper API

    /// <summary>
    /// Calculates absolute world position for floating damage text.
    /// </summary>
    public Vector3 GetDamageWorldPosition()
    {
        return transform.position + damageNumberOffset;
    }

    /// <summary>
    /// Calculates absolute world position for target info UI cards.
    /// </summary>
    public Vector3 GetTargetInfoWorldPosition()
    {
        return transform.position + targetInfoOffset;
    }

    /// <summary>
    /// Calculates absolute world position for the Hero's Command Cross menu.
    /// </summary>
    public Vector3 GetCommandCrossWorldPosition()
    {
        return transform.position + commandCrossOffset;
    }

    /// <summary>
    /// Calculates absolute world position for Hero submenus (Psynergy, Items, Djinn).
    /// </summary>
    public Vector3 GetSubMenuWorldPosition()
    {
        return transform.position + subMenuOffset;
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        // Color verde para números de daño
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + damageNumberOffset, 0.08f);

        // Color cian para cartelera UI de nombre
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position + targetInfoOffset, 0.08f);

        // Color amarillo para la cruz de comandos (Command Cross)
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position + commandCrossOffset, 0.08f);

        // Color magenta para submenús
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(transform.position + subMenuOffset, 0.08f);
    }

    #endregion
}