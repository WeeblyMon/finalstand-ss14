using Content.Shared._FinalStand.GameTicking;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class FSDarkWaveStateSystem : EntitySystem
{
    public bool IsDarkWave
    {
        get
        {
            var query = EntityQueryEnumerator<FSWaveStateComponent>();
            return query.MoveNext(out var state) && state.DarkWave;
        }
    }
}
