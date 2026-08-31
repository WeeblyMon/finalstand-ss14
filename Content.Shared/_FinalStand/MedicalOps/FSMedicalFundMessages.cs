using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

// One integer, so it broadcasts rather than networking the component.
[Serializable, NetSerializable]
public sealed class FSMedicalFundUpdatedEvent : EntityEventArgs
{
    public int Balance;

    public FSMedicalFundUpdatedEvent(int balance)
    {
        Balance = balance;
    }
}

[Serializable, NetSerializable]
public sealed class FSMedicalFundRequestEvent : EntityEventArgs
{
}

// Whether this client should get the medic-grade health readout. Resolved server-side off the held
// ID, so the client never has to work out what counts as Medical.
[Serializable, NetSerializable]
public sealed class FSMedicalStatusEvent : EntityEventArgs
{
    public bool IsMedical;

    public FSMedicalStatusEvent(bool isMedical)
    {
        IsMedical = isMedical;
    }
}
