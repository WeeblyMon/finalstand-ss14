using Content.Shared._FinalStand.Weapons;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

public sealed class FSMarksmansRhythmSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMarksmansRhythmComponent, AmmoShotEvent>(OnShot);
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnProjectileHit);
    }

    private void OnShot(EntityUid uid, FSMarksmansRhythmComponent comp, AmmoShotEvent args)
    {
        comp.LastShotTime = _timing.CurTime;
    }

    private void OnProjectileHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (!TryComp<FSMarksmansRhythmComponent>(ev.Weapon.Value, out var comp))
            return;
        if (!HasComp<MobStateComponent>(ev.Target))
            return;

        if (ev.Shooter != null)
            comp.Shooter = ev.Shooter;

        var bonus = 1f + comp.DamagePerStack * comp.CurrentStacks;
        ev.AdditionalMultiplier *= bonus;

        comp.CurrentStacks = Math.Min(comp.CurrentStacks + 1, comp.MaxStacks);
        comp.LastHitTime = _timing.CurTime;
        BroadcastState(comp);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSMarksmansRhythmComponent>();
        while (query.MoveNext(out _, out var comp))
        {
            if (comp.CurrentStacks == 0)
                continue;

            var prevStacks = comp.CurrentStacks;

            if ((now - comp.LastHitTime).TotalSeconds > comp.DecaySeconds)
            {
                comp.CurrentStacks = 0;
            }
            else if (comp.LastShotTime > comp.LastHitTime &&
                     (now - comp.LastShotTime).TotalSeconds > comp.MissWindowSeconds)
            {
                comp.CurrentStacks = 0;
            }

            if (comp.CurrentStacks == 0 && prevStacks > 0)
                BroadcastZero(comp);
        }
    }

    private void BroadcastState(FSMarksmansRhythmComponent comp)
    {
        var bonusPct = (int)MathF.Round(comp.CurrentStacks * comp.DamagePerStack * 100f);
        BroadcastToShooter(comp, comp.CurrentStacks, bonusPct);
    }

    private void BroadcastZero(FSMarksmansRhythmComponent comp)
        => BroadcastToShooter(comp, 0, 0);

    private void BroadcastToShooter(FSMarksmansRhythmComponent comp, int stacks, int bonusPct)
    {
        if (comp.Shooter is not { } shooter || !Exists(shooter))
            return;
        if (!_mind.TryGetMind(shooter, out _, out var mind) || mind?.UserId == null)
            return;
        if (!_playerManager.TryGetSessionById(mind.UserId.Value, out var session))
            return;

        RaiseNetworkEvent(
            new FSMarksmansRhythmStateEvent(stacks, comp.MaxStacks, bonusPct),
            Filter.SinglePlayer(session));
    }
}
