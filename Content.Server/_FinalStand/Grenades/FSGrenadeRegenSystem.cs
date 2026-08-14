using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.GameTicking;

namespace Content.Server._FinalStand.Grenades;

public sealed partial class FSGrenadeRegenSystem : EntitySystem
{
    [Dependency] private FSGrenadeSelectActionSystem _grenadeSelect = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnWavePrepStarted);
    }

    private void OnWavePrepStarted(WavePrepStartedEvent _)
    {
        var query = EntityQueryEnumerator<FSGrenadePackComponent>();
        while (query.MoveNext(out var uid, out var pack))
        {
            if (pack.Stock >= pack.MaxStock)
                continue;
            pack.Stock = Math.Min(pack.MaxStock, pack.Stock + pack.RegenPerWave);
            Dirty(uid, pack);
            _grenadeSelect.SyncPackCounter(uid, pack);
        }
    }
}
