using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Perks;

[RegisterComponent, NetworkedComponent]
public sealed partial class FSMedievalistComponent : Component
{
    public TimeSpan NextPopup;
}

public sealed partial class FSMedievalistSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMedievalistComponent, ShotAttemptedEvent>(OnShotAttempted);
    }

    private void OnShotAttempted(Entity<FSMedievalistComponent> ent, ref ShotAttemptedEvent args)
    {
        if (HasComp<FSRivetGunComponent>(args.Used) || HasComp<FSMediGunComponent>(args.Used))
            return;

        args.Cancel();
        if (_timing.CurTime < ent.Comp.NextPopup)
            return;

        ent.Comp.NextPopup = _timing.CurTime + TimeSpan.FromSeconds(2);
        _popup.PopupClient(Loc.GetString("fs-medievalist-no-guns"), ent, ent);
    }
}
