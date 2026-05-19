using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Perks;
using Content.Server.Popups;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Mind;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Shop;

public sealed class FSShopPerkSystem : EntitySystem
{
    [Dependency] private readonly FSPlayerWalletSystem _wallet = default!;
    [Dependency] private readonly PerkSystem _perks = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        Subs.BuiEvents<FSShopWeaponComponent>(FSShopWeaponUiKey.Key, subs =>
        {
            subs.Event<FSShopBuyPerkMessage>(OnBuyPerkMessage);
        });
    }

    private void OnBuyPerkMessage(EntityUid shopUid, FSShopWeaponComponent shopComp, FSShopBuyPerkMessage args)
    {
        var player = args.Actor;
        if (!player.IsValid())
            return;

        if (!TryComp<FSShopPerksComponent>(shopUid, out var perksComp))
            return;

        if (!_proto.TryIndex<PerkPrototype>(args.PerkProtoId, out var perkDef))
            return;

        if (!perksComp.Perks.Contains(new ProtoId<PerkPrototype>(args.PerkProtoId)))
            return;

        if (perkDef.IsLocked)
        {
            _popup.PopupEntity("Coming soon.", shopUid, player);
            return;
        }

        var perkComp = EnsureComp<PerkComponent>(player);

        if (perkComp.HasPerk(perkDef.PerkType))
        {
            _popup.PopupEntity("Already active.", shopUid, player);
            return;
        }

        if (!perkComp.CanAddPerk())
        {
            _popup.PopupEntity("No perk slots available.", shopUid, player);
            return;
        }

        if (!_mind.TryGetMind(player, out var mindId, out var _))
            return;

        if (!_wallet.TryDeductCredits(mindId, perkDef.Cost))
        {
            _popup.PopupEntity("Insufficient funds.", shopUid, player);
            return;
        }

        _perks.AddPerk(player, perkDef.PerkType);
        _popup.PopupEntity($"{perkDef.Name} activated!", shopUid, player);
    }
}
