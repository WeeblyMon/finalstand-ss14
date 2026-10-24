namespace Content.Server._FinalStand.Effects;

// Raised broadcast when a structure with FSStructureHealthComponent is destroyed, after its debris spawns.
[ByRefEvent]
public readonly record struct FSStructureDestroyedEvent(EntityUid Structure);
