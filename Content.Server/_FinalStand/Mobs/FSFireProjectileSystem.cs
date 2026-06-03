using Content.Server._FinalStand.Spawners;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Projectiles;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Atmos.Components;
using Content.Shared.Physics;
using Content.Shared.Projectiles;
using Robust.Shared.Physics.Events;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSFireProjectileSystem : EntitySystem
{
    [Dependency] private readonly FlammableSystem _flammable = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSFireProjectileComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<FSFireProjectileComponent, StartCollideEvent>(OnCollide, after: [typeof(ProjectileSystem)]);
    }

    private void OnPreventCollide(EntityUid uid, FSFireProjectileComponent comp, ref PreventCollideEvent args)
    {
        if (HasComp<WaveSpawnedTagComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnCollide(EntityUid uid, FSFireProjectileComponent comp, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture)
            return;
        if (!args.OtherFixture.Hard)
            return;
        if (!TryComp<ProjectileComponent>(uid, out var proj))
            return;
        if (!proj.ProjectileSpent)
            return;

        var target = args.OtherEntity;

        if (!HasComp<WaveSpawnedTagComponent>(target) &&
            TryComp<FlammableComponent>(target, out var flammable))
        {
            flammable.FireStacks += 3f;
            _flammable.Ignite(target, uid, flammable);
        }

        proj.ProjectileSpent = false;
    }
}
