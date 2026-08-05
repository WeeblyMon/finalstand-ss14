// Part of FSPlayerUpgradesSystem: what a shot or hit does on top of its damage.
using Content.Shared._FinalStand.Shop;
using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.FixedPoint;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSPlayerUpgradesSystem
{
    private bool TryApplyShotEffects(EntityUid weapon, EntityUid player, WeaponUpgradeDef def,
        int newLevel, bool spawnItems, FSWeaponUpgradeStateComponent state)
    {
        switch (def.Type)
        {
            case WeaponUpgradeType.CritChance:
                {
                    state.CritChance = Math.Min(state.CritChance + def.ValuePerLevel, 1f);
                    break;
                }
            case WeaponUpgradeType.CritDamage:
                {
                    state.CritDamageMultiplier += def.ValuePerLevel;
                    break;
                }
            case WeaponUpgradeType.Pierce:
                {
                    state.PierceThreshold += FixedPoint2.New(def.ValuePerLevel);
                    break;
                }
            case WeaponUpgradeType.Overclocked:
                {
                    var oc = EnsureComp<FSOverclockedComponent>(weapon);
                    oc.Level = newLevel;
                    break;
                }
            case WeaponUpgradeType.IronBeast:
                EnsureComp<FSIronBeastComponent>(weapon);
                break;
            case WeaponUpgradeType.ExplosiveShot:
                {
                    state.ExplosiveShotLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.MoneyGainBonus:
                {
                    state.MoneyGainBonusPerKill += (int)def.ValuePerLevel;
                    if (def.Id == "knife-golden" && newLevel >= def.MaxLevel)
                    {
                        state.KnifeGolden = true;
                        Dirty(weapon, state);
                    }
                    break;
                }
            case WeaponUpgradeType.MoneyPerHit:
                {
                    state.MoneyPerHitBonus += (int)def.ValuePerLevel;
                    break;
                }
            case WeaponUpgradeType.Slowing:
                {
                    state.SlowingEnabled = true;
                    break;
                }
            case WeaponUpgradeType.BeamChaining:
                {
                    state.BeamChainTargets = newLevel;
                    break;
                }
            case WeaponUpgradeType.Knockback:
                {
                    state.KnockbackLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.SetOnFire:
                {
                    state.SetOnFireEnabled = true;
                    break;
                }
            case WeaponUpgradeType.APRounds:
                {
                    state.APRoundsEnabled = true;
                    break;
                }
            case WeaponUpgradeType.ArmorShred:
                {
                    state.ArmorShredMagnitude += def.ValuePerLevel;
                    break;
                }
            case WeaponUpgradeType.LifeSteal:
                {
                    state.LifeStealPercent = newLevel * def.ValuePerLevel;
                    break;
                }
            case WeaponUpgradeType.StaminaSteal:
                {
                    state.StaminaStealLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.Radius:
                {
                    state.BlastRadiusBonus += (int)def.ValuePerLevel;
                    break;
                }
            case WeaponUpgradeType.Bleed:
                {
                    state.BleedLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.FlechetteRounds:
                {
                    state.FlechetteEnabled = true;
                    break;
                }
            case WeaponUpgradeType.SplinterImpact:
                {
                    state.SplinterImpactEnabled = true;
                    break;
                }
            case WeaponUpgradeType.OverchargeShot:
                {
                    state.OverchargeShotEnabled = true;
                    break;
                }
            case WeaponUpgradeType.Damage:
                {
                    state.DamageMultiplier += def.ValuePerLevel;
                    break;
                }
            case WeaponUpgradeType.Overkill:
                {
                    state.OverkillLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.Execution:
                {
                    state.ExecutionEnabled = true;
                    Dirty(weapon, state);
                    break;
                }
            case WeaponUpgradeType.WarTorn:
                {
                    state.WarTornEnabled = true;
                    var wt = EnsureComp<FSWarTornComponent>(weapon);
                    wt.BonusPerStack = newLevel * 0.02f;
                    wt.MaxStacks = newLevel switch { 1 => 15, 2 => 30, _ => 50 };
                    break;
                }
            case WeaponUpgradeType.Suppression:
                {
                    state.SuppressionLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.Resonance:
                {
                    state.ResonanceEnabled = true;
                    EnsureComp<FSResonanceComponent>(weapon);
                    break;
                }
            case WeaponUpgradeType.Prismatic:
                {
                    state.PrismaticLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.OverloadRound:
                {
                    state.OverloadRoundLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.HomingBolts:
                {
                    state.HomingLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.Multishot:
                {
                    state.MultishotLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.PulseCascade:
                {
                    state.PulseCascadeEnabled = true;
                    break;
                }
            case WeaponUpgradeType.Aftershock:
                {
                    state.AftershockEnabled = true;
                    break;
                }
            case WeaponUpgradeType.VaporiseWeakMob:
                {
                    state.VaporiseWeakMobEnabled = true;
                    break;
                }
            case WeaponUpgradeType.PointBlankCrit:
                {
                    state.PointBlankCritEnabled = true;
                    break;
                }
            case WeaponUpgradeType.ExecutionShot:
                {
                    state.ExecutionShotUpgradeEnabled = true;
                    break;
                }
            case WeaponUpgradeType.MarksmansRhythm:
                {
                    EnsureComp<FSMarksmansRhythmComponent>(weapon);
                    break;
                }
            case WeaponUpgradeType.ClusterBarrage:
                {
                    state.ClusterBarrageEnabled = true;
                    break;
                }
            case WeaponUpgradeType.Barrage:
                {
                    var barrage = EnsureComp<FSBarrageComponent>(weapon);
                    barrage.Level = newLevel;
                    break;
                }
            case WeaponUpgradeType.ShapedCharge:
                {
                    state.ShapedChargeLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.RadiationCoating:
                {
                    state.RadiationCoatingLevel = newLevel;
                    break;
                }
            case WeaponUpgradeType.GravitonCore:
                {
                    var core = EnsureComp<FSGravitonCoreComponent>(weapon);
                    core.Level = newLevel;
                    break;
                }
            case WeaponUpgradeType.TeslaArcRange:
                {
                    state.TeslaArcRangeBonus += def.ValuePerLevel;
                    break;
                }

            default:
                return false;
        }

        return true;
    }
}
