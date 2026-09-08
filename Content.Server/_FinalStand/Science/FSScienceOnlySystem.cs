using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Placement;
using Content.Shared._FinalStand.Science;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Server._FinalStand.Science;

// Restricts FSScienceOnlyComponent-tagged items to Science department members, mirrors FSRCDEngineerOnlySystem.
public sealed partial class FSScienceOnlySystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private FSDepartmentAccessSystem _departments = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSScienceOnlyComponent, AttemptShootEvent>(OnAttemptShoot);
        SubscribeLocalEvent<FSScienceOnlyComponent, UseInHandEvent>(OnUseInHand,
            before: [typeof(FSPlacementSystem)]);
    }

    private void OnAttemptShoot(EntityUid uid, FSScienceOnlyComponent comp, ref AttemptShootEvent args)
    {
        if (args.Cancelled || IsScience(args.User))
            return;

        args.Cancelled = true;
        args.Message = Loc.GetString("fs-science-only-use");
    }

    private void OnUseInHand(EntityUid uid, FSScienceOnlyComponent comp, UseInHandEvent args)
    {
        if (args.Handled || IsScience(args.User))
            return;

        args.Handled = true;
        _popup.PopupEntity(Loc.GetString("fs-science-only-use"), uid, args.User);
    }

    public bool IsScience(EntityUid user) => _departments.IsScience(user);
}
