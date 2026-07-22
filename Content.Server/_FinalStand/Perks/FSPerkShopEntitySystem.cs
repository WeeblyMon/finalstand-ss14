using Content.Shared._FinalStand.Perks;
using Content.Shared.Interaction;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Perks;

public sealed class FSPerkShopEntitySystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPerkShopComponent, ActivateInWorldEvent>(OnActivated);
    }

    private void OnActivated(EntityUid uid, FSPerkShopComponent _, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        if (!TryComp(args.User, out ActorComponent? actor))
            return;
        RaiseNetworkEvent(new FSOpenPerkShopEvent(), actor.PlayerSession);
        args.Handled = true;
    }
}
