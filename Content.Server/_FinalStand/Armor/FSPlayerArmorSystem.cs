using Content.Shared._FinalStand.Armor;

namespace Content.Server._FinalStand.Armor;

// Protection (damage reduction, speed, stamina) is handled by Armor/ClothingSpeedModifier/StaminaResistance
// on the hardsuit item equipped via FSArmorShopSystem. FSPlayerArmorComponent is retained only as a
// tier-ID marker on the mob.
public sealed class FSPlayerArmorSystem : EntitySystem { }
