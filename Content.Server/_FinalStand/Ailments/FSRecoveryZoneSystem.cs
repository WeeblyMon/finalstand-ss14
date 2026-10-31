using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.Deployables;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Ailments;

public sealed partial class FSRecoveryZoneSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private FSVitalsSystem _vitals = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(1);

    private readonly Dictionary<EntityUid, Entity<FSRecoveryZoneComponent>> _inZone = new();
    private readonly HashSet<Entity<FSAilmentStateComponent>> _nearby = new();
    private readonly List<Entity<FSAilmentStateComponent, DamageableComponent>> _patients = new();
    private TimeSpan _nextTick;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        _nextTick = now + TickInterval;

        _inZone.Clear();
        var zones = EntityQueryEnumerator<FSRecoveryZoneComponent, TransformComponent>();
        while (zones.MoveNext(out var uid, out var zone, out var xform))
        {
            if (TryComp<FSArmingDelayComponent>(uid, out var arming) && !arming.Armed)
                continue;

            _nearby.Clear();
            _lookup.GetEntitiesInRange(xform.Coordinates.Offset(zone.Offset), zone.Radius, _nearby);
            foreach (var patient in _nearby)
            {
                _inZone.TryAdd(patient, (uid, zone));
            }
        }

        _patients.Clear();
        var query = EntityQueryEnumerator<FSAilmentStateComponent, DamageableComponent>();
        while (query.MoveNext(out var uid, out var state, out var damageable))
        {
            _patients.Add((uid, state, damageable));
        }

        foreach (var patient in _patients)
        {
            if (TerminatingOrDeleted(patient))
                continue;

            if (_inZone.TryGetValue(patient, out var zone) && !_mobState.IsDead(patient))
                Recover(patient, zone, now);
            else
                _ailments.SetRecovering(patient, null);
        }
    }

    private void Recover(Entity<FSAilmentStateComponent, DamageableComponent> patient, Entity<FSRecoveryZoneComponent> zone, TimeSpan now)
    {
        var since = patient.Comp1.RecoveringSince ?? now;
        _ailments.SetRecovering(patient, since);

        var deployer = CompOrNull<FSDeployedByComponent>(zone)?.DeployedBy;
        var fraction = (float) TickInterval.TotalSeconds / zone.Comp.CureSeconds;
        foreach (var slot in FSAilmentSystem.Slots)
        {
            _ailments.AddMend(patient, slot, fraction, deployer);
        }

        var quiet = patient.Comp1.LastDamage > since ? patient.Comp1.LastDamage : since;
        if (!_mobState.IsAlive(patient)
            || now - quiet < zone.Comp.RegenDelay
            || _ailments.Has(patient, FSAilmentSlot.Burn))
            return;

        _vitals.Regen(patient, zone.Comp.RegenPerSecond * (float) TickInterval.TotalSeconds, zone.Comp.RegenCap);
    }
}
