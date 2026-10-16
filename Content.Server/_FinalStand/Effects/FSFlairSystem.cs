// Sends one-off visual moments to the players who can see them, and leaves rubble where a fortification is destroyed.
using Content.Shared._FinalStand.Effects;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Destructible;
using Content.Shared._FinalStand.Particles;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Effects;

[ByRefEvent]
public readonly record struct FSStructureDestroyedEvent(EntityUid Structure);

public sealed partial class FSFlairSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly ProtoId<FSParticleEmitterPrototype> Debris = "FSDebris";
    private static readonly ProtoId<FSParticleEmitterPrototype> DustPuff = "FSDustPuff";

    private static readonly EntProtoId Rubble = "FSRubble";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSStructureHealthComponent, DestructionEventArgs>(OnDestroyed);
    }

    public void Burst(ProtoId<FSParticleEmitterPrototype> emitter, EntityCoordinates coords, Color? tint = null, float delay = 0f, float count = 1f, float kick = 0f)
    {
        if (!coords.IsValid(EntityManager))
            return;

        RaiseNetworkEvent(new FSFlairBurstEvent(emitter.Id, GetNetCoordinates(coords), tint, delay, count, kick),
            Filter.Pvs(coords, entityMan: EntityManager));
    }

    public void Burst(ProtoId<FSParticleEmitterPrototype> emitter, EntityUid at, Color? tint = null, float delay = 0f, float count = 1f, float kick = 0f)
    {
        Burst(emitter, Transform(at).Coordinates, tint, delay, count, kick);
    }

    public void Hologram(EntityCoordinates coords, string piece, float duration, Angle rotation)
    {
        RaiseNetworkEvent(new FSBuildHologramEvent(GetNetCoordinates(coords), piece, duration, rotation),
            Filter.Pvs(coords, entityMan: EntityManager));
    }

    private void OnDestroyed(Entity<FSStructureHealthComponent> ent, ref DestructionEventArgs args)
    {
        var coords = _transform.GetMoverCoordinates(ent);
        Burst(Debris, coords, kick: 0.4f);
        Burst(DustPuff, coords);
        Spawn(Rubble, coords);

        var destroyed = new FSStructureDestroyedEvent(ent);
        RaiseLocalEvent(ref destroyed);
    }
}
