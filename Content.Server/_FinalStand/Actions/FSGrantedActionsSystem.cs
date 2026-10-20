using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Actions;

public sealed partial class FSGrantedActionsSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public bool TryGet(EntityUid mob, EntProtoId proto, out EntityUid action)
    {
        action = default;
        return TryComp<FSGrantedActionsComponent>(mob, out var granted)
               && granted.Actions.TryGetValue(proto, out action)
               && !TerminatingOrDeleted(action);
    }

    public bool Has(EntityUid mob, EntProtoId proto) => TryGet(mob, proto, out _);

    public void Grant(EntityUid mob, EntProtoId proto)
    {
        if (Has(mob, proto) || _actions.AddAction(mob, proto) is not { } action)
            return;

        EnsureComp<FSGrantedActionsComponent>(mob).Actions[proto] = action;
    }

    public void Revoke(EntityUid mob, EntProtoId proto)
    {
        if (!TryComp<FSGrantedActionsComponent>(mob, out var granted) || !granted.Actions.Remove(proto, out var action))
            return;

        _actions.RemoveAction(mob, action);
    }
}
