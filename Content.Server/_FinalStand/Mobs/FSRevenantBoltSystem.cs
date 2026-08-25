using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSRevenantBoltSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string HitEffect = "FSRevenantHitEffect";
    private const float FlashInterval = 0.2f;

    private readonly HashSet<Entity<FSFriendlyFireComponent>> _hitBuffer = new();
    private readonly List<EntityUid> _staleFlashes = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSRevenantBoltComponent, StartCollideEvent>(OnStartCollide);
    }

    private void OnStartCollide(EntityUid uid, FSRevenantBoltComponent comp, ref StartCollideEvent args)
    {
        QueueDel(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var query = EntityQueryEnumerator<FSRevenantBoltComponent>();
        while (query.MoveNext(out var uid, out var bolt))
        {
            bolt.PollAccum += frameTime;
            if (bolt.PollAccum < bolt.PollInterval)
                continue;
            bolt.PollAccum = 0f;

            var boltPos = _transform.GetMapCoordinates(uid);
            if (boltPos.MapId == MapId.Nullspace)
                continue;

            _hitBuffer.Clear();
            _lookup.GetEntitiesInRange<FSFriendlyFireComponent>(boltPos, bolt.HitRadius, _hitBuffer);

            foreach (var (targetUid, _) in _hitBuffer)
            {
                if (!TryComp<MobStateComponent>(targetUid, out var ms) || ms.CurrentState != MobState.Alive)
                    continue;

                if (bolt.LastHitTimes.TryGetValue(targetUid, out var lastHit)
                    && (now - lastHit).TotalSeconds < bolt.HitCooldown)
                    continue;

                bolt.LastHitTimes[targetUid] = now;

                var pierced = bolt.Damage * Math.Clamp(bolt.ResistanceBypass, 0f, 1f);
                ApplySlash(targetUid, bolt.Damage - pierced, false, bolt.Shooter);
                ApplySlash(targetUid, pierced, true, bolt.Shooter);

                var flash = EnsureComp<FSRevenantHitFlashComponent>(targetUid);
                if ((now - flash.LastFlash).TotalSeconds < FlashInterval)
                    continue;

                flash.LastFlash = now;

                var targetPos = _transform.GetMapCoordinates(targetUid);
                if (targetPos.MapId != MapId.Nullspace)
                    Spawn(HitEffect, targetPos);
            }
        }

        SweepStaleFlashes(now);
    }

    private void ApplySlash(EntityUid target, float amount, bool ignoreResistances, EntityUid origin)
    {
        if (amount <= 0f)
            return;

        var dmg = new DamageSpecifier();
        dmg.DamageDict["Slash"] = FixedPoint2.New(amount);
        _damageable.TryChangeDamage(target, dmg, ignoreResistances: ignoreResistances, origin: origin);
    }

    private void SweepStaleFlashes(TimeSpan now)
    {
        _staleFlashes.Clear();

        var query = EntityQueryEnumerator<FSRevenantHitFlashComponent>();
        while (query.MoveNext(out var uid, out var flash))
        {
            if ((now - flash.LastFlash).TotalSeconds >= FlashInterval * 2f)
                _staleFlashes.Add(uid);
        }

        foreach (var uid in _staleFlashes)
            RemComp<FSRevenantHitFlashComponent>(uid);
    }
}
