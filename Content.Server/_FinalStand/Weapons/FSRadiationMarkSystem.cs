using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

public sealed partial class FSRadiationMarkSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const float MarkDurationSeconds = 3f;

    private EntityQuery<MobStateComponent> _mobQuery;

    public override void Initialize()
    {
        base.Initialize();
        _mobQuery = GetEntityQuery<MobStateComponent>();
        SubscribeLocalEvent<FSXrayRaycastComponent, HitscanRaycastFiredEvent>(OnXRayHit);
        // DamageModifyEvent is a class event (EntityEventArgs) — no ref keyword.
        SubscribeLocalEvent<FSRadiationMarkComponent, DamageModifyEvent>(OnDamageModify);
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<FSRadiationMarkComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (curTime >= comp.ExpiresAt)
            {
                // Deferred: removing a component while its own query enumerates is not safe.
                RemCompDeferred<FSRadiationMarkComponent>(uid);
                continue;
            }

            if (!comp.HasDot || comp.DotRemaining <= 0f)
                continue;
            if (!_mobQuery.TryGetComponent(uid, out var ms) || ms.CurrentState != MobState.Alive)
                continue;

            var tick = Math.Min(comp.DotPerSecond * frameTime, comp.DotRemaining);
            comp.DotRemaining -= tick;
            var dot = new DamageSpecifier();
            dot.DamageDict.Add("Radiation", FixedPoint2.New(tick));
            _damageable.TryChangeDamage(uid, dot, ignoreResistances: true);
        }
    }

    private void OnXRayHit(EntityUid uid, FSXrayRaycastComponent _, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity == null)
            return;

        var target = args.Data.HitEntity.Value;

        var coatingLevel = 0;
        var coatingResearchBonus = 0f;
        if (TryComp<FSWeaponUpgradeStateComponent>(args.Data.Gun, out var state))
        {
            coatingLevel = state.RadiationCoatingLevel;
            coatingResearchBonus = state.RadiationCoatingResearchBonus;
        }

        var duration = MarkDurationSeconds + coatingLevel;
        var mark = EnsureComp<FSRadiationMarkComponent>(target);
        mark.DamageMultiplier = 1.5f;
        mark.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(duration);

        if (coatingLevel > 0)
        {
            mark.HasDot = true;
            mark.DotPerSecond = 8f * coatingLevel * (1f + coatingResearchBonus);
            mark.DotRemaining = (1.5f + coatingLevel) * mark.DotPerSecond;
        }
    }

    private void OnDamageModify(EntityUid uid, FSRadiationMarkComponent comp, DamageModifyEvent args)
    {
        if (_timing.CurTime >= comp.ExpiresAt)
            return;
        args.Damage *= comp.DamageMultiplier;
    }
}
