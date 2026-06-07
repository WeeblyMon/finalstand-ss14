using Content.Shared._FinalStand.Augments;
using Content.Shared.Interaction;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Augments;

public sealed class FSAugmentShopEntitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSAugmentShopComponent, ActivateInWorldEvent>(OnActivated);
    }

    private void OnActivated(EntityUid uid, FSAugmentShopComponent _, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        if (!TryComp(args.User, out ActorComponent? actor))
            return;
        RaiseNetworkEvent(new FSOpenAugmentShopEvent(), actor.PlayerSession);
        args.Handled = true;
    }
}
