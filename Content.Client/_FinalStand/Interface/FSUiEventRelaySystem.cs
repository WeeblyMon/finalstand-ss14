using Content.Shared._FinalStand.Economy;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Ladder;
using Content.Shared._FinalStand.Leaderboard;
using Content.Shared._FinalStand.Leveling;
using Content.Shared._FinalStand.Tutorial;

namespace Content.Client._FinalStand.Interface;

public sealed class FSUiEventRelaySystem : EntitySystem
{
    public event Action<WalletUpdatedEvent>? WalletUpdated;
    public event Action<FSLevelingUpdatedEvent>? LevelingUpdated;
    public event Action<FSWaveStatusEvent>? WaveStatusUpdated;
    public event Action<FSLeaderboardUpdateEvent>? LeaderboardUpdated;
    public event Action? LadderOpenRequested;
    public event Action<FSLadderResponseEvent>? LadderResponse;
    public event Action<FSTutorialTip>? TutorialTip;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WalletUpdatedEvent>(ev => WalletUpdated?.Invoke(ev));
        SubscribeNetworkEvent<FSLevelingUpdatedEvent>(ev => LevelingUpdated?.Invoke(ev));
        SubscribeNetworkEvent<FSWaveStatusEvent>(ev => WaveStatusUpdated?.Invoke(ev));
        SubscribeNetworkEvent<FSLeaderboardUpdateEvent>(ev => LeaderboardUpdated?.Invoke(ev));
        SubscribeNetworkEvent<FSOpenLadderEvent>(_ => LadderOpenRequested?.Invoke());
        SubscribeNetworkEvent<FSLadderResponseEvent>(ev => LadderResponse?.Invoke(ev));
        SubscribeNetworkEvent<FSTutorialTipEvent>(ev => TutorialTip?.Invoke(ev.Tip));
    }
}
