// Giant zombie body-part removal: hide sprite layers and spawn floor debris as HP drops; gib on death.
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Visuals;

public sealed partial class FSGiantZombieVisualsSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private FlammableSystem _flammable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGiantZombieVisualsComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<FSGiantZombieVisualsComponent, MobStateChangedEvent>(OnDied);
    }

    private void OnDamageChanged(EntityUid uid, FSGiantZombieVisualsComponent comp, DamageChangedEvent args)
    {
        if (comp.Dead)
            return;

        if (!TryComp<MobThresholdsComponent>(uid, out var thresholds))
            return;

        var deathThreshold = 0f;
        foreach (var (threshold, state) in thresholds.Thresholds)
        {
            if (state == MobState.Dead)
                deathThreshold = threshold.Float();
        }

        if (deathThreshold <= 0f)
            return;

        var currentDamage = _damageable.GetTotalDamage((uid, args.Damageable)).Float();
        var healthPct = 1f - currentDamage / deathThreshold;

        var coords = Transform(uid).Coordinates;
        var dirty = false;

        if (!comp.RightArmRemoved && healthPct < 0.75f)
        {
            comp.RightArmRemoved = true;
            SpawnGib("FSGiantZombieArmFloor", coords, new Vector2(3f, 1f));
            dirty = true;
        }

        if (!comp.LeftArmRemoved && healthPct < 0.50f)
        {
            comp.LeftArmRemoved = true;
            SpawnGib("FSGiantZombieArmFloor", coords, new Vector2(-3f, 1f));
            dirty = true;
        }

        if (!comp.HeadRemoved && healthPct < 0.25f)
        {
            comp.HeadRemoved = true;
            SpawnGib("FSGiantZombieHeadFloor", coords, new Vector2(_random.NextFloat(-1f, 1f), 4f));
            dirty = true;
        }

        if (dirty)
            Dirty(uid, comp);
    }

    private void OnDied(EntityUid uid, FSGiantZombieVisualsComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || comp.Dead)
            return;

        comp.Dead = true;
        Dirty(uid, comp);

        if (TryComp<FlammableComponent>(uid, out var flammable))
            _flammable.Extinguish(uid, flammable);

        var coords = Transform(uid).Coordinates;

        SpawnGib("FSGiantZombieTorsoFloor", coords, new Vector2(_random.NextFloat(-0.5f, 0.5f), 2f));
        SpawnGib("FSGiantZombieLegFloor", coords, new Vector2(-2f, -1.5f));
        SpawnGib("FSGiantZombieLegFloor", coords, new Vector2(2f, -1.5f));

        if (!comp.RightArmRemoved)
            SpawnGib("FSGiantZombieArmFloor", coords, new Vector2(3f, 1f));
        if (!comp.LeftArmRemoved)
            SpawnGib("FSGiantZombieArmFloor", coords, new Vector2(-3f, 1f));
        if (!comp.HeadRemoved)
            SpawnGib("FSGiantZombieHeadFloor", coords, new Vector2(_random.NextFloat(-1f, 1f), 4f));
    }

    private void SpawnGib(string prototype, EntityCoordinates coords, Vector2 velocity)
    {
        var gib = Spawn(prototype, coords);
        if (TryComp<PhysicsComponent>(gib, out var physics))
        {
            _physics.SetLinearVelocity(gib, velocity, body: physics);
            _physics.SetAngularVelocity(gib, _random.NextFloat(-5f, 5f), body: physics);
        }
    }
}
