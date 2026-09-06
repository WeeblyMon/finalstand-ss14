using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.MedicalOps;

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

[Serializable, NetSerializable]
public sealed class FSMedicalStatusEvent : EntityEventArgs
{
    public bool IsMedical;

    public FSMedicalStatusEvent(bool isMedical)
    {
        IsMedical = isMedical;
    }
}
