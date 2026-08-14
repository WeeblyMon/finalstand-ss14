using Content.Shared._FinalStand.Grenades;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Containers;

namespace Content.Server._FinalStand.Grenades;

public sealed partial class FSGrenadeSelectActionSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSGrenadePackComponent, EntGotInsertedIntoContainerMessage>(OnPackInserted);
        SubscribeLocalEvent<FSGrenadePackComponent, EntGotRemovedFromContainerMessage>(OnPackRemoved);
        SubscribeLocalEvent<FSGrenadePackComponent, ComponentShutdown>(OnPackShutdown);

        SubscribeLocalEvent<FSSelectFragGrenadeEvent>(OnSelectFrag);
        SubscribeLocalEvent<FSSelectIncendiaryGrenadeEvent>(OnSelectIncendiary);
        SubscribeLocalEvent<FSSelectFlashGrenadeEvent>(OnSelectFlash);
        SubscribeLocalEvent<FSSelectPipeGrenadeEvent>(OnSelectPipe);
    }

    private void OnPackInserted(EntityUid uid, FSGrenadePackComponent comp, EntGotInsertedIntoContainerMessage args)
    {
        if (comp.GrantedActionId != null || comp.SelectActionProtoId == null)
            return;

        var player = FindPlayerOwner(uid);
        if (player == null)
            return;

        EntityUid? actionId = null;
        _actions.AddAction(player.Value, ref actionId, comp.SelectActionProtoId.Value);
        comp.GrantedActionId = actionId;

        SyncPackCounter(uid, comp);

        var active = EnsureComp<FSActiveGrenadeComponent>(player.Value);
        active.ActiveType = comp.PackType;
        Dirty(player.Value, active);
    }

    private void OnPackRemoved(EntityUid uid, FSGrenadePackComponent comp, EntGotRemovedFromContainerMessage args)
    {
        if (comp.GrantedActionId == null)
            return;

        // Check if the pack is still within a player's containers (e.g., moved pocket→pocket).
        // Only revoke the action if it has truly left the player.
        if (FindPlayerOwner(uid) != null)
            return;

        _actions.RemoveAction(comp.GrantedActionId.Value);
        comp.GrantedActionId = null;
    }

    private void OnPackShutdown(EntityUid uid, FSGrenadePackComponent comp, ComponentShutdown args)
    {
        if (comp.GrantedActionId is { } actionId)
        {
            _actions.RemoveAction(actionId);
            comp.GrantedActionId = null;
        }
    }

    /// <summary>Writes current/max stock onto the action entity so ActionButton can display a badge.</summary>
    public void SyncPackCounter(EntityUid packUid, FSGrenadePackComponent pack)
    {
        if (pack.GrantedActionId is not { } actionId)
            return;

        var counter = EnsureComp<FSActionCounterComponent>(actionId);
        counter.Current = pack.Stock;
        counter.Max = pack.MaxStock;
        Dirty(actionId, counter);
    }

    private EntityUid? FindPlayerOwner(EntityUid item)
    {
        var current = item;
        for (var depth = 0; depth < 8; depth++)
        {
            if (!_containers.TryGetContainingContainer(current, out var container))
                return null;
            var parent = container.Owner;
            if (HasComp<ActionsComponent>(parent))
                return parent;
            current = parent;
        }
        return null;
    }

    private void OnSelectFrag(FSSelectFragGrenadeEvent args)
    {
        if (!args.Performer.IsValid())
            return;
        var active = EnsureComp<FSActiveGrenadeComponent>(args.Performer);
        active.ActiveType = GrenadeType.Frag;
        Dirty(args.Performer, active);
        args.Handled = true;
    }

    private void OnSelectIncendiary(FSSelectIncendiaryGrenadeEvent args)
    {
        if (!args.Performer.IsValid())
            return;
        var active = EnsureComp<FSActiveGrenadeComponent>(args.Performer);
        active.ActiveType = GrenadeType.Incendiary;
        Dirty(args.Performer, active);
        args.Handled = true;
    }

    private void OnSelectFlash(FSSelectFlashGrenadeEvent args)
    {
        if (!args.Performer.IsValid())
            return;
        var active = EnsureComp<FSActiveGrenadeComponent>(args.Performer);
        active.ActiveType = GrenadeType.Flash;
        Dirty(args.Performer, active);
        args.Handled = true;
    }

    private void OnSelectPipe(FSSelectPipeGrenadeEvent args)
    {
        if (!args.Performer.IsValid())
            return;
        var active = EnsureComp<FSActiveGrenadeComponent>(args.Performer);
        active.ActiveType = GrenadeType.Pipe;
        Dirty(args.Performer, active);
        args.Handled = true;
    }
}
