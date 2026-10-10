using Content.Server._FinalStand.Departments;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Engineering;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Enums;

namespace Content.Server._FinalStand.WaveReport;

public sealed class FSWaveReportSystem : EntitySystem
{
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSDepartmentAccessSystem _departments = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private FSResearchSystem _research = default!;
    [Dependency] private FSLevelingSystem _leveling = default!;
    [Dependency] private FSMedicalStatsSystem _medical = default!;
    [Dependency] private FSEngineerStatsSystem _engineering = default!;

    private readonly record struct Totals(int Kills, int Assists, float Soaked, float Repaired, int EquipmentKills, float Healed,
        int Revives);

    private readonly Dictionary<EntityUid, Totals> _last = new();
    private readonly Dictionary<EntityUid, int> _earned = new();
    private readonly Dictionary<EntityUid, int> _points = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        _wallet.CreditsGiven += (mind, amount) => _earned[mind] = _earned.GetValueOrDefault(mind) + amount;
        _research.PointsContributed += (mind, amount) => _points[mind] = _points.GetValueOrDefault(mind) + amount;
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _last.Clear();
        _earned.Clear();
        _points.Clear();
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.Status != SessionStatus.InGame
                || session.AttachedEntity is not { } body
                || !_mind.TryGetMind(body, out var mindId, out _))
                continue;

            var now = Read(mindId);
            var before = _last.GetValueOrDefault(mindId);
            _last[mindId] = now;
            var earned = _earned.GetValueOrDefault(mindId);
            var research = _points.GetValueOrDefault(mindId);

            var report = Build(ev.WaveNumber, body, now, before, earned, research);
            if (report.Values.Exists(v => v > 0))
                RaiseNetworkEvent(report, session);
        }

        _earned.Clear();
        _points.Clear();
    }

    private Totals Read(EntityUid mindId)
    {
        var (kills, assists) = _leveling.KillsOf(mindId);
        var eng = _engineering.GetStats(mindId);
        var med = _medical.GetStats(mindId);
        return new Totals(kills, assists, eng.Absorbed, eng.Repaired, eng.EquipmentKills, med.HpHealed, med.Stabilises + med.Revives);
    }

    private FSWaveReportEvent Build(int wave, EntityUid body, Totals now, Totals before, int earned, int research)
    {
        var kills = now.Kills - before.Kills;
        if (_departments.IsEngineering(body))
        {
            var report = new FSWaveReportEvent(wave, Loc.GetString("fs-wave-report-title-engineering", ("wave", wave)));
            report.Add(Loc.GetString("fs-wave-report-soaked"), (int) (now.Soaked - before.Soaked));
            report.Add(Loc.GetString("fs-wave-report-repaired"), (int) (now.Repaired - before.Repaired), FSWaveReportStyle.Gain);
            report.Add(Loc.GetString("fs-wave-report-kills"), kills + now.EquipmentKills - before.EquipmentKills);
            report.Add(Loc.GetString("fs-wave-report-earned"), earned, FSWaveReportStyle.Money);
            return report;
        }

        if (_departments.IsMedical(body))
        {
            var report = new FSWaveReportEvent(wave, Loc.GetString("fs-wave-report-title-medical", ("wave", wave)));
            report.Add(Loc.GetString("fs-wave-report-healed"), (int) (now.Healed - before.Healed), FSWaveReportStyle.Gain);
            report.Add(Loc.GetString("fs-wave-report-revives"), now.Revives - before.Revives);
            report.Add(Loc.GetString("fs-wave-report-kills"), kills);
            report.Add(Loc.GetString("fs-wave-report-earned"), earned, FSWaveReportStyle.Money);
            return report;
        }

        if (_departments.IsScience(body))
        {
            var report = new FSWaveReportEvent(wave, Loc.GetString("fs-wave-report-title-science", ("wave", wave)));
            report.Add(Loc.GetString("fs-wave-report-research"), research, FSWaveReportStyle.Gain);
            report.Add(Loc.GetString("fs-wave-report-kills"), kills);
            report.Add(Loc.GetString("fs-wave-report-assists"), now.Assists - before.Assists);
            report.Add(Loc.GetString("fs-wave-report-earned"), earned, FSWaveReportStyle.Money);
            return report;
        }

        var combat = new FSWaveReportEvent(wave, Loc.GetString("fs-wave-report-title-combat", ("wave", wave)));
        combat.Add(Loc.GetString("fs-wave-report-kills"), kills);
        combat.Add(Loc.GetString("fs-wave-report-assists"), now.Assists - before.Assists);
        combat.Add(Loc.GetString("fs-wave-report-earned"), earned, FSWaveReportStyle.Money);
        return combat;
    }
}
