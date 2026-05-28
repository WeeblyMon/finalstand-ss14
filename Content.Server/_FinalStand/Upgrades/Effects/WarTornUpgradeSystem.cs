using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Upgrades;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Hands;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Upgrades.Effects;

// kill stacks increase AKMS damage; resets at wave end
public sealed class WarTornUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
        SubscribeLocalEvent<FSRifleKillTrackerComponent, MobStateChangedEvent>(OnKill);
        SubscribeLocalEvent<FSWarTornComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnd);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (!TryComp<FSWarTornComponent>(ev.Weapon.Value, out var warTorn))
            return;
        if (!HasComp<WaveSpawnedTagComponent>(ev.Target))
            return;

        if (ev.Shooter != null)
            warTorn.Shooter = ev.Shooter;

        var tracker = EnsureComp<FSRifleKillTrackerComponent>(ev.Target);
        tracker.Weapon = ev.Weapon;
        tracker.Shooter = ev.Shooter;
    }

    private void OnKill(EntityUid uid, FSRifleKillTrackerComponent tracker, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead)
            return;
        if (tracker.Weapon is not { } gun || !Exists(gun))
            return;
        if (!TryComp<FSWarTornComponent>(gun, out var warTorn))
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(gun, out var state))
            return;
        if (warTorn.Stacks >= warTorn.MaxStacks)
            return;

        state.DamageMultiplier -= warTorn.CurrentBonus;
        warTorn.Stacks++;
        warTorn.CurrentBonus = warTorn.Stacks * warTorn.BonusPerStack;
        state.DamageMultiplier += warTorn.CurrentBonus;
        BroadcastState(warTorn);
    }

    private void OnUnequipped(EntityUid uid, FSWarTornComponent warTorn, GotUnequippedHandEvent args)
    {
        if (warTorn.Stacks <= 0)
            return;
        BroadcastZero(warTorn);
    }

    private void OnWaveEnd(ref WaveEndedEvent args)
    {
        var query = EntityQueryEnumerator<FSWarTornComponent, FSWeaponUpgradeStateComponent>();
        while (query.MoveNext(out _, out var warTorn, out var state))
        {
            state.DamageMultiplier -= warTorn.CurrentBonus;
            warTorn.Stacks = 0;
            warTorn.CurrentBonus = 0f;
            BroadcastZero(warTorn);
        }
    }

    private void BroadcastState(FSWarTornComponent comp)
        => BroadcastToShooter(comp, comp.Stacks, (int)MathF.Round(comp.CurrentBonus * 100f));

    private void BroadcastZero(FSWarTornComponent comp)
        => BroadcastToShooter(comp, 0, 0);

    private void BroadcastToShooter(FSWarTornComponent comp, int stacks, int bonusPct)
    {
        if (comp.Shooter is not { } shooter || !Exists(shooter))
            return;
        if (!_mind.TryGetMind(shooter, out _, out var mind) || mind?.UserId == null)
            return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        RaiseNetworkEvent(
            new FSWarTornStateEvent(stacks, comp.MaxStacks, bonusPct),
            Filter.SinglePlayer(session));
    }
}
