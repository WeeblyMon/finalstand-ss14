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

/// <summary>The viewer's own share of the pot, sent when they open the medical console.</summary>
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

/// <summary>What a heal just paid, and whether diminishing returns had started biting.</summary>
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

    public FSMedicalStatusEvent(bool isMedical, bool isChemist = false)
    {
        IsMedical = isMedical;
        IsChemist = isChemist;
    }
}
