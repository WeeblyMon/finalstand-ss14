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
    public bool IsChemist;

    public FSMedicalStatusEvent(bool isMedical, bool isChemist = false)
    {
        IsMedical = isMedical;
        IsChemist = isChemist;
    }
}
