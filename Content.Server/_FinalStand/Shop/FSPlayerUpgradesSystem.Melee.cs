// Part of FSPlayerUpgradesSystem: melee weapons and effects carried by the holder.
using Content.Shared._FinalStand.Shop;
using Content.Server._FinalStand.Leveling;
using Content.Shared.Weapons.Ranged.Components;

using Content.Shared._FinalStand.Chainsaw;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSPlayerUpgradesSystem
{
    private bool TryApplyMelee(EntityUid weapon, EntityUid player, WeaponUpgradeDef def,
        int newLevel, bool spawnItems, FSWeaponUpgradeStateComponent state)
    {
        switch (def.Type)
        {
            case WeaponUpgradeType.MovementSpeed:
                {
                    if (!spawnItems) break;
                    if (!HasComp<GunComponent>(weapon))
                    {
                        state.HeldSpeedBonusPercent += def.ValuePerLevel;
                        _movement.RefreshMovementSpeedModifiers(player);
                    }
                    else
                    {
                        var bonus = EnsureComp<FSSpeedBonusComponent>(player);
                        bonus.SpeedMultiplier += def.ValuePerLevel;
                        _movement.RefreshMovementSpeedModifiers(player);
                    }
                    break;
                }
            case WeaponUpgradeType.ConcussionClub:
                {
                    state.ConcussionClubStunMs += (int)(def.ValuePerLevel * 1000f);
                    break;
                }
            case WeaponUpgradeType.CritVsStunned:
                {
                    state.CritVsStunned = true;
                    break;
                }
            case WeaponUpgradeType.StunOnHit:
                {
                    state.StunOnHitMs += (int)(def.ValuePerLevel * 1000f);
                    break;
                }
            case WeaponUpgradeType.FlintlockCritSynergy:
                {
                    state.FlintlockCritDurationSec += (int)def.ValuePerLevel;
                    if (spawnItems && def.SpawnProtoId.HasValue)
                    {
                        var coords = Transform(player).Coordinates;
                        for (var i = 0; i < def.SpawnCountPerLevel; i++)
                        {
                            var item = Spawn(def.SpawnProtoId.Value, coords);
                            _stash.Stash(player, item);
                        }
                    }
                    break;
                }
            case WeaponUpgradeType.CritVsBurning:
                {
                    state.CritVsBurning = true;
                    break;
                }
            case WeaponUpgradeType.FireResist:
                {
                    state.FireDamageResist = MathF.Min(1f, state.FireDamageResist + def.ValuePerLevel);
                    break;
                }
            case WeaponUpgradeType.WhileBurningBuff:
                {
                    state.WhileBurningBuff = true;
                    break;
                }
            case WeaponUpgradeType.FuelEfficiency:
                {
                    state.FuelEfficiencyReduction += def.ValuePerLevel;
                    break;
                }
            case WeaponUpgradeType.FuelCapacity:
                {
                    state.FuelCapacityMultiplier += def.ValuePerLevel;
                    if (TryComp<FSChainsawFuelComponent>(weapon, out var fuel))
                    {
                        fuel.MaxFuelMultiplier = state.FuelCapacityMultiplier;
                        fuel.CurrentFuel = MathF.Min(fuel.CurrentFuel + fuel.BaseMaxFuel * def.ValuePerLevel,
                                                      fuel.BaseMaxFuel * fuel.MaxFuelMultiplier);
                        Dirty(weapon, fuel);
                    }
                    break;
                }
            case WeaponUpgradeType.WielderResistance:
                {
                    state.WielderResistance = MathF.Min(0.95f, state.WielderResistance + def.ValuePerLevel);
                    break;
                }
            case WeaponUpgradeType.DualWieldEnergySword:
                {
                    var oldState = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                    if (oldState.DualWieldEnergySwordApplied)
                        break;
                    oldState.DualWieldEnergySwordApplied = true;
                    TryTransformToDualWieldEnergySword(weapon, player, oldState, def.ValuePerLevel);
                    break;
                }

            default:
                return false;
        }

        return true;
    }
}
