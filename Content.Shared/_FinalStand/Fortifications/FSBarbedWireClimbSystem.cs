using Content.Shared.Climbing.Events;
using Content.Shared.Examine;
using Content.Shared.Popups;

namespace Content.Shared._FinalStand.Fortifications;

public sealed partial class FSBarbedWireClimbSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSBarbedWireComponent, AttemptClimbEvent>(OnAttemptClimb);
        SubscribeLocalEvent<FSBarbedWireComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<FSBarbedWireComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("fs-barbed-wire-examine"));
    }

    private void OnAttemptClimb(Entity<FSBarbedWireComponent> ent, ref AttemptClimbEvent args)
    {
        args.Cancelled = true;
        _popup.PopupClient(Loc.GetString("fs-barbed-wire-no-vault"), ent, args.User);
    }
}
