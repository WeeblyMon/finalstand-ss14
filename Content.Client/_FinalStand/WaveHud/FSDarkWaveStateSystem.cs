using Content.Shared._FinalStand.WaveHud;
using Content.Shared.GameTicking;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class FSDarkWaveStateSystem : EntitySystem
{
    public bool IsDarkWave { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSDarkWaveStartedEvent>(OnStarted);
        SubscribeNetworkEvent<FSDarkWaveEndedEvent>(OnEnded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnStarted(FSDarkWaveStartedEvent ev) => IsDarkWave = true;

    private void OnEnded(FSDarkWaveEndedEvent ev) => IsDarkWave = false;

    private void OnRoundRestart(RoundRestartCleanupEvent args) => IsDarkWave = false;
}
