using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.Engineering;

// What one engineer's work did over the wave that just ended, for the report card.
[Serializable, NetSerializable]
public sealed class FSEngineerWaveReportEvent(int wave, int absorbed, int repaired, int kills, int credits) : EntityEventArgs
{
    public readonly int Wave = wave;
    public readonly int Absorbed = absorbed;
    public readonly int Repaired = repaired;
    public readonly int Kills = kills;
    public readonly int Credits = credits;
}
