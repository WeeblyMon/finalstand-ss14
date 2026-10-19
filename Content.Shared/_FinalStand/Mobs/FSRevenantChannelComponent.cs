using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Mobs;

[RegisterComponent, NetworkedComponent]
[Access(typeof(FSRevenantChannelSystem))]
public sealed partial class FSRevenantChannelComponent : Component;
