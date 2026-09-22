using Content.Server._FinalStand.MedicalOps;
using Content.Server.Popups;
using Content.Shared._FinalStand.Deployables;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Deployables;

public sealed class FSPackableSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSMedicalOnlySystem _medical = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier PackSound =
        new SoundPathSpecifier("/Audio/Items/zip.ogg");
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSPackableComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<FSPackableComponent, InteractHandEvent>(OnInteractHand);
        SubscribeLocalEvent<FSPackableComponent, FSPackDoAfterEvent>(OnPacked);
        SubscribeLocalEvent<FSDeployableLifetimeComponent, FSDeployableDeployedEvent>(OnDeployed);
    }

    private void OnGetVerbs(Entity<FSPackableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("fs-packable-verb"),
            Act = () => TryStart(ent, user),
        });
    }

    private void OnInteractHand(Entity<FSPackableComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryStart(ent, args.User);
    }

    private bool TryStart(Entity<FSPackableComponent> ent, EntityUid user)
    {
        if (ent.Comp.MedicalOnly && !_medical.IsMedical(user))
        {
            _popup.PopupEntity(Loc.GetString("fs-medical-only-use"), user, user);
            return false;
        }

        if (TryComp<StrapComponent>(ent, out var strap) && strap.BuckledEntities.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("fs-packable-occupied"), user, user);
            return false;
        }

        var args = new DoAfterArgs(EntityManager, user, ent.Comp.PackTime, new FSPackDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (!_doAfter.TryStartDoAfter(args))
            return false;

        _popup.PopupEntity(Loc.GetString("fs-packable-started"), ent, user);
        _audio.PlayPvs(PackSound, ent);
        return true;
    }

    private void OnPacked(Entity<FSPackableComponent> ent, ref FSPackDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (TryComp<StrapComponent>(ent, out var strap) && strap.BuckledEntities.Count > 0)
            return;

        args.Handled = true;

        // Restore stock onto the item that was actually consumed to deploy this, instead of
        // spawning a fresh one - otherwise deploy+pack loops duplicate stock for free.
        EntityUid item;
        if (TryComp<FSDeployedByComponent>(ent.Owner, out var deployedBy)
            && deployedBy.SourceItem is { } sourceItem
            && Exists(sourceItem)
            && !TerminatingOrDeleted(sourceItem))
        {
            item = sourceItem;

            if (TryComp<FSDeployableItemComponent>(item, out var restored))
            {
                restored.Stock = Math.Min(restored.Stock + 1, restored.MaxStock);
                Dirty(item, restored);
            }
        }
        else
        {
            item = Spawn(ent.Comp.PackedProtoId, _transform.GetMapCoordinates(ent.Owner));

            if (TryComp<FSDeployableItemComponent>(item, out var deployable))
            {
                deployable.Stock = 1;
                Dirty(item, deployable);
            }
        }

        if (TryComp<FSDeployableLifetimeComponent>(ent.Owner, out var lifetime))
        {
            var remaining = lifetime.ExpiresAt - _timing.CurTime;
            EnsureComp<FSBankedLifetimeComponent>(item).Remaining =
                remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        _hands.TryPickupAnyHand(args.User, item);
        Del(ent.Owner);
    }

    private void OnDeployed(Entity<FSDeployableLifetimeComponent> ent, ref FSDeployableDeployedEvent args)
    {
        if (!TryComp<FSBankedLifetimeComponent>(args.Item, out var banked))
            return;

        ent.Comp.ExpiresAt = _timing.CurTime + banked.Remaining;
        Dirty(ent);
    }
}
