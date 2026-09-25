using Content.Server.Body.Components;
using Content.Server.Temperature.Systems;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Temperature.Components;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSManOnFireSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private TemperatureSystem _temperature = default!;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);
    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSManOnFireComponent, GetFireProtectionEvent>(OnGetFireProtection);
    }

    public static float GetResist(int level)
    {
        return level <= 0 ? 0f : FSPerkBonusConstants.ManOnFireResist[Math.Min(level, FSPerkDef.MaxLevel) - 1];
    }

    public bool IsHeatImmune(EntityUid uid)
    {
        return TryComp<FSManOnFireComponent>(uid, out var comp) && GetResist(comp.Level) >= 1f;
    }

    private void OnGetFireProtection(Entity<FSManOnFireComponent> ent, ref GetFireProtectionEvent args)
    {
        args.Reduce(MathF.Min(GetResist(ent.Comp.Level), 1f));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextTick)
            return;
        _nextTick = _timing.CurTime + TickInterval;

        var query = EntityQueryEnumerator<FSManOnFireComponent, FlammableComponent, TemperatureComponent>();
        while (query.MoveNext(out var uid, out var comp, out var flammable, out var temp))
        {
            var resist = GetResist(comp.Level);
            if (resist < 1f)
                continue;

            var normalTemp = TryComp<ThermalRegulatorComponent>(uid, out var regulator)
                ? regulator.NormalBodyTemperature
                : Atmospherics.T20C;

            // Burning also heats the body, which deals its own damage past the fire protection check.
            if (temp.Temperature > normalTemp)
                _temperature.ChangeHeat((uid, temp), (normalTemp - temp.Temperature) * temp.HeatCapacity, ignoreHeatResistance: true);

            if (resist > 1f && flammable.OnFire)
                _damageable.HealEvenly(uid, FixedPoint2.New(-FSPerkBonusConstants.ManOnFireHealPerSecond));
        }
    }
}
