using Robust.Shared.Map;

namespace Content.Shared._FinalStand.RCD;

// Raised on the RCD before its build do-after starts, so FS can change how long the build takes.
[ByRefEvent]
public record struct FSRCDDelayEvent(EntityUid User, EntityCoordinates Location, float Delay);
