using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

// Damage bonus that grows as health drops toward going down; the HUD shows the current percentage.
public sealed partial class FSBerserkerSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private FSUndyingSystem _undying = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;

    private static readonly TimeSpan HudInterval = TimeSpan.FromSeconds(0.5);
    private TimeSpan _nextHud;
    private readonly Dictionary<EntityUid, int> _shown = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => _shown.Clear());
    }

    public float GetMultiplier(EntityUid body, FSPerkLevelsComponent perks, bool ranged)
    {
        var level = perks.GetSlottedLevel("Berserker");
        if (level <= 0)
            return 1f;

        var bonus = level * FSPerkBonusConstants.BerserkerPerLevel * MissingHealth(body);
        if (ranged)
            bonus *= FSPerkBonusConstants.BerserkerRangedFactor;

        return 1f + bonus;
    }

    private float MissingHealth(EntityUid body)
    {
        if (_undying.IsActive(body))
            return 1f;

        if (!_thresholds.TryGetIncapThreshold(body, out var threshold) || threshold.Value <= 0)
            return 0f;

        return Math.Clamp(_damageable.GetTotalDamage(body).Float() / threshold.Value.Float(), 0f, 1f);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_timing.CurTime < _nextHud)
            return;
        _nextHud = _timing.CurTime + HudInterval;

        var query = EntityQueryEnumerator<FSPerkLevelsComponent, MindComponent>();
        while (query.MoveNext(out var mindId, out var perks, out var mind))
        {
            if (mind.CurrentEntity is not { } body || perks.GetSlottedLevel("Berserker") <= 0)
            {
                _shown.Remove(mindId);
                continue;
            }

            var pct = (int) MathF.Round((GetMultiplier(body, perks, ranged: false) - 1f) * 100f);
            if (_shown.TryGetValue(mindId, out var last) && last == pct)
                continue;

            _shown[mindId] = pct;
            _notify.SendStacks(mindId, "Berserker", pct);
        }
    }
}
