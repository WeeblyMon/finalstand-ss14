using Robust.Shared.Serialization;
using Content.Shared.Actions;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSCasualtyPullActionEvent : EntityTargetActionEvent
{
}

/// <summary>Picked from the recovery panel instead of the click-a-target action.</summary>
[Serializable, NetSerializable]
public sealed class FSCasualtyPullRequestEvent : EntityEventArgs
{
    public NetEntity Patient;

    public FSCasualtyPullRequestEvent(NetEntity patient)
    {
        Patient = patient;
    }
}
