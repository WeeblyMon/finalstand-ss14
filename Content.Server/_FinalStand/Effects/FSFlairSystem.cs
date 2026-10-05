// Sends one-off visual moments to the players who can see them, and leaves rubble where a fortification is destroyed.
using Content.Shared._FinalStand.Effects;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Destructible;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Effects;

public sealed partial class FSFlairSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;

    private static readonly EntProtoId Rubble = "FSRubble";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSStructureHealthComponent, DestructionEventArgs>(OnDestroyed);
    }

    public void Burst(string emitter, EntityCoordinates coords, Color? tint = null, float delay = 0f, float count = 1f, float kick = 0f)
    {
        if (!coords.IsValid(EntityManager))
            return;

        RaiseNetworkEvent(new FSFlairBurstEvent(emitter, GetNetCoordinates(coords), tint, delay, count, kick),
            Filter.Pvs(coords, entityMan: EntityManager));
    }

    public void Burst(string emitter, EntityUid at, Color? tint = null, float delay = 0f, float count = 1f, float kick = 0f)
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
        Burst("FSDebris", coords, kick: 0.4f);
        Burst("FSDustPuff", coords);
        Spawn(Rubble, coords);
    }
}
