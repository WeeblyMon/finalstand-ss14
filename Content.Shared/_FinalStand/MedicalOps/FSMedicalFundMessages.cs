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
public sealed class FSMedicalContributionEvent : EntityEventArgs
{
    public int Contributed;
    public int LifetimeEarned;

    public FSMedicalContributionEvent(int contributed, int lifetimeEarned)
    {
        Contributed = contributed;
        LifetimeEarned = lifetimeEarned;
    }
}

[Serializable, NetSerializable]
public sealed class FSHealPayoutEvent : EntityEventArgs
{
    public int Credits;
    public bool Diminished;

    public FSHealPayoutEvent(int credits, bool diminished)
    {
        Credits = credits;
        Diminished = diminished;
    }
}

[Serializable, NetSerializable]
public sealed class FSMedicalStatusEvent : EntityEventArgs
{
    public bool IsMedical;
    public bool IsChemist;
    public bool ShowPips;
    public float SoftCap;

    public FSMedicalStatusEvent(bool isMedical, bool isChemist, bool showPips, float softCap)
    {
        IsMedical = isMedical;
        IsChemist = isChemist;
        ShowPips = showPips;
        SoftCap = softCap;
    }
}

[Serializable, NetSerializable]
public sealed class FSOpenChemistGuideEvent : EntityEventArgs
{
}
