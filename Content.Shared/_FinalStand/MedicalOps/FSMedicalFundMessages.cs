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
