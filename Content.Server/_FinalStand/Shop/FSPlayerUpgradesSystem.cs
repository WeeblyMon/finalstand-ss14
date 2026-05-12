using Content.Shared._FinalStand.Shop;
using Content.Shared.Mind;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.FixedPoint;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Shop;

public sealed class FSPlayerUpgradesSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
    }

    private void OnPlayerAttached(PlayerAttachedEvent ev)
    {
        if (!_mind.TryGetMind(ev.Entity, out var mindId, out _))
            return;
        EnsureComp<FSPlayerUpgradesComponent>(mindId);
        NotifyClient(mindId);
    }

    public int GetLevel(EntityUid mindId, string upgradeId)
    {
        return TryComp<FSPlayerUpgradesComponent>(mindId, out var comp)
            ? comp.Levels.GetValueOrDefault(upgradeId, 0)
            : 0;
    }

    public bool TryPurchase(EntityUid mindId, string upgradeId, int maxLevel, out int newLevel)
    {
        newLevel = 0;
        var comp = EnsureComp<FSPlayerUpgradesComponent>(mindId);
        var current = comp.Levels.GetValueOrDefault(upgradeId, 0);
        if (current >= maxLevel)
            return false;
        newLevel = current + 1;
        comp.Levels[upgradeId] = newLevel;
        return true;
    }

    public void ApplyUpgrades(EntityUid weapon, EntityUid mindId, List<WeaponUpgradeDef> defs)
    {
        if (!TryComp<FSPlayerUpgradesComponent>(mindId, out var upgradeComp))
            return;

        var critChance = 0f;
        var critMult = 2f;
        var pierce = FixedPoint2.Zero;

        foreach (var def in defs)
        {
            var level = upgradeComp.Levels.GetValueOrDefault(def.Id, 0);
            if (level == 0)
                continue;

            switch (def.Type)
            {
                case WeaponUpgradeType.FireRate:
                    if (TryComp<GunComponent>(weapon, out var gun))
                    {
#pragma warning disable RA0002
                        gun.FireRate += def.ValuePerLevel * level;
                        gun.FireRateModified = gun.FireRate;
#pragma warning restore RA0002
                        Dirty(weapon, gun);
                    }
                    break;

                case WeaponUpgradeType.AngleMax:
                    if (TryComp<GunComponent>(weapon, out var gunA))
                    {
                        var deg = Math.Max(0.0, gunA.MaxAngle.Degrees - def.ValuePerLevel * level);
#pragma warning disable RA0002
                        gunA.MaxAngle = Angle.FromDegrees(deg);
                        gunA.MaxAngleModified = Angle.FromDegrees(deg);
#pragma warning restore RA0002
                        Dirty(weapon, gunA);
                    }
                    break;

                case WeaponUpgradeType.SpawnItem:
                    if (def.SpawnProtoId.HasValue)
                    {
                        var coords = Transform(weapon).Coordinates;
                        for (var i = 0; i < def.SpawnCountPerLevel * level; i++)
                            Spawn(def.SpawnProtoId.Value, coords);
                    }
                    break;

                case WeaponUpgradeType.Accuracy:
                    if (TryComp<GunComponent>(weapon, out var gunAcc))
                    {
                        var deg = def.ValuePerLevel * level;
#pragma warning disable RA0002
                        gunAcc.MinAngle = Angle.FromDegrees(Math.Max(0.0, gunAcc.MinAngle.Degrees - deg * 0.5));
                        gunAcc.MaxAngle = Angle.FromDegrees(Math.Max(gunAcc.MinAngle.Degrees, gunAcc.MaxAngle.Degrees - deg * 0.2));
                        gunAcc.AngleIncrease = Angle.FromDegrees(Math.Max(0.0, gunAcc.AngleIncrease.Degrees - deg * 0.3));
                        gunAcc.MinAngleModified = gunAcc.MinAngle;
                        gunAcc.MaxAngleModified = gunAcc.MaxAngle;
                        gunAcc.AngleIncreaseModified = gunAcc.AngleIncrease;
#pragma warning restore RA0002
                        Dirty(weapon, gunAcc);
                    }
                    break;

                case WeaponUpgradeType.MagazineSize:
                    if (TryComp<BallisticAmmoProviderComponent>(weapon, out var bal))
                    {
                        var extra = (int)(def.ValuePerLevel * level);
#pragma warning disable RA0002
                        bal.Capacity += extra;
                        bal.UnspawnedCount = Math.Min(bal.UnspawnedCount + extra, bal.Capacity);
#pragma warning restore RA0002
                        Dirty(weapon, bal);
                    }
                    else if (TryComp<BatteryAmmoProviderComponent>(weapon, out var bat))
                    {
#pragma warning disable RA0002
                        bat.FireCost = Math.Max(1f, bat.FireCost - def.ValuePerLevel * level);
#pragma warning restore RA0002
                        Dirty(weapon, bat);
                    }
                    break;

                case WeaponUpgradeType.ReloadSpeed:
                    break; // stub — implement after staged reload system

                case WeaponUpgradeType.Range:
                    if (TryComp<GunComponent>(weapon, out var gunR))
                    {
#pragma warning disable RA0002
                        gunR.ProjectileSpeed += def.ValuePerLevel * level;
                        gunR.ProjectileSpeedModified = gunR.ProjectileSpeed;
#pragma warning restore RA0002
                        Dirty(weapon, gunR);
                    }
                    break;

                case WeaponUpgradeType.FullAuto:
                    if (TryComp<GunComponent>(weapon, out var gunF))
                    {
#pragma warning disable RA0002
                        gunF.AvailableModes |= SelectiveFire.FullAuto;
                        gunF.SelectedMode = SelectiveFire.FullAuto;
#pragma warning restore RA0002
                        Dirty(weapon, gunF);
                    }
                    break;

                case WeaponUpgradeType.CritChance:
                    critChance += def.ValuePerLevel * level;
                    break;

                case WeaponUpgradeType.CritDamage:
                    critMult = 2f + def.ValuePerLevel * level;
                    break;

                case WeaponUpgradeType.Pierce:
                    pierce += FixedPoint2.New(def.ValuePerLevel * level);
                    break;

                case WeaponUpgradeType.Radius:
                    break; // stub
            }
        }

        if (critChance > 0 || pierce > FixedPoint2.Zero)
        {
            var state = EnsureComp<FSWeaponUpgradeStateComponent>(weapon);
            state.CritChance = Math.Min(critChance, 1f);
            state.CritDamageMultiplier = critMult;
            state.PierceThreshold = pierce;
        }
    }

    public void NotifyClient(EntityUid mindId)
    {
        if (!TryComp<FSPlayerUpgradesComponent>(mindId, out var comp))
            return;
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.UserId == null)
            return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;
        RaiseNetworkEvent(new UpgradeLevelsUpdatedEvent(new Dictionary<string, int>(comp.Levels)),
            Filter.SinglePlayer(session));
    }
}
