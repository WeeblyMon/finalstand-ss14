using Robust.Shared.GameObjects;

namespace Content.Shared._FinalStand.Zombies;

/// <summary>
/// Marks a wave enemy as weak enough to be instakilled by the Energy Magnum Vaporise upgrade.
/// Add only to Normal and Runner zombies. Any enemy without this tag is immune by default.
/// </summary>
[RegisterComponent]
public sealed partial class FSVaporiseWeakComponent : Component { }
