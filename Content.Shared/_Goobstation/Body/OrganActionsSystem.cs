using Content.Shared._FinalStand.Medical;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Body;
using Robust.Shared.Network;

namespace Content.Goobstation.Shared.Body;

public sealed partial class OrganActionsSystem : EntitySystem
{
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedActionsSystem _actions = default!;

    private EntityQuery<OrganComponent> _organQuery;

    public override void Initialize()
    {
        base.Initialize();

        _organQuery = GetEntityQuery<OrganComponent>();

        SubscribeLocalEvent<OrganActionsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<OrganActionsComponent, OrganGotInsertedEvent>(OnAdded);
        SubscribeLocalEvent<OrganActionsComponent, OrganEnabledEvent>(OnEnabled);
        SubscribeLocalEvent<OrganActionsComponent, OrganDisabledEvent>(OnDisabled);
        SubscribeLocalEvent<OrganActionsComponent, OrganGotRemovedEvent>(OnRemoved);
    }

    private void OnMapInit(Entity<OrganActionsComponent> ent, ref MapInitEvent args)
    {
        var actions = EnsureComp<ActionsContainerComponent>(ent);
        foreach (var id in ent.Comp.Actions)
        {
            _actionContainer.AddAction(ent, id, actions);
        }
    }

    private void OnAdded(Entity<OrganActionsComponent> ent, ref OrganGotInsertedEvent args)
    {
        // container shit chuds out
        if (_net.IsClient)
            return;

        if (TryComp<ActionsContainerComponent>(ent, out var container))
            _actions.GrantContainedActions(args.Target, (ent, container));
    }

    private void OnEnabled(Entity<OrganActionsComponent> ent, ref OrganEnabledEvent args)
    {
        if (!_organQuery.TryComp(ent, out var organ) || organ.Body is not { } body)
            return;

        if (TryComp<ActionsContainerComponent>(ent, out var container))
            _actions.GrantContainedActions(body, (ent, container));
    }

    private void OnDisabled(Entity<OrganActionsComponent> ent, ref OrganDisabledEvent args)
    {
        if (args.Organ.Comp.Body is {} body)
            _actions.RemoveProvidedActions(body, ent.Owner);
    }

    private void OnRemoved(Entity<OrganActionsComponent> ent, ref OrganGotRemovedEvent args)
    {
        _actions.RemoveProvidedActions(args.Target, ent.Owner);
    }
}
