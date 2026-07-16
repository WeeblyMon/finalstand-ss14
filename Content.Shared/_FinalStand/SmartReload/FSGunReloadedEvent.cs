namespace Content.Shared._FinalStand.SmartReload;

/// <summary>
/// Broadcast after a gun successfully completes a reload action (mag swap or shell insert).
/// </summary>
[ByRefEvent]
public record struct FSGunReloadedEvent(EntityUid Gun, EntityUid User);
