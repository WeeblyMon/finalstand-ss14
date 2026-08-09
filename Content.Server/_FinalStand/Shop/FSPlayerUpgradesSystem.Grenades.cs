// Part of FSPlayerUpgradesSystem: grenade packs and deployables.
using Content.Shared._FinalStand.Shop;

using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Grenades;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSPlayerUpgradesSystem
{
    private bool TryApplyGrenades(EntityUid weapon, EntityUid player, WeaponUpgradeDef def,
        int newLevel, bool spawnItems, FSWeaponUpgradeStateComponent state)
    {
        switch (def.Type)
        {
            case WeaponUpgradeType.GrenadeCapacity:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack))
                    {
                        pack.MaxStock += (int)def.ValuePerLevel;
                        pack.Stock = Math.Min(pack.Stock + (int)def.ValuePerLevel, pack.MaxStock);
                        Dirty(weapon, pack);
                        _grenadeSelect.SyncPackCounter(weapon, pack);
                    }
                    break;
                }
            case WeaponUpgradeType.GrenadeRegen:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack))
                    {
                        pack.RegenPerWave += (int)def.ValuePerLevel;
                        Dirty(weapon, pack);
                    }
                    break;
                }
            case WeaponUpgradeType.DeployableCapacity:
                {
                    if (TryComp<FSDeployableItemComponent>(weapon, out var deployable))
                    {
                        deployable.MaxStock += (int)def.ValuePerLevel;
                        deployable.Stock = Math.Min(deployable.Stock + (int)def.ValuePerLevel, deployable.MaxStock);
                        Dirty(weapon, deployable);
                    }
                    break;
                }
            case WeaponUpgradeType.GrenadeBurnDuration:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack))
                    {
                        pack.BurnDuration += def.ValuePerLevel;
                        Dirty(weapon, pack);
                    }
                    break;
                }
            case WeaponUpgradeType.GrenadeStunDuration:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack))
                    {
                        pack.StunDuration += def.ValuePerLevel;
                        Dirty(weapon, pack);
                    }
                    break;
                }
            case WeaponUpgradeType.GrenadeBaitDuration:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack))
                    {
                        pack.BaitDuration += def.ValuePerLevel;
                        Dirty(weapon, pack);
                    }
                    break;
                }
            case WeaponUpgradeType.GrenadeImpactFuse:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack))
                    {
                        pack.ImpactFuse = true;
                        Dirty(weapon, pack);
                    }
                    break;
                }
            case WeaponUpgradeType.GrenadeEffectRadius:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack))
                    {
                        pack.EffectRadius += def.ValuePerLevel;
                        Dirty(weapon, pack);
                    }
                    break;
                }
            case WeaponUpgradeType.GrenadeBlastBonus:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack))
                    {
                        pack.BlastBonus += def.ValuePerLevel;
                        Dirty(weapon, pack);
                    }
                    break;
                }
            case WeaponUpgradeType.GrenadeCluster:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack) && !pack.IsCluster)
                    {
                        pack.IsCluster = true;
                        // Cluster grenade costs one capacity slot.
                        pack.MaxStock = Math.Max(1, pack.MaxStock - 1);
                        pack.Stock = Math.Min(pack.Stock, pack.MaxStock);
                        Dirty(weapon, pack);
                        _grenadeSelect.SyncPackCounter(weapon, pack);
                    }
                    break;
                }
            case WeaponUpgradeType.GrenadeSingularity:
                {
                    if (TryComp<FSGrenadePackComponent>(weapon, out var pack) && !pack.IsSingularity)
                    {
                        pack.IsSingularity = true;
                        Dirty(weapon, pack);
                        _grenadeSelect.SyncPackCounter(weapon, pack);
                    }
                    break;
                }

            default:
                return false;
        }

        return true;
    }
}
