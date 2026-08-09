using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.SmartReload;

// Present on a gun while a single-load chain is running. Networked so the client blocks the
// shot during prediction instead of firing and being corrected.
[RegisterComponent, NetworkedComponent]
public sealed partial class FSReloadingComponent : Component;
