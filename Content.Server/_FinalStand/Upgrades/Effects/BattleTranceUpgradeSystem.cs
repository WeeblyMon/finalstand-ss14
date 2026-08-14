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
public sealed partial class BattleTranceUpgradeSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
        SubscribeLocalEvent<FSRifleKillTrackerComponent, MobStateChangedEvent>(OnKill);
        SubscribeLocalEvent<FSBattleTranceComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<FSBattleTranceComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnd);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (!TryComp<FSBattleTranceComponent>(ev.Weapon.Value, out var battleTrance))
            return;
        if (!HasComp<WaveSpawnedTagComponent>(ev.Target))
            return;

        if (ev.Shooter != null)
            battleTrance.Shooter = ev.Shooter;

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
        if (!TryComp<FSBattleTranceComponent>(gun, out var battleTrance))
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(gun, out var state))
            return;
        if (battleTrance.Stacks >= battleTrance.MaxStacks)
            return;

        state.DamageMultiplier -= battleTrance.CurrentBonus;
        battleTrance.Stacks++;
        battleTrance.CurrentBonus = battleTrance.Stacks * battleTrance.BonusPerStack;
        state.DamageMultiplier += battleTrance.CurrentBonus;
        BroadcastState(battleTrance);
    }

    // Stacks live on the weapon and survive being dropped — only the wave end clears them.
    // Picking it back up has to restore the readout, or it shows zero until the next kill.
    private void OnEquipped(EntityUid uid, FSBattleTranceComponent battleTrance, GotEquippedHandEvent args)
    {
        battleTrance.Shooter = args.User;
        if (battleTrance.Stacks <= 0)
            return;
        BroadcastState(battleTrance);
    }

    private void OnUnequipped(EntityUid uid, FSBattleTranceComponent battleTrance, GotUnequippedHandEvent args)
    {
        if (battleTrance.Stacks <= 0)
            return;
        BroadcastZero(battleTrance);
    }

    private void OnWaveEnd(ref WaveEndedEvent args)
    {
        var query = EntityQueryEnumerator<FSBattleTranceComponent, FSWeaponUpgradeStateComponent>();
        while (query.MoveNext(out _, out var battleTrance, out var state))
        {
            state.DamageMultiplier -= battleTrance.CurrentBonus;
            battleTrance.Stacks = 0;
            battleTrance.CurrentBonus = 0f;
            BroadcastZero(battleTrance);
        }
    }

    private void BroadcastState(FSBattleTranceComponent comp)
        => BroadcastToShooter(comp, comp.Stacks, (int)MathF.Round(comp.CurrentBonus * 100f));

    private void BroadcastZero(FSBattleTranceComponent comp)
        => BroadcastToShooter(comp, 0, 0);

    private void BroadcastToShooter(FSBattleTranceComponent comp, int stacks, int bonusPct)
    {
        if (comp.Shooter is not { } shooter || !Exists(shooter))
            return;
        if (!_mind.TryGetMind(shooter, out _, out var mind) || mind?.UserId == null)
            return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        RaiseNetworkEvent(
            new FSBattleTranceStateEvent(stacks, comp.MaxStacks, bonusPct),
            Filter.SinglePlayer(session));
    }
}
