using Content.Shared._FinalStand.CCC;

namespace Content.Client._FinalStand.CCC;

public sealed class CCCCapabilityClientSystem : EntitySystem
{
    public bool CanStartWave { get; private set; }

    public event Action<bool>? CanStartWaveChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CCCCanStartWaveEvent>(OnReceived);
    }

    private void OnReceived(CCCCanStartWaveEvent ev)
    {
        CanStartWave = ev.CanStartWave;
        CanStartWaveChanged?.Invoke(CanStartWave);
    }
}
