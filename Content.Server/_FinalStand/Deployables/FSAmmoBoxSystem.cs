using Content.Server._FinalStand.Ammo;
using Content.Shared._FinalStand.Deployables;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Server._FinalStand.Deployables;

// deployed ammo crate: a shared pool of uses, and its owner can lock it to themselves
public sealed partial class FSAmmoBoxSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private WaveAmmoBoxSystem _waveAmmo = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAmmoBoxComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<FSAmmoBoxComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<FSAmmoBoxComponent, FSAmmoBoxRefillDoAfterEvent>(OnRefillDoAfter);
        SubscribeLocalEvent<FSAmmoBoxComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FSAmmoBoxComponent, FSDeployableDeployedEvent>(OnDeployed);
    }

    private void OnDeployed(Entity<FSAmmoBoxComponent> ent, ref FSDeployableDeployedEvent args)
    {
        ent.Comp.OwnerPlayer = args.User;

        if (TryComp<FSAmmoBoxComponent>(args.Item, out var item))
        {
            ent.Comp.MaxUses = item.MaxUses;
            ent.Comp.RefillDuration = item.RefillDuration;
        }

        ent.Comp.UsesLeft = ent.Comp.MaxUses;
        Dirty(ent);
        _appearance.SetData(ent, FSAmmoBoxVisuals.Upgraded, ent.Comp.MaxUses > 2);
    }

    private void OnInteractHand(Entity<FSAmmoBoxComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled || !Transform(ent).Anchored)
            return;

        args.Handled = TryStartRefill(ent, args.User);
    }

    private void OnActivate(Entity<FSAmmoBoxComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !Transform(ent).Anchored)
            return;

        args.Handled = TryStartRefill(ent, args.User);
    }

    private bool TryStartRefill(Entity<FSAmmoBoxComponent> ent, EntityUid user)
    {
        if (ent.Comp.UsesLeft <= 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-ammo-box-spent"), ent, user);
            return false;
        }

        if (ent.Comp.Private && ent.Comp.OwnerPlayer is { } owner && owner != user)
        {
            _popup.PopupEntity(Loc.GetString("fs-ammo-box-private"), ent, user);
            return false;
        }

        return _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.RefillDuration,
            new FSAmmoBoxRefillDoAfterEvent(), ent, target: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        });
    }

    private void OnRefillDoAfter(Entity<FSAmmoBoxComponent> ent, ref FSAmmoBoxRefillDoAfterEvent args)
    {
        if (args.Cancelled || ent.Comp.UsesLeft <= 0)
            return;

        var user = args.Args.User;

        if (ent.Comp.Private && ent.Comp.OwnerPlayer is { } owner && owner != user)
            return;

        ent.Comp.UsesLeft--;
        Dirty(ent);

        _waveAmmo.RefillAllAmmo(user);
        _popup.PopupEntity(Loc.GetString("fs-ammo-box-used", ("left", ent.Comp.UsesLeft)), ent, user);

        if (ent.Comp.UsesLeft <= 0)
            QueueDel(ent);
    }

    private void OnGetVerbs(Entity<FSAmmoBoxComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !Transform(ent).Anchored)
            return;

        if (ent.Comp.OwnerPlayer is not { } owner || owner != args.User)
            return;

        var comp = ent.Comp;
        var uid = ent.Owner;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(comp.Private ? "fs-ammo-box-verb-public" : "fs-ammo-box-verb-private"),
            Act = () =>
            {
                comp.Private = !comp.Private;
                Dirty(uid, comp);
            },
        });
    }
}
