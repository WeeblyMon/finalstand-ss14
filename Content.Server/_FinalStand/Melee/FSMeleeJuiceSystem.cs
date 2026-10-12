using System.Numerics;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Shared._FinalStand.Melee;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Mobs;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.Audio;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Melee;

public sealed partial class FSMeleeJuiceSystem : SharedFSMeleeJuiceSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private KnockbackUpgradeSystem _knockback = default!;
    [Dependency] private MovementModStatusSystem _movementStatus = default!;

    private readonly record struct PendingStrike(EntityUid User, EntityUid Weapon, List<EntityUid> Targets, DamageSpecifier Damage, Vector2 Direction, bool Heavy, int Strike, TimeSpan Due);

    private readonly List<PendingStrike> _strikes = new();
    private readonly Dictionary<(EntityUid User, EntityUid Target), (EntityUid Weapon, bool Heavy)> _hitsThisTick = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MobStateChangedEvent>(OnMobStateChanged);
    }

    protected override void OnHit(Entity<FSMeleeJuiceComponent> weapon, EntityUid user, List<EntityUid> targets, Vector2 direction, bool heavy, bool lethal, MeleeHitEvent args)
    {
        var profile = weapon.Comp.Get(heavy);

        SendHit(user, weapon, targets, direction, heavy, 0, lethal);
        ApplyImpact(user, weapon, targets, profile);

        foreach (var target in targets)
            _hitsThisTick[(user, target)] = (weapon, heavy);

        if (profile.ExtraStrikes <= 0)
            return;

        var damage = (args.BaseDamage + args.BonusDamage) * profile.StrikeDamageFraction;
        for (var i = 1; i <= profile.ExtraStrikes; i++)
        {
            var due = Timing.CurTime + TimeSpan.FromSeconds(profile.StrikeInterval * i);
            _strikes.Add(new PendingStrike(user, weapon, new List<EntityUid>(targets), damage, direction, heavy, i, due));
        }
    }

    private void SendHit(EntityUid user, EntityUid weapon, List<EntityUid> targets, Vector2 direction, bool heavy, int strike, bool lethal)
    {
        var filter = Filter.Pvs(user, entityManager: EntityManager);
        if (strike == 0)
            filter.RemoveWhereAttachedEntity(e => e == user);

        RaiseNetworkEvent(new FSMeleeHitJuiceEvent(GetNetEntity(user), GetNetEntity(weapon), GetNetEntityList(targets), direction, heavy, strike, lethal), filter);
    }

    private void ApplyImpact(EntityUid user, EntityUid weapon, List<EntityUid> targets, FSMeleeJuiceProfile profile)
    {
        var userPos = Xform.GetWorldPosition(user);
        var status = TryComp<ItemToggleComponent>(weapon, out var toggle) && !toggle.Activated ? null : profile.HitStatus;
        foreach (var target in targets)
        {
            if (TerminatingOrDeleted(target))
                continue;

            if (profile.Knockback > 0f)
                _knockback.Push(target, Xform.GetWorldPosition(target) - userPos, profile.Knockback, TimeSpan.FromSeconds(profile.KnockbackDuration));

            Flinch(target, profile.FlinchDuration, profile.FlinchSpeed);

            if (status != null)
                _movementStatus.TryUpdateMovementSpeedModDuration(target, status.Value, TimeSpan.FromSeconds(profile.HitStatusDuration), profile.HitStatusSpeed);
        }
    }

    private void OnMobStateChanged(MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || args.OldMobState == MobState.Dead || args.Origin is not { } user)
            return;

        if (!_hitsThisTick.TryGetValue((user, args.Target), out var hit)
            || !TryComp<FSMeleeJuiceComponent>(hit.Weapon, out var juice))
            return;

        var profile = juice.Get(hit.Heavy);
        _audio.PlayPvs(profile.KillSound, args.Target, profile.KillSound?.Params.WithVariation(0.08f));
        RaiseNetworkEvent(new FSMeleeKillJuiceEvent(GetNetEntity(user), GetNetEntity(hit.Weapon), GetNetEntity(args.Target), hit.Heavy),
            Filter.Pvs(args.Target, entityManager: EntityManager));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _hitsThisTick.Clear();

        var now = Timing.CurTime;
        for (var i = _strikes.Count - 1; i >= 0; i--)
        {
            var strike = _strikes[i];
            if (now < strike.Due)
                continue;

            _strikes.RemoveAt(i);
            RunStrike(strike);
        }
    }

    private void RunStrike(PendingStrike strike)
    {
        if (TerminatingOrDeleted(strike.User) || !TryComp<FSMeleeJuiceComponent>(strike.Weapon, out var juice))
            return;

        strike.Targets.RemoveAll(t => TerminatingOrDeleted(t));
        if (strike.Targets.Count == 0)
            return;

        foreach (var target in strike.Targets)
        {
            _hitsThisTick[(strike.User, target)] = (strike.Weapon, strike.Heavy);
            _damageable.ChangeDamage(target, strike.Damage, origin: strike.User);
        }

        var profile = juice.Get(strike.Heavy);
        _audio.PlayPvs(profile.StrikeSound, strike.Targets[0], profile.StrikeSound?.Params.WithPitchScale(1f + profile.StrikePitchStep * strike.Strike));
        SendHit(strike.User, strike.Weapon, strike.Targets, strike.Direction, strike.Heavy, strike.Strike, false);
        ApplyImpact(strike.User, strike.Weapon, strike.Targets, profile);
    }
}
