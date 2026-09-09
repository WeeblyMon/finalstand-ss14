using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.MedicalOps.Shop;
using Robust.Client.Player;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSSyringeUpgradeClientSystem : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;

    public int Credits { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<WalletUpdatedEvent>(OnWalletUpdated);
    }

    private void OnWalletUpdated(WalletUpdatedEvent ev)
    {
        Credits = ev.Credits;
    }

    public string? OwnedTierId()
    {
        return _player.LocalEntity is { } player
               && TryComp<FSSyringeUpgradeComponent>(player, out var comp)
            ? comp.TierId
            : null;
    }
}
