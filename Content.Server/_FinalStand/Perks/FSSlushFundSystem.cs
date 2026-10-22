using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSSlushFundSystem : EntitySystem
{
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSMedicalFundSystem _medicalFund = default!;
    [Dependency] private FSDepartmentAccessSystem _departments = default!;

    public override void Initialize()
    {
        base.Initialize();
        _wallet.CreditsGiven += OnCreditsGiven;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _wallet.CreditsGiven -= OnCreditsGiven;
    }

    private void OnCreditsGiven(EntityUid mindId, int amount)
    {
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var perks)
            || perks.GetSlottedLevel("SlushFund") is var level && level <= 0
            || CompOrNull<MindComponent>(mindId)?.CurrentEntity is not { } body
            || !_departments.IsMedical(body))
            return;

        var slush = EnsureComp<FSSlushFundComponent>(mindId);
        slush.Remainder += amount * level * FSPerkBonusConstants.SlushFundPerLevel;
        var paid = (int) slush.Remainder;
        if (paid <= 0)
            return;

        slush.Remainder -= paid;
        _medicalFund.GrantMedicalFunds(paid, "slush-fund", mindId);
    }
}

[RegisterComponent]
public sealed partial class FSSlushFundComponent : Component
{
    public float Remainder;
}
