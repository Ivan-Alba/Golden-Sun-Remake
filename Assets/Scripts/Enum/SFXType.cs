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

    // Combat Sound Effects
    Combat_AttackSwing,
}