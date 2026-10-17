using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared.GameTicking;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Engineering;

public sealed partial class FSEngineeringTelemetrySystem : EntitySystem
{
    private readonly Dictionary<string, int> _dropped = new();
    private readonly Dictionary<string, Dictionary<string, int>> _spent = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Clear());
    }

    public void RecordDrop(EntProtoId proto)
    {
        _dropped[proto.Id] = _dropped.GetValueOrDefault(proto.Id) + 1;
    }

    public void RecordSpend(string source, IReadOnlyDictionary<ProtoId<StackPrototype>, int> materials)
    {
        if (!_spent.TryGetValue(source, out var bySource))
            _spent[source] = bySource = new Dictionary<string, int>();

        foreach (var (material, amount) in materials)
            bySource[material.Id] = bySource.GetValueOrDefault(material.Id) + amount;
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        var dropped = string.Join(", ", _dropped.OrderBy(d => d.Key).Select(d => $"{d.Key} {d.Value}"));
        var spent = string.Join("; ", _spent.OrderBy(s => s.Key)
            .Select(s => $"{s.Key}: " + string.Join(", ", s.Value.OrderBy(m => m.Key).Select(m => $"{m.Key} {m.Value}"))));
        Log.Info($"Wave {ev.WaveNumber} materials. Dropped: {(dropped.Length > 0 ? dropped : "none")}. Spent: {(spent.Length > 0 ? spent : "none")}.");
        Clear();
    }

    private void Clear()
    {
        _dropped.Clear();
        _spent.Clear();
    }
}
