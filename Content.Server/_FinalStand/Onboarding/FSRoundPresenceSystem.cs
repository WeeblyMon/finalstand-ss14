using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Network;

namespace Content.Server._FinalStand.Onboarding;

public sealed partial class FSRoundPresenceSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private IPlayerManager _playerManager = default!;

    private readonly Dictionary<NetUserId, (int Waves, int Highest)> _presence = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _presence.Clear());
    }

    public int GetWavesPresent(NetUserId userId) => _presence.GetValueOrDefault(userId).Waves;

    public int GetHighestWavePresent(NetUserId userId) => _presence.GetValueOrDefault(userId).Highest;

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame || session.AttachedEntity == null)
                continue;
            if (!_mind.TryGetMind(session, out var mindId, out _) || !HasComp<FSPlayerLevelComponent>(mindId))
                continue;

            var current = _presence.GetValueOrDefault(session.UserId);
            _presence[session.UserId] = (current.Waves + 1, Math.Max(current.Highest, args.WaveNumber));
        }
    }
}
