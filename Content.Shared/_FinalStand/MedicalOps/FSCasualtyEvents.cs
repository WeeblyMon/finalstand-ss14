// The casualty board: outstanding calls for a medic, and who is on the way.

using Content.Shared.Actions;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSCasualtyBoardActionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public enum FSCasualtyState : byte
{
    Hurt,
    Critical,
    Dead,
}

[Serializable, NetSerializable]
public struct FSCasualtyEntry
{
    public NetEntity Patient;
    public string Name;
    public FSCasualtyState State;
    public NetCoordinates Position;
    public string? Responder;
}

// Pushed to medical players only. The board has to show casualties outside PVS, so it is a snapshot
// rather than a networked component.
[Serializable, NetSerializable]
public sealed class FSCasualtyBoardEvent : EntityEventArgs
{
    public List<FSCasualtyEntry> Entries;

    // Set only on the reply to the action, so a routine refresh never forces the window up.
    public bool Open;

    public FSCasualtyBoardEvent(List<FSCasualtyEntry> entries, bool open = false)
    {
        Entries = entries;
        Open = open;
    }
}

[Serializable, NetSerializable]
public sealed class FSRespondToCasualtyEvent : EntityEventArgs
{
    public NetEntity Patient;

    public FSRespondToCasualtyEvent(NetEntity patient)
    {
        Patient = patient;
    }
}
