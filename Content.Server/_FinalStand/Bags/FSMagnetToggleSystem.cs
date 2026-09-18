using Content.Shared._FinalStand.Bags;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Verbs;

namespace Content.Server._FinalStand.Bags;

public sealed class FSMagnetToggleSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMagnetToggleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<FSMagnetToggleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnMapInit(Entity<FSMagnetToggleComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Captured || !TryComp<MagnetPickupComponent>(ent, out var magnet))
            return;

        ent.Comp.Range = magnet.Range;
        ent.Comp.SlotFlags = magnet.SlotFlags;
        ent.Comp.RequireActiveHand = magnet.RequireActiveHand;
        ent.Comp.Captured = true;

        if (!ent.Comp.Enabled)
            RemComp<MagnetPickupComponent>(ent);
    }

    private void OnGetVerbs(Entity<FSMagnetToggleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Enabled ? "fs-magnet-toggle-off" : "fs-magnet-toggle-on"),
            Act = () => Toggle(ent, user),
        });
    }

    private void Toggle(Entity<FSMagnetToggleComponent> ent, EntityUid user)
    {
        ent.Comp.Enabled = !ent.Comp.Enabled;
        Dirty(ent);

        if (ent.Comp.Enabled)
        {
            var magnet = EnsureComp<MagnetPickupComponent>(ent);
            magnet.Range = ent.Comp.Range;
            magnet.SlotFlags = ent.Comp.SlotFlags;
            magnet.RequireActiveHand = ent.Comp.RequireActiveHand;
            Dirty(ent.Owner, magnet);
        }
        else
        {
            RemComp<MagnetPickupComponent>(ent);
        }

        _popup.PopupEntity(
            Loc.GetString(ent.Comp.Enabled ? "fs-magnet-enabled" : "fs-magnet-disabled"), ent, user);
    }
}
