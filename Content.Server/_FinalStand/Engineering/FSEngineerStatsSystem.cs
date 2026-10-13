// Engineer income and stats: pays owners for wave damage their structures soak and for repairing that damage,
// counts equipment kills, sends each engineer a report card when a wave ends, and writes the engineering block of the
// round-end summary.
using System.Linq;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Leveling;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.GameTicking;
using Content.Server.GameTicking;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Server.Player;
using Robust.Shared.Enums;

namespace Content.Server._FinalStand.Engineering;

public sealed partial class FSEngineerStatsSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private IPlayerManager _players = default!;

    private const float AbsorbBudget = 300f;
    private const float AbsorbCreditsPerHp = 1f;
    private const float RepairFullBudget = 150f;
    private const float RepairHalfBudget = 450f;
    private const float RepairHalfRate = 0.35f;
    private const float RepairCreditsPerHp = 6f;
    private const float PointsPerCredit = 0.1f;

    public readonly record struct FSEngineerRoundStats(float Absorbed, float Repaired, int EquipmentKills, float Points);

    private readonly Dictionary<EntityUid, FSEngineerRoundStats> _roundStats = new();
    private readonly Dictionary<EntityUid, float> _waveDamage = new();
    private readonly Dictionary<EntityUid, float> _absorbedThisWave = new();
    private readonly Dictionary<(EntityUid Mind, EntityUid Structure), float> _repairedThisWave = new();
    private readonly Dictionary<EntityUid, float> _unpaid = new();

    public FSEngineerRoundStats GetStats(EntityUid mindId) => _roundStats.GetValueOrDefault(mindId);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDeployedByComponent, DamageChangedEvent>(OnStructureDamageChanged);
        SubscribeLocalEvent<FSWaveEnemyDiedEvent>(OnEnemyDied);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEnd, after: [typeof(FSLevelingSystem)]);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _roundStats.Clear();
        _waveDamage.Clear();
        _absorbedThisWave.Clear();
        _repairedThisWave.Clear();
        _unpaid.Clear();
    }

    private void OnPrepStarted(WavePrepStartedEvent ev)
    {
        _absorbedThisWave.Clear();
        _repairedThisWave.Clear();
    }

    private void OnStructureDamageChanged(Entity<FSDeployedByComponent> ent, ref DamageChangedEvent args)
    {
        if (args.DamageDelta is not { } delta || delta.Empty)
            return;

        var amount = delta.GetTotal().Float();
        if (args.DamageIncreased)
            OnAbsorbed(ent, amount, args.Origin);
        else if (amount < 0f)
            OnRepaired(ent.Owner, -amount, args.Origin);
    }

    // Only wave enemies count: player damage and wire hits (no attacker) never pay.
    private void OnAbsorbed(Entity<FSDeployedByComponent> ent, float amount, EntityUid? origin)
    {
        if (origin is not { } attacker || !HasComp<WaveSpawnedTagComponent>(attacker))
            return;

        _waveDamage[ent.Owner] = _waveDamage.GetValueOrDefault(ent.Owner) + amount;

        if (ent.Comp.OwnerMind is not { } owner || !IsPresent(owner))
            return;

        var prior = _absorbedThisWave.GetValueOrDefault(ent.Owner);
        var paid = Math.Clamp(AbsorbBudget - prior, 0f, amount);
        _absorbedThisWave[ent.Owner] = prior + amount;

        Record(owner, absorbed: amount, credits: paid * AbsorbCreditsPerHp);
    }

    // Repairs pay only for wave damage the structure is still carrying, with diminishing returns per repairer.
    private void OnRepaired(EntityUid structure, float amount, EntityUid? origin)
    {
        if (origin is not { } repairer || !_mind.TryGetMind(repairer, out var mindId, out _))
            return;

        var outstanding = _waveDamage.GetValueOrDefault(structure);
        var eligible = MathF.Min(amount, outstanding);
        _waveDamage[structure] = outstanding - eligible;

        var key = (mindId, structure);
        var prior = _repairedThisWave.GetValueOrDefault(key);
        var full = Math.Clamp(RepairFullBudget - prior, 0f, eligible);
        var half = Math.Clamp(RepairHalfBudget - MathF.Max(prior, RepairFullBudget), 0f, eligible - full);
        _repairedThisWave[key] = prior + eligible;

        Record(mindId, repaired: amount, credits: (full + half * RepairHalfRate) * RepairCreditsPerHp);
    }

    private void OnEnemyDied(ref FSWaveEnemyDiedEvent args)
    {
        if (args.Killer is { } killer
            && TryComp<FSDeployedByComponent>(killer, out var deployed)
            && deployed.OwnerMind is { } owner)
            Record(owner, kills: 1);
    }

    private bool IsPresent(EntityUid mindId)
    {
        return TryComp<MindComponent>(mindId, out var mind)
               && mind.UserId is { } userId
               && _players.TryGetSessionById(userId, out var session)
               && session.Status == SessionStatus.InGame;
    }

    private void Record(EntityUid mindId, float absorbed = 0f, float repaired = 0f, int kills = 0, float credits = 0f)
    {
        var cur = _roundStats.GetValueOrDefault(mindId);
        _roundStats[mindId] = new FSEngineerRoundStats(
            cur.Absorbed + absorbed,
            cur.Repaired + repaired,
            cur.EquipmentKills + kills,
            cur.Points + credits * PointsPerCredit);

        if (credits <= 0f)
            return;

        // Hits land a few HP at a time; bank the fractions and pay whole credits.
        var owed = _unpaid.GetValueOrDefault(mindId) + credits;
        var whole = (int) owed;
        _unpaid[mindId] = owed - whole;
        if (whole > 0)
            _wallet.GiveCredits(mindId, whole);
    }

    private void OnRoundEnd(RoundEndTextAppendEvent args)
    {
        var sorted = _roundStats
            .Where(kv => kv.Key.IsValid() && (kv.Value.Absorbed > 0f || kv.Value.Repaired > 0f || kv.Value.EquipmentKills > 0))
            .OrderByDescending(kv => kv.Value.Points)
            .ToList();

        if (sorted.Count == 0)
            return;

        args.AddLine(Loc.GetString("fs-engineer-roundend-header"));
        foreach (var (mindId, stats) in sorted)
        {
            if (!TryComp<MindComponent>(mindId, out var mind))
                continue;

            args.AddLine(Loc.GetString("fs-engineer-roundend-line",
                ("name", mind.CharacterName ?? "Unknown"),
                ("absorbed", (int) stats.Absorbed),
                ("repaired", (int) stats.Repaired),
                ("kills", stats.EquipmentKills)));
        }

        args.AddLine(Loc.GetString("fs-engineer-roundend-footer"));
    }
}
