// One place that turns "this perk's stack count changed" into a message for the owning client.
// Five systems carried their own copy of this mind -> body -> session walk.
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Perks;

public sealed class FSPerkNotifySystem : EntitySystem
{
    /// <summary>Send a stack count to the player owning <paramref name="mindId"/>.</summary>
    public void SendStacks(EntityUid mindId, string perkId, int stacks)
    {
        if (!TryComp<MindComponent>(mindId, out var mind) || mind.CurrentEntity is not { } body)
            return;
        SendStacksToBody(body, perkId, stacks);
    }

    /// <summary>Send a stack count to the player controlling <paramref name="bodyUid"/>.</summary>
    public void SendStacksToBody(EntityUid bodyUid, string perkId, int stacks)
    {
        if (!TryComp<ActorComponent>(bodyUid, out var actor))
            return;

        RaiseNetworkEvent(new FSPerkStacksUpdateEvent { PerkId = perkId, Stacks = stacks },
            Filter.SinglePlayer(actor.PlayerSession));
    }
}
