// File: SFXType.cs
// Defines strongly-typed identifiers for all game sound effects.
public enum SFXType
{
    None,

    // UI Sound Effects
    UI_Confirm,
    UI_Move,
    UI_Cancel,
    UI_Error,
    UI_Cure,
    UI_ItemFound,
    UI_Equip,
    UI_Count,

    // World & Exploration Sound Effects
    World_ChestOpen,
    World_DoorOpen,
    World_PsynergyCast,
    World_PsynergySuccess,
    World_PsynergyFail,

    World_Jump,

    // Combat Sound Effects
    Combat_AttackSwing,

    // Physical Hit SFX
    Combat_HitSlash,
    Combat_HitBlunt,
    Combat_HitPierce,

    // Physical Elemental Hit SFX
    Combat_HitVenus,
    Combat_HitMars,
    Combat_HitJupiter,
    Combat_HitMercury,

    // Magical Elemental Hit SFX
    Combat_MagicVenus,
    Combat_MagicMars,
    Combat_MagicJupiter,
    Combat_MagicMercury,

    // Healing SFX
    Combat_Heal
}