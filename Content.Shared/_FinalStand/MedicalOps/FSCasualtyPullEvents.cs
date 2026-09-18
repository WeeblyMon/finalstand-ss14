using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSCasualtyPullActionEvent : InstantActionEvent
{
}

[Serializable, NetSerializable]
public sealed class FSCasualtyPullRequestEvent : EntityEventArgs
{
    public NetEntity Patient;

    public FSCasualtyPullRequestEvent(NetEntity patient)
    {
        Patient = patient;
    }
}
