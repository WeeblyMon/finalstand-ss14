using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Interaction;
using Content.Shared.Medical;
using Content.Shared.Popups;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMedicalOnlySystem : EntitySystem
{
    [Dependency] private FSMedicalRosterSystem _roster = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMedicalOnlyComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(FSMediGunSystem), typeof(SharedDefibrillatorSystem)]);
    }

    private void OnAfterInteract(EntityUid uid, FSMedicalOnlyComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || IsMedical(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("fs-medical-only"), uid, args.User, PopupType.Medium);
    }

    public bool IsMedical(EntityUid user) => _roster.IsMedical(user);
}
