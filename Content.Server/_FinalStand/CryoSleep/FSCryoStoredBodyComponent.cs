using Robust.Shared.Network;

namespace Content.Server._FinalStand.CryoSleep;

// Marks a body parked in cryo. Lives on the body so it dies with it and shows up in VV.
[RegisterComponent]
public sealed partial class FSCryoStoredBodyComponent : Component
{
    [ViewVariables]
    public NetUserId User;

    [ViewVariables]
    public EntityUid Pod;
}
