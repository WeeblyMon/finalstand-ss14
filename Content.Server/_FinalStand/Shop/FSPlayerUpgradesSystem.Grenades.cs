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
            case WeaponUpgradeType.AmmoBoxUses:
                {
                    if (TryComp<FSAmmoBoxComponent>(weapon, out var box))
                    {
                        box.MaxUses += (int)def.ValuePerLevel;
                        box.UsesLeft = box.MaxUses;
                        Dirty(weapon, box);
                        _appearance.SetData(weapon, FSAmmoBoxVisuals.Upgraded, true);
                    }
                    break;
                }
            case WeaponUpgradeType.AmmoBoxSpeed:
                {
                    if (TryComp<FSAmmoBoxComponent>(weapon, out var box))
                    {
                        var seconds = box.RefillDuration.TotalSeconds - def.ValuePerLevel;
                        box.RefillDuration = TimeSpan.FromSeconds(Math.Max(0.5, seconds));
                        Dirty(weapon, box);
                    }
                    break;
                }
            case WeaponUpgradeType.SentryAmmo:
                {
                    if (TryComp<FSSentryTurretComponent>(weapon, out var turret))
                    {
                        var bonus = (int)def.ValuePerLevel;
                        turret.MaxAmmo += bonus;
                        turret.Ammo = turret.MaxAmmo;
                        Dirty(weapon, turret);
                        ForEachDeployedSentry(weapon, player, t =>
                        {
                            t.MaxAmmo += bonus;
                            t.Ammo = Math.Min(t.Ammo + bonus, t.MaxAmmo);
                        });
                    }
                    break;
                }
            case WeaponUpgradeType.SentryFireRate:
                {
                    if (TryComp<FSSentryTurretComponent>(weapon, out var turret))
                    {
                        turret.FireInterval = MathF.Max(0.1f, turret.FireInterval * (1f - def.ValuePerLevel));
                        Dirty(weapon, turret);
                        ForEachDeployedSentry(weapon, player, t =>
                            t.FireInterval = MathF.Max(0.1f, t.FireInterval * (1f - def.ValuePerLevel)));
                    }
                    break;
                }
            case WeaponUpgradeType.SentryDamage:
                {
                    if (TryComp<FSSentryTurretComponent>(weapon, out var turret))
                    {
                        turret.DamageMultiplier += def.ValuePerLevel;
                        Dirty(weapon, turret);
                        ForEachDeployedSentry(weapon, player, t => t.DamageMultiplier += def.ValuePerLevel);
                    }
                    break;
                }
            case WeaponUpgradeType.SentryRange:
                {
                    if (TryComp<FSSentryTurretComponent>(weapon, out var turret))
                    {
                        turret.Range += def.ValuePerLevel;
                        Dirty(weapon, turret);
                        ForEachDeployedSentry(weapon, player, t => t.Range += def.ValuePerLevel);
                    }
                    break;
                }
            case WeaponUpgradeType.DeployableRegen:
                {
                    if (TryComp<FSDeployableItemComponent>(weapon, out var deployable))
                    {
                        deployable.RegenPerWave += (int)def.ValuePerLevel;
                        Dirty(weapon, deployable);
                    }
                    break;
                }
            case WeaponUpgradeType.LandmineDamage:
                {
                    if (TryComp<FSLandmineComponent>(weapon, out var mine))
                    {
                        mine.IntensityMultiplier += def.ValuePerLevel;
                        Dirty(weapon, mine);
                    }
                    break;
                }
            case WeaponUpgradeType.LandmineDetonations:
                {
                    if (TryComp<FSLandmineComponent>(weapon, out var mine))
                    {
                        mine.Detonations += (int)def.ValuePerLevel;
                        Dirty(weapon, mine);
                    }
                    break;
                }
            case WeaponUpgradeType.LandmineHighExplosive:
                {
                    if (TryComp<FSLandmineComponent>(weapon, out var mine))
                    {
                        mine.HighExplosive = true;
                        Dirty(weapon, mine);
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

    // Turret upgrades are bought against the item in hand, but a turret already on the ground is a
    // separate entity that only copied stats from the item at deploy time. Without this, buying an
    // upgrade after placing a turret does nothing to the one already down.
    private void ForEachDeployedSentry(EntityUid item, EntityUid player, Action<FSSentryTurretComponent> apply)
    {
        if (!_mind.TryGetMind(player, out var mindId, out _))
            return;

        if (!TryComp<FSDeployableItemComponent>(item, out var deployable))
            return;

        var query = EntityQueryEnumerator<FSSentryTurretComponent, FSDeployedByComponent>();
        while (query.MoveNext(out var uid, out var turret, out var deployedBy))
        {
            if (deployedBy.OwnerMind != mindId || deployedBy.SourceProto.Id != deployable.DeployedProtoId.Id)
                continue;

            apply(turret);
            Dirty(uid, turret);
        }
    }
}
