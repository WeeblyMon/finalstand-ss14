using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Upgrades;
using Content.Server.Popups;
using Content.Shared._FinalStand.Akimbo;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Shop;

public sealed class FSPlayerUpgradesSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly ProtoId<TagPrototype> AkimboTag = "AkimboEligible";
    private static readonly string[] InventorySlotPriority = ["belt", "suitstorage", "pocket1", "pocket2"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, EntInsertedIntoContainerMessage>(OnMagInsertedToGun);
        SubscribeLocalEvent<FSWeaponUpgradeStateComponent, EntRemovedFromContainerMessage>(OnMagRemovedFromGun);
    }

    public void ApplySingleUpgrade(EntityUid weapon, EntityUid player, WeaponUpgradeDef def, int newLevel, bool spawnItems = true)
    {
        switch (def.Type)
        {
            case WeaponUpgradeType.FireRate:
                if (TryComp<GunComponent>(weapon, out var gun))
                {
#pragma warning disable RA0002
                    gun.FireRate += def.ValuePerLevel;
                    gun.FireRateModified = gun.FireRate;
#pragma warning restore RA0002
                    Dirty(weapon, gun);
                }
                break;

            case WeaponUpgradeType.AngleMax:
                if (TryComp<GunComponent>(weapon, out var gunA))
                {
                    var deg = Math.Max(0.0, gunA.MaxAngle.Degrees - def.ValuePerLevel);
#pragma warning disable RA0002
                    gunA.MaxAngle = Angle.FromDegrees(deg);
                    gunA.MaxAngleModified = gunA.MaxAngle;
#pragma warning restore RA0002
                    Dirty(weapon, gunA);
                }
                {
                    var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                    state.PelletSpreadMultiplier = Math.Max(0.1f, state.PelletSpreadMultiplier - 0.16f);
                }
                break;

            case WeaponUpgradeType.Accuracy:
                if (TryComp<GunComponent>(weapon, out var gunAcc))
                {
                    var d = def.ValuePerLevel;
#pragma warning disable RA0002
                    gunAcc.MinAngle = Angle.FromDegrees(Math.Max(0.0, gunAcc.MinAngle.Degrees - d * 0.5));
                    gunAcc.MaxAngle = Angle.FromDegrees(Math.Max(gunAcc.MinAngle.Degrees, gunAcc.MaxAngle.Degrees - d * 0.2));
                    gunAcc.AngleIncrease = Angle.FromDegrees(Math.Max(0.0, gunAcc.AngleIncrease.Degrees - d * 0.3));
                    gunAcc.MinAngleModified = gunAcc.MinAngle;
                    gunAcc.MaxAngleModified = gunAcc.MaxAngle;
                    gunAcc.AngleIncreaseModified = gunAcc.AngleIncrease;
#pragma warning restore RA0002
                    Dirty(weapon, gunAcc);
                }
                if (TryComp<GunWieldBonusComponent>(weapon, out var wieldBonus))
                {
                    var d = def.ValuePerLevel;
#pragma warning disable RA0002
                    wieldBonus.MinAngle = Angle.FromDegrees(wieldBonus.MinAngle.Degrees - d);
                    wieldBonus.MaxAngle = Angle.FromDegrees(wieldBonus.MaxAngle.Degrees - d);
#pragma warning restore RA0002
                    Dirty(weapon, wieldBonus);
                }
                _gun.RefreshModifiers(weapon);
                {
                    var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                    state.PelletSpreadMultiplier = Math.Max(0.1f, state.PelletSpreadMultiplier - 0.16f);
                }
                break;

            case WeaponUpgradeType.SpawnItem:
                if (spawnItems && def.SpawnProtoId.HasValue)
                {
                    var coords = Transform(player).Coordinates;
                    for (var i = 0; i < def.SpawnCountPerLevel; i++)
                    {
                        var item = Spawn(def.SpawnProtoId.Value, coords);
                        TryStashOnPlayer(player, item);
                    }
                }
                break;

            case WeaponUpgradeType.MagazineSize:
                if (TryComp<BallisticAmmoProviderComponent>(weapon, out var bal))
                {
                    var extra = (int)def.ValuePerLevel;
#pragma warning disable RA0002
                    bal.Capacity += extra;
                    bal.UnspawnedCount = Math.Min(bal.UnspawnedCount + extra, bal.Capacity);
#pragma warning restore RA0002
                    Dirty(weapon, bal);
                    _gun.RefreshModifiers(weapon);
                }
                else if (TryComp<BatteryAmmoProviderComponent>(weapon, out var bat))
                {
#pragma warning disable RA0002
                    bat.FireCost = Math.Max(1f, bat.FireCost - def.ValuePerLevel);
#pragma warning restore RA0002
                    var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                    state.BatteryFireCostReduction += def.ValuePerLevel;
                    Dirty(weapon, bat);
                }
                else if (HasComp<ChamberMagazineAmmoProviderComponent>(weapon))
                {
                    var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                    state.MagazineSizeBonus += (int)def.ValuePerLevel;
                    ApplyMagSizeBonusToCurrentMag(weapon, (int)def.ValuePerLevel);
                    _gun.RefreshModifiers(weapon);
                }
                break;

            case WeaponUpgradeType.Range:
                if (TryComp<GunComponent>(weapon, out var gunR))
                {
#pragma warning disable RA0002
                    gunR.ProjectileSpeed += def.ValuePerLevel;
                    gunR.ProjectileSpeedModified = gunR.ProjectileSpeed;
#pragma warning restore RA0002
                    Dirty(weapon, gunR);
                }
                break;

            case WeaponUpgradeType.FullAuto:
                if (newLevel >= 1 && TryComp<GunComponent>(weapon, out var gunF))
                {
#pragma warning disable RA0002
                    gunF.AvailableModes |= SelectiveFire.FullAuto;
                    gunF.SelectedMode = SelectiveFire.FullAuto;
#pragma warning restore RA0002
                    Dirty(weapon, gunF);
                }
                break;

            case WeaponUpgradeType.CritChance:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.CritChance = Math.Min(state.CritChance + def.ValuePerLevel, 1f);
                break;
            }

            case WeaponUpgradeType.CritDamage:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.CritDamageMultiplier += def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.Pierce:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.PierceThreshold += FixedPoint2.New(def.ValuePerLevel);
                break;
            }

            case WeaponUpgradeType.Akimbo:
                if (newLevel == 1)
                    TryApplyAkimbo(weapon, player);
                break;

            case WeaponUpgradeType.SpeedLoader:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.SpeedLoaderEnabled = true;
                break;
            }

            case WeaponUpgradeType.ExplosiveShot:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ExplosiveShotLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.MoneyGainBonus:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.MoneyGainBonusPerKill += (int)def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.MoneyPerHit:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.MoneyPerHitBonus += (int)def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.Slowing:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.SlowingEnabled = true;
                break;
            }

            case WeaponUpgradeType.BeamChaining:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.BeamChainTargets = newLevel;
                break;
            }

            case WeaponUpgradeType.Knockback:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.KnockbackLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.SelfChargeSpeed:
                if (TryComp<BatterySelfRechargerComponent>(weapon, out var selfCharge))
                {
                    selfCharge.AutoRechargeRate *= (float)def.ValuePerLevel;
                    Dirty(weapon, selfCharge);
                    _battery.RefreshChargeRate((weapon, null));
                }
                break;

            case WeaponUpgradeType.SetOnFire:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.SetOnFireEnabled = true;
                break;
            }

            case WeaponUpgradeType.APRounds:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.APRoundsEnabled = true;
                break;
            }

            case WeaponUpgradeType.ArmorShred:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ArmorShredMagnitude += def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.Recoil:
                break;

            case WeaponUpgradeType.ReloadSpeed:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ReloadSpeedMultiplier = MathF.Max(0.1f, 1.0f - newLevel * def.ValuePerLevel);
                break;
            }

            case WeaponUpgradeType.LifeSteal:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.LifeStealPercent = newLevel * def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.StaminaSteal:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.StaminaStealLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.AttackSpeed:
                if (TryComp<GunComponent>(weapon, out var burstGun))
                {
#pragma warning disable RA0002
                    burstGun.BurstFireRate += def.ValuePerLevel;
#pragma warning restore RA0002
                    Dirty(weapon, burstGun);
                }
                else
                {
                    var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                    state.AttackSpeedMultiplier += def.ValuePerLevel;
                }
                break;

            case WeaponUpgradeType.MovementSpeed:
            {
                if (!spawnItems) break;
                if (!HasComp<GunComponent>(weapon))
                {
                    var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
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

            case WeaponUpgradeType.Radius:
                break;

            case WeaponUpgradeType.PelletCount:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ExtraPellets += (int)def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.Scrapshot:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ScrapshotEnabled = true;
                state.ExtraPellets += 3;
                if (TryComp<GunComponent>(weapon, out var gunSc))
                {
#pragma warning disable RA0002
                    gunSc.MaxAngle = Angle.FromDegrees(gunSc.MaxAngle.Degrees + 15.0);
                    gunSc.MaxAngleModified = gunSc.MaxAngle;
#pragma warning restore RA0002
                    Dirty(weapon, gunSc);
                }
                break;
            }

            case WeaponUpgradeType.Bleed:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.BleedLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.SlamFire:
            {
                if (TryComp<GunComponent>(weapon, out var gunSlam))
                {
#pragma warning disable RA0002
                    gunSlam.AvailableModes |= SelectiveFire.FullAuto;
                    gunSlam.SelectedMode = SelectiveFire.FullAuto;
                    gunSlam.FireRate *= 1.4f;
                    gunSlam.FireRateModified = gunSlam.FireRate;
#pragma warning restore RA0002
                    Dirty(weapon, gunSlam);
                }
                break;
            }

            case WeaponUpgradeType.FlechetteRounds:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.FlechetteEnabled = true;
                break;
            }

            case WeaponUpgradeType.SplinterImpact:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.SplinterImpactEnabled = true;
                break;
            }

            case WeaponUpgradeType.OverchargeShot:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.OverchargeShotEnabled = true;
                break;
            }

            case WeaponUpgradeType.Damage:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.DamageMultiplier += def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.Overkill:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.OverkillLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.Execution:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ExecutionEnabled = true;
                break;
            }

            case WeaponUpgradeType.WarTorn:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.WarTornEnabled = true;
                var wt = EnsureComp<FSWarTornComponent>(weapon);
                wt.BonusPerStack = newLevel * 0.02f;
                wt.MaxStacks = newLevel switch { 1 => 15, 2 => 30, _ => 50 };
                break;
            }

            case WeaponUpgradeType.Suppression:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.SuppressionLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.Resonance:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ResonanceEnabled = true;
                EnsureComp<FSResonanceComponent>(weapon);
                break;
            }

            case WeaponUpgradeType.Prismatic:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.PrismaticLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.MagEfficiency:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.MagEfficiencyLevel = newLevel;
                break;
            }

            case WeaponUpgradeType.PulseCascade:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.PulseCascadeEnabled = true;
                break;
            }

            case WeaponUpgradeType.Aftershock:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.AftershockEnabled = true;
                break;
            }

            case WeaponUpgradeType.ConcussionClub:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.ConcussionClubStunMs += (int)(def.ValuePerLevel * 1000f);
                break;
            }

            case WeaponUpgradeType.CritVsStunned:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.CritVsStunned = true;
                break;
            }

            case WeaponUpgradeType.StunOnHit:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.StunOnHitMs += (int)(def.ValuePerLevel * 1000f);
                break;
            }

            case WeaponUpgradeType.FlintlockCritSynergy:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.FlintlockCritDurationSec += (int)def.ValuePerLevel;
                if (spawnItems && def.SpawnProtoId.HasValue)
                {
                    var coords = Transform(player).Coordinates;
                    for (var i = 0; i < def.SpawnCountPerLevel; i++)
                    {
                        var item = Spawn(def.SpawnProtoId.Value, coords);
                        TryStashOnPlayer(player, item);
                    }
                }
                break;
            }

            case WeaponUpgradeType.CritVsBurning:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.CritVsBurning = true;
                break;
            }

            case WeaponUpgradeType.FireResist:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.FireDamageResist = MathF.Min(1f, state.FireDamageResist + def.ValuePerLevel);
                break;
            }

            case WeaponUpgradeType.WhileBurningBuff:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.WhileBurningBuff = true;
                break;
            }

            case WeaponUpgradeType.FuelEfficiency:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.FuelEfficiencyReduction += def.ValuePerLevel;
                break;
            }

            case WeaponUpgradeType.FuelCapacity:
            {
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
                state.FuelCapacityMultiplier += def.ValuePerLevel;
                if (TryComp<Content.Shared._FinalStand.Chainsaw.FSChainsawFuelComponent>(weapon, out var fuel))
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
                var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
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
        }
    }

    private void TryTransformToDualWieldEnergySword(EntityUid oldSword, EntityUid player,
        FSWeaponUpgradeStateComponent oldState, float attackSpeedBonus)
    {
        var coords = Transform(player).Coordinates;
        var newSword = Spawn("EnergySwordDouble", coords);

        var newState = EnsureComp<FSWeaponUpgradeStateComponent>(newSword);
        CopyUpgradeState(oldState, newState);
        newState.AttackSpeedMultiplier = oldState.AttackSpeedMultiplier + attackSpeedBonus;
        newState.DualWieldEnergySwordApplied = true;

        // only delete old sword if we can safely drop it — avoids silently losing upgrade state
        var safeToDelete = true;
        var wasInHand = false;
        if (TryComp<HandsComponent>(player, out var hands)
            && _hands.IsHolding((player, hands), oldSword, out _))
        {
            wasInHand = true;
            safeToDelete = _hands.TryDrop((player, hands), oldSword);
            if (!safeToDelete)
                Log.Warning($"DualWieldEnergySword: TryDrop failed for {ToPrettyString(oldSword)} on {ToPrettyString(player)} — keeping old sword to avoid losing upgrade state.");
        }

        if (safeToDelete)
            QueueDel(oldSword);

        if (wasInHand && hands != null && _hands.TryPickup(player, newSword, handsComp: hands))
            return;

        TryStashOnPlayer(player, newSword);
    }

    // single source of truth for transform upgrades — add new DataFields here to avoid silent state loss
    private static void CopyUpgradeState(FSWeaponUpgradeStateComponent from, FSWeaponUpgradeStateComponent to)
    {
        to.CritChance = from.CritChance;
        to.CritDamageMultiplier = from.CritDamageMultiplier;
        to.PierceThreshold = from.PierceThreshold;
        to.Levels = new Dictionary<string, int>(from.Levels);
        to.ExplosiveShotLevel = from.ExplosiveShotLevel;
        to.MoneyGainBonusPerKill = from.MoneyGainBonusPerKill;
        to.MoneyPerHitBonus = from.MoneyPerHitBonus;
        to.SlowingEnabled = from.SlowingEnabled;
        to.BeamChainTargets = from.BeamChainTargets;
        to.KnockbackLevel = from.KnockbackLevel;
        to.SetOnFireEnabled = from.SetOnFireEnabled;
        to.APRoundsEnabled = from.APRoundsEnabled;
        to.ArmorShredMagnitude = from.ArmorShredMagnitude;
        to.ReloadSpeedMultiplier = from.ReloadSpeedMultiplier;
        to.SpeedLoaderEnabled = from.SpeedLoaderEnabled;
        to.LifeStealPercent = from.LifeStealPercent;
        to.StaminaStealLevel = from.StaminaStealLevel;
        to.DamageMultiplier = from.DamageMultiplier;
        to.ExtraPellets = from.ExtraPellets;
        to.ScrapshotEnabled = from.ScrapshotEnabled;
        to.BleedLevel = from.BleedLevel;
        to.FlechetteEnabled = from.FlechetteEnabled;
        to.SplinterImpactEnabled = from.SplinterImpactEnabled;
        to.OverchargeShotEnabled = from.OverchargeShotEnabled;
        to.PelletSpreadMultiplier = from.PelletSpreadMultiplier;
        to.OverkillLevel = from.OverkillLevel;
        to.ExecutionEnabled = from.ExecutionEnabled;
        to.WarTornEnabled = from.WarTornEnabled;
        to.SuppressionLevel = from.SuppressionLevel;
        to.ResonanceEnabled = from.ResonanceEnabled;
        to.PrismaticLevel = from.PrismaticLevel;
        to.MagEfficiencyLevel = from.MagEfficiencyLevel;
        to.PulseCascadeEnabled = from.PulseCascadeEnabled;
        to.AftershockEnabled = from.AftershockEnabled;
        to.MagazineSizeBonus = from.MagazineSizeBonus;
        to.AttackSpeedMultiplier = from.AttackSpeedMultiplier;
        to.ConcussionClubStunMs = from.ConcussionClubStunMs;
        to.CritVsStunned = from.CritVsStunned;
        to.StunOnHitMs = from.StunOnHitMs;
        to.FlintlockCritDurationSec = from.FlintlockCritDurationSec;
        to.CritVsBurning = from.CritVsBurning;
        to.FireDamageResist = from.FireDamageResist;
        to.WhileBurningBuff = from.WhileBurningBuff;
        to.FuelEfficiencyReduction = from.FuelEfficiencyReduction;
        to.FuelCapacityMultiplier = from.FuelCapacityMultiplier;
        to.WielderResistance = from.WielderResistance;
        to.DualWieldEnergySwordApplied = from.DualWieldEnergySwordApplied;
        to.HeldSpeedBonusPercent = from.HeldSpeedBonusPercent;
        to.TotalSpent = from.TotalSpent;
    }

    public void TryApplyAkimbo(EntityUid gun, EntityUid player)
    {
        if (!_tags.HasTag(gun, AkimboTag))
            return;
        if (HasComp<FSAkimboGunComponent>(gun))
            return;

        EnsureComp<FSAkimboGunComponent>(gun);

        if (TryComp<GunComponent>(gun, out var gunComp))
        {
#pragma warning disable RA0002
            gunComp.AvailableModes |= SelectiveFire.FullAuto;
            gunComp.SelectedMode = SelectiveFire.FullAuto;
#pragma warning restore RA0002
            Dirty(gun, gunComp);
        }

        RemComp<GunRequiresWieldComponent>(gun);
        _gun.RefreshModifiers(gun);
        _popup.PopupEntity("Akimbo activated!", gun, player);
    }

    private void TryStashOnPlayer(EntityUid player, EntityUid item)
    {
        foreach (var slot in InventorySlotPriority)
        {
            if (_inventory.TryEquip(player, item, slot, silent: true))
                return;
        }
        if (_inventory.TryGetSlotEntity(player, "back", out var backpack))
            _storage.Insert(backpack.Value, item, out _, user: player, playSound: false);
    }

    private void ApplyMagSizeBonusToCurrentMag(EntityUid gun, int bonus)
    {
        if (!_itemSlots.TryGetSlot(gun, SharedGunSystem.MagazineSlot, out var slot))
            return;
        var mag = slot.Item;
        if (mag == null || !TryComp<BallisticAmmoProviderComponent>(mag.Value, out var bal))
            return;
#pragma warning disable RA0002
        bal.Capacity += bonus;
        bal.UnspawnedCount = Math.Min(bal.UnspawnedCount + bonus, bal.Capacity);
#pragma warning restore RA0002
        var upgraded = EnsureComp<FSMagUpgradedComponent>(mag.Value);
        upgraded.AppliedBonus += bonus;
        Dirty(mag.Value, bal);
    }

    private void OnMagInsertedToGun(EntityUid gun, FSWeaponUpgradeStateComponent state,
        EntInsertedIntoContainerMessage args)
    {
        if (state.MagazineSizeBonus <= 0) return;
        if (!TryComp<BallisticAmmoProviderComponent>(args.Entity, out var bal)) return;

        var upgraded = EnsureComp<FSMagUpgradedComponent>(args.Entity);
        var diff = state.MagazineSizeBonus - upgraded.AppliedBonus;
        if (diff <= 0) return;

#pragma warning disable RA0002
        bal.Capacity += diff;
        bal.UnspawnedCount = Math.Min(bal.UnspawnedCount + diff, bal.Capacity);
#pragma warning restore RA0002
        upgraded.AppliedBonus = state.MagazineSizeBonus;
        Dirty(args.Entity, bal);
    }

    private void OnMagRemovedFromGun(EntityUid gun, FSWeaponUpgradeStateComponent state,
        EntRemovedFromContainerMessage args)
    {
        if (!TryComp<FSMagUpgradedComponent>(args.Entity, out var upgraded) || upgraded.AppliedBonus <= 0)
            return;
        if (!TryComp<BallisticAmmoProviderComponent>(args.Entity, out var bal))
            return;

#pragma warning disable RA0002
        bal.Capacity -= upgraded.AppliedBonus;
        bal.UnspawnedCount = Math.Min(bal.UnspawnedCount, bal.Capacity);
#pragma warning restore RA0002
        upgraded.AppliedBonus = 0;
        Dirty(args.Entity, bal);
    }
}
