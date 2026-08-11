using Content.Shared._FinalStand.Medical.Relay;
using Content.Shared._Shitmed.Weapons.Melee.Events;
using Content.Shared._Shitmed.Weapons.Ranged.Events;
using Content.Shared.Body;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Wieldable.Components;

namespace Content.Shared._Shitmed.Weapons.Systems;

public sealed class FumbleOnDamageSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly OrganRelaySystem _relay = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, AttemptMeleeEvent>(OnAttemptMeleeEvent);
        SubscribeLocalEvent<HandsComponent, GunShotBodyEvent>(OnAttemptShootEvent);
    }

    private void OnAttemptMeleeEvent(EntityUid uid, HandsComponent hands, ref AttemptMeleeEvent ev)
    {
        var useAllHands = ev.WeaponComponent.MustBeEquippedToUse
                          || TryComp(ev.Weapon, out WieldableComponent? wieldable)
                          && wieldable.Wielded;

        var ev2 = new AttemptHandsMeleeEvent();
        if (useAllHands)
            _relay.RelayEvent(uid, ev2);
        else if (GetActiveHandOrgan(uid, hands) is { } organ)
            RaiseLocalEvent(organ, ev2);

        if (ev2.Cancelled)
            ev.Cancelled = true;
    }

    private void OnAttemptShootEvent(EntityUid uid, HandsComponent hands, GunShotBodyEvent ev)
    {
        if (ev.GunUid == uid) // If the gun is the same user with a component e.g. laser eyes, dont bother.
            return;

        var useAllHands = TryComp(ev.GunUid, out WieldableComponent? wieldable) && wieldable.Wielded;

        var ev2 = new AttemptHandsShootEvent();
        if (useAllHands)
            _relay.RelayEvent(uid, ev2);
        else if (GetActiveHandOrgan(uid, hands) is { } organ)
            RaiseLocalEvent(organ, ev2);
    }

    private EntityUid? GetActiveHandOrgan(EntityUid uid, HandsComponent hands)
    {
        if (_hands.GetActiveHand((uid, hands)) is not { } hand)
            return null;

        foreach (var organ in _body.EnumerateOrgans<HandOrganComponent>(uid))
        {
            if (organ.Comp2.HandID == hand)
                return organ.Owner;
        }

        return null;
    }
}
