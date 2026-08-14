using Content.Server._FinalStand.Perks;
using Content.Server.Body.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Upgrades.Effects;

// FireResist (scale incoming Heat damage) + WhileBurningBuff HoT tick. Damage-boost is in FSMeleeUpgradeRuntimeSystem.
public sealed class MeleeFireResistUpgradeSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const float BurningBuffHealPerSecond = 1f;

    private TimeSpan _nextTick = TimeSpan.Zero;
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HandsComponent, DamageModifyEvent>(OnWielderDamageModify);
        SubscribeLocalEvent<HandsComponent, BeforeHeatExchangeEvent>(OnWielderHeatExchange);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextTick)
            return;
        _nextTick = _timing.CurTime + TickInterval;

        var burnQuery = EntityQueryEnumerator<HandsComponent, FlammableComponent>();
        while (burnQuery.MoveNext(out var uid, out var hands, out var flammable))
        {
            if (!flammable.OnFire)
                continue;

            if (TryGetHeldWielderBuff(uid, hands, out _))
                _damageable.HealEvenly(uid, FixedPoint2.New(-BurningBuffHealPerSecond));
        }

        // Actively clamp body temperature for fire-immune players — catches any path that
        // bypasses ModifyChangedTemperatureEvent (e.g. ignoreHeatResistance: true callers).
        var tempQuery = EntityQueryEnumerator<HandsComponent, TemperatureComponent>();
        while (tempQuery.MoveNext(out var tUid, out var tHands, out var temp))
        {
            var fireImmune = false;
            foreach (var held in _hands.EnumerateHeld((tUid, tHands)))
            {
                if (TryComp<FSWeaponUpgradeStateComponent>(held, out var state) && state.FireDamageResist >= 1f)
                {
                    fireImmune = true;
                    break;
                }
            }

            if (!fireImmune)
                continue;

            var normalTemp = TryComp<ThermalRegulatorComponent>(tUid, out var regulator)
                ? regulator.NormalBodyTemperature
                : Atmospherics.T20C;

            if (temp.Temperature > normalTemp)
                _temperature.ChangeHeat((tUid, temp), (normalTemp - temp.Temperature) * temp.HeatCapacity, ignoreHeatResistance: true);
        }
    }

    private void OnWielderHeatExchange(Entity<HandsComponent> ent, ref BeforeHeatExchangeEvent args)
    {
        foreach (var held in _hands.EnumerateHeld((ent.Owner, ent.Comp)))
        {
            if (TryComp<FSWeaponUpgradeStateComponent>(held, out var state) && state.FireDamageResist >= 1f)
            {
                args.HeatTransferModifier = 0f;
                return;
            }
        }
    }

    private void OnWielderDamageModify(EntityUid uid, HandsComponent hands, DamageModifyEvent args)
    {
        // single directed (HandsComponent, DamageModifyEvent) subscription — Robust forbids two on same pair globally
        var bestFireResist = 0f;
        var bestWielderResist = 0f;
        foreach (var held in _hands.EnumerateHeld((uid, hands)))
        {
            if (TryComp<FSWeaponUpgradeStateComponent>(held, out var state))
            {
                if (state.FireDamageResist > bestFireResist)
                    bestFireResist = state.FireDamageResist;
                if (state.WielderResistance > bestWielderResist)
                    bestWielderResist = state.WielderResistance;
            }
        }

        if (bestWielderResist > 0f)
            args.Damage *= (1f - bestWielderResist);

        if (bestFireResist > 0f
            && args.Damage.DamageDict.TryGetValue("Heat", out var heat))
        {
            args.Damage.DamageDict["Heat"] = heat * FixedPoint2.New(1f - bestFireResist);
        }

        // Broadcast rather than calling the perk system directly: this stays the correct point
        // in the pipeline (after weapon resists), but the upgrades module no longer has to know
        // perks exist. Robust allows one directed subscriber per (component, event) pair and this
        // system owns (HandsComponent, DamageModifyEvent), so a relay is the way in.
        var perkEv = new FSIncomingDamageModifyEvent(uid, args);
        RaiseLocalEvent(ref perkEv);
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
