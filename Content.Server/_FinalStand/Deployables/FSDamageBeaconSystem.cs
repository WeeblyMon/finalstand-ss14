using System.Numerics;
using Content.Server._FinalStand.Mobs;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Mobs;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Deployables;

public sealed class FSDamageBeaconSystem : FSDeployableAuraSystem<FSDamageBeaconComponent>
{
    [Dependency] private FSDamageVulnerabilitySystem _vulnerability = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDamageBeaconComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FSDamageBeaconComponent, EntityTerminatingEvent>(OnTerminating);
    }

    private void OnMapInit(EntityUid uid, FSDamageBeaconComponent comp, MapInitEvent args)
    {
        if (comp.FieldVfxProtoId is { } vfxProto)
            Spawn(vfxProto, new EntityCoordinates(uid, Vector2.Zero));
    }

    private void OnTerminating(EntityUid uid, FSDamageBeaconComponent comp, ref EntityTerminatingEvent args)
    {
        if (comp.DestroyVfxProtoId is not { } destroyProto)
            return;

        var xform = Transform(uid);
        if (xform.MapUid is not { } map || TerminatingOrDeleted(map))
            return;

        Spawn(destroyProto, xform.Coordinates.ToMap(EntityManager, XformSystem));
    }

    protected override float GetRadius(FSDamageBeaconComponent aura) => aura.Radius;

    protected override void ApplyTo(EntityUid target, EntityUid source, FSDamageBeaconComponent aura)
    {
        _vulnerability.Apply(target, RefreshDuration);
        EnsureComp<FSDamageVulnerabilityComponent>(target).DamageMultiplier = aura.DamageMultiplier;
    }
}
