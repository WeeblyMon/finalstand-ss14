using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.WaveHud;

[Serializable, NetSerializable]
public enum FSWaveReportStyle : byte
{
    Plain,
    Gain,
    Money,
}

[Serializable, NetSerializable]
public sealed class FSWaveReportEvent(int wave, string title) : EntityEventArgs
{
    public readonly int Wave = wave;
    public readonly string Title = title;
    public List<string> Captions = new();
    public List<int> Values = new();
    public List<FSWaveReportStyle> Styles = new();

    public void Add(string caption, int value, FSWaveReportStyle style = FSWaveReportStyle.Plain)
    {
        Captions.Add(caption);
        Values.Add(value);
        Styles.Add(style);
    }
}
