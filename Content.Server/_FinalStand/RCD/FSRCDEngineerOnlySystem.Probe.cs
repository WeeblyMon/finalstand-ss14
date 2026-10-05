using Content.Server.Hands.Systems;
using Content.Shared._FinalStand.Materials;
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
            if (target is { } unknown && !Exists(unknown))
                target = null;

            if (target is { } owned && TakeDownBlock(user, owned) is { } refusal)
            {
                RaiseNetworkEvent(new FSRCDProbeResultEvent(ev.Rcd, ev.Location, refusal, true), args.SenderSession);
                return;
            }

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

    private void OnBeltPick(FSRCDBeltPickEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || !_hands.TryGetActiveItem(user, out var held)
            || GetEntity(ev.Rcd) != held
            || !HasComp<FSEngineerOnlyRCDComponent>(held)
            || !TryComp<RCDComponent>(held, out var rcd))
            return;

        var available = rcd.AvailablePrototypes;
        if (!available.Contains(ev.Piece))
            return;

        RaiseLocalEvent(held.Value, new RCDSystemMessage(ev.Piece) { Actor = user });
    }

    private string MaterialList(Dictionary<ProtoId<StackPrototype>, int> materials)
    {
        return materials.Count == 0
            ? Loc.GetString("fs-rcd-refund-nothing")
            : FSMaterialText.List(_proto, materials);
    }
}
