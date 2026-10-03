using Content.Server.Hands.Systems;
using Content.Shared._FinalStand.RCD;
using Content.Shared.Interaction;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.RCD;

public sealed partial class FSRCDEngineerOnlySystem
{
    [Dependency] private HandsSystem _hands = default!;

    private const float ProbeRange = SharedInteractionSystem.InteractionRange + 1f;

    private void OnProbe(FSRCDProbeRequestEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !_hands.TryGetActiveItem(user, out var held)
            || GetEntity(ev.Rcd) != held
            || !TryComp<FSEngineerOnlyRCDComponent>(held, out var comp)
            || !TryComp<RCDComponent>(held, out var rcd))
            return;

        var location = GetCoordinates(ev.Location);
        if (!location.IsValid(EntityManager) || !_transform.InRange(Transform(user).Coordinates, location, ProbeRange))
            return;

        string? text;
        bool blocked;
        if (_proto.Index(rcd.ProtoId).Mode == RcdMode.Deconstruct)
        {
            var target = GetEntity(ev.Target);
            var refund = target is { } t ? RefundFor(t) : null;
            blocked = refund == null && target != null && !HasComp<RCDDeconstructableComponent>(target);
            text = refund != null
                ? Loc.GetString("fs-rcd-refund", ("list", MaterialList(refund)))
                : blocked ? Loc.GetString("fs-rcd-cant-deconstruct") : null;
        }
        else
        {
            text = GetBlockReason(held.Value, comp, user, location);
            blocked = text != null;
        }

        RaiseNetworkEvent(new FSRCDProbeResultEvent(ev.Rcd, ev.Location, text, blocked), args.SenderSession);
    }

    private string MaterialList(Dictionary<ProtoId<StackPrototype>, int> materials)
    {
        if (materials.Count == 0)
            return Loc.GetString("fs-rcd-refund-nothing");

        var parts = new List<string>();
        foreach (var (material, amount) in materials)
            parts.Add($"{amount} {Loc.GetString(_proto.Index(material).Name)}");

        return string.Join(", ", parts);
    }
}
