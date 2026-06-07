using Content.Server._FinalStand.Spawners;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Projectiles;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Systems;
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
        SubscribeLocalEvent<WaveSpawnedTagComponent, BeforeDamageChangedEvent>(OnWaveEntityBeforeDamage);
    }

    private void OnPreventCollide(EntityUid uid, FSFireProjectileComponent comp, ref PreventCollideEvent args)
    {
        if (HasComp<WaveSpawnedTagComponent>(args.OtherEntity))
            args.Cancelled = true;
    }

    private void OnWaveEntityBeforeDamage(EntityUid uid, WaveSpawnedTagComponent comp, ref BeforeDamageChangedEvent args)
    {
        if (args.Origin != null && HasComp<FSFireProjectileComponent>(args.Origin.Value))
            args.Cancelled = true;
    }

    private void OnCollide(EntityUid uid, FSFireProjectileComponent comp, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != SharedProjectileSystem.ProjectileFixture)
            return;
        if (!args.OtherFixture.Hard)
            return;

        var target = args.OtherEntity;

        if (!HasComp<WaveSpawnedTagComponent>(target) &&
            TryComp<FlammableComponent>(target, out var flammable))
        {
            flammable.FireStacks += 0.2f;
            _flammable.Ignite(target, uid, flammable);
        }
    }
}
