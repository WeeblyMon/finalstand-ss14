using Content.Shared._FinalStand.Shop;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

// FireResist (scale incoming Heat damage) + WhileBurningBuff HoT tick. Damage-boost is in FSMeleeUpgradeRuntimeSystem.
public sealed class MeleeFireResistUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float BurningBuffHealPerSecond = 1f;

    private TimeSpan _nextTick = TimeSpan.Zero;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HandsComponent, DamageModifyEvent>(OnWielderDamageModify);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextTick)
            return;
        _nextTick = _timing.CurTime + TickInterval;

        var query = EntityQueryEnumerator<HandsComponent, FlammableComponent>();
        while (query.MoveNext(out var uid, out var hands, out var flammable))
        {
            if (!flammable.OnFire)
                continue;

            if (TryGetHeldWielderBuff(uid, hands, out _))
                _damageable.HealEvenly(uid, FixedPoint2.New(-BurningBuffHealPerSecond));
        }
    }

    private void OnWielderDamageModify(EntityUid uid, HandsComponent hands, DamageModifyEvent args)
    {
        // single directed (HandsComponent, DamageModifyEvent) subscription — Robust forbids two on same pair
        var bestFireResist = 0f;
        var bestWielderResist = 0f;
        foreach (var held in _hands.EnumerateHeld((uid, hands)))
        {
            if (!TryComp<FSWeaponUpgradeStateComponent>(held, out var state))
                continue;
            if (state.FireDamageResist > bestFireResist)
                bestFireResist = state.FireDamageResist;
            if (state.WielderResistance > bestWielderResist)
                bestWielderResist = state.WielderResistance;
        }

        if (bestWielderResist > 0f)
            args.Damage *= (1f - bestWielderResist);

        if (bestFireResist > 0f
            && args.Damage.DamageDict.TryGetValue("Heat", out var heat))
        {
            args.Damage.DamageDict["Heat"] = heat * FixedPoint2.New(1f - bestFireResist);
        }
    }

    private bool TryGetHeldWielderBuff(EntityUid uid, HandsComponent hands, out FSWeaponUpgradeStateComponent? state)
    {
        foreach (var held in _hands.EnumerateHeld((uid, hands)))
        {
            if (TryComp<FSWeaponUpgradeStateComponent>(held, out var s) && s.WhileBurningBuff)
            {
                state = s;
                return true;
            }
        }
        state = null;
        return false;
    }
}
