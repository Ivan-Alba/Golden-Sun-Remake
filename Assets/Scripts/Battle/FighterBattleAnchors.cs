using UnityEngine;

/// <summary>
/// Component attached to Hero and Enemy prefabs to explicitly define 3D spatial anchors 
/// for UI presentation (names, damage numbers, command menus), in-world visual elements (3D selection pointers),
/// and Melee attack stopping positions.
/// </summary>
public class FighterBattleAnchors : MonoBehaviour
{
    [Header("3D Selection Pointer Anchor")]
    [Tooltip("Target transform where the 3D Pointer is attached/positioned.")]
    [SerializeField] private Transform pointerAnchor;

    [Header("Melee Combat Stopping Point")]
    [Tooltip("Optional explicit Transform defining where Melee attackers should stop in front of this target.")]
    [SerializeField] private Transform meleeAttackAnchor;

    [Tooltip("Fallback offset relative to this target's position and forward direction if no explicit Transform is assigned.")]
    [SerializeField] private Vector3 meleeAttackOffset = new Vector3(0f, 0f, 2.2f);

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
    public Transform MeleeAttackAnchor => meleeAttackAnchor;
    public Vector3 MeleeAttackOffset => meleeAttackOffset;
    public Vector3 DamageNumberOffset => damageNumberOffset;
    public Vector3 TargetInfoOffset => targetInfoOffset;
    public Vector3 CommandCrossOffset => commandCrossOffset;
    public Vector3 SubMenuOffset => subMenuOffset;

    #endregion

    #region Public Helper API

    /// <summary>
    /// Calculates absolute world position where a Melee attacker should stop in front of this entity.
    /// Prioritizes explicit transform anchor if assigned; otherwise calculates forward offset based on entity orientation.
    /// </summary>
    public Vector3 GetMeleeAttackWorldPosition()
    {
        if (meleeAttackAnchor != null)
        {
            return meleeAttackAnchor.position;
        }

        // Fallback: Calculates stopping point in front of the target based on its forward direction and offset
        return transform.position + transform.TransformDirection(meleeAttackOffset);
    }

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
        // Color rojo para la posición del ataque Melee
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(GetMeleeAttackWorldPosition(), 0.12f);
        Gizmos.DrawWireCube(GetMeleeAttackWorldPosition(), new Vector3(0.3f, 0.05f, 0.3f));

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