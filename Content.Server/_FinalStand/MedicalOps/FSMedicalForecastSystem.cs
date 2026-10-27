using Content.Server._FinalStand.Chat;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Chat.Managers;
using Content.Shared._FinalStand.Ailments;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.GameTicking;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSMedicalForecastSystem : EntitySystem
{
    [Dependency] private WaveGameRuleSystem _waves = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IChatManager _chat = default!;

    private static readonly EntProtoId DarkRevenant = "FSZombieRevenantDark";

    private readonly Dictionary<(FSAilmentSlot Slot, int Level), int> _firstWave = new();
    private readonly List<string> _lines = new();
    private readonly List<string> _advice = new();

    public string? Current { get; private set; }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<FSDarkWaveWarningEvent>(OnDarkWaveWarning);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _firstWave.Clear();
        Current = null;
    }

    private void OnPrepStarted(WavePrepStartedEvent ev)
    {
        if (!_waves.TryGetActiveState(out var wave))
            return;

        var forecast = Build(wave.WaveNumber, wave.NextWaveEnemyTypes);
        _chat.DispatchServerMarkupAnnouncement(forecast);
        SetPanels(forecast);
    }

    private void OnDarkWaveWarning(FSDarkWaveWarningEvent ev)
    {
        if (!_waves.TryGetActiveState(out var wave))
            return;

        _chat.DispatchServerMarkupAnnouncement(Build(wave.WaveNumber, [DarkRevenant]));
        SetPanels(Build(wave.WaveNumber, [..wave.NextWaveEnemyTypes, DarkRevenant]));
    }

    public string Build(int wave, IEnumerable<string> enemies)
    {
        _lines.Clear();
        _advice.Clear();

        var seen = new HashSet<string>();
        foreach (var id in enemies)
        {
            if (!seen.Add(id)
                || !_proto.TryIndex<EntityPrototype>(id, out var proto)
                || !proto.TryGetComponent<FSSignatureAilmentComponent>(out var signature, Factory))
            {
                continue;
            }

            foreach (var (slot, level) in signature.Ailments)
            {
                if (!_ailments.Rules.Slots.TryGetValue(slot, out var info) || info.Name is not { } name)
                    continue;

                if (!_firstWave.TryGetValue((slot, level), out var first))
                    _firstWave[(slot, level)] = first = wave;

                var ailment = level > 0
                    ? Loc.GetString("fs-ailment-name-level", ("name", Loc.GetString(name)), ("level", level))
                    : Loc.GetString(name);

                _lines.Add(Loc.GetString("fs-forecast-line", ("new", first == wave), ("enemy", proto.Name), ("ailment", ailment)));

                if (info.Advice is { } adviceId && Loc.GetString(adviceId) is var advice && !_advice.Contains(advice))
                    _advice.Add(advice);
            }
        }

        return _lines.Count == 0
            ? Loc.GetString("fs-forecast-none", ("wave", wave))
            : Loc.GetString("fs-forecast-header", ("wave", wave), ("lines", string.Join(", ", _lines)), ("advice", string.Join(", ", _advice)));
    }

    private void SetPanels(string forecast)
    {
        Current = forecast;
        var query = EntityQueryEnumerator<FSCmoPanelComponent>();
        while (query.MoveNext(out var uid, out var panel))
        {
            panel.Forecast = forecast;
            Dirty(uid, panel);
        }
    }
}
