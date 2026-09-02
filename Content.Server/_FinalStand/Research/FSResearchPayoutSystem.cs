using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Science;
using Content.Shared._FinalStand.Research.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Research;

// pays the science department for completing tech nodes, scaling with tree depth
public sealed partial class FSResearchPayoutSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSScienceOnlySystem _science = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    private const int CreditsPerTier = 60;
    private const int WeaponUnlockBonus = 3000;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnNodeCompleted);
    }

    private void OnNodeCompleted(FSResearchNodeCompletedEvent ev)
    {
        if (!ev.Earned || !_proto.TryIndex<FSTechNodePrototype>(ev.NodeId, out var node))
            return;

        var payout = CreditsPerTier * node.Tier;
        if (node.WeaponShopUnlock != null)
            payout += WeaponUnlockBonus;

        if (payout <= 0)
            return;

        var message = Loc.GetString("fs-research-payout",
            ("node", node.Name), ("credits", payout));

        var paid = 0;
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is not { } mob || !_science.IsScience(mob))
                continue;

            if (!_mind.TryGetMind(mob, out var mindId, out _))
                continue;

            _wallet.GiveCredits(mindId, payout);
            _popup.PopupEntity(message, mob, mob);
            paid++;
        }

        if (paid > 0)
            Log.Info($"[FSResearch] Node {node.ID} paid {payout} credits to {paid} science player(s)");
    }
}
