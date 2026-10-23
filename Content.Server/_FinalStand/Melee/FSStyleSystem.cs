using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Melee;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Melee;

// Scores melee play into the style meter: varied hits, big swings and kills raise it, getting hit knocks it down a rank.
public sealed partial class FSStyleSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSPerkKillPipelineSystem _killPipeline = default!;

    private const float LightHit = 14f;
    private const float HeavyHit = 24f;
    private const float FinisherHit = 34f;
    private const float ExtraTarget = 5f;
    private const float ChargeBonus = 50f;
    private const float FullChargeBonus = 30f;
    private const float KillPoints = 35f;
    private const float FinisherKill = 15f;
    private const float HeavyKill = 10f;
    private const float MultiKill = 20f;
    private const float SwitchBonus = 1.3f;

    public const float StrikePoints = 5f;
    public const float PiercePoints = 10f;
    public const float StaggerPoints = 4f;
    public const float WallSlamPoints = 25f;
    public const float CrashPoints = 18f;
    public const float ExecutionPoints = 60f;

    // Smaller hits than this don't cost a rank, so chip damage from fire or bleeding leaves the meter alone.
    private const float DropDamage = 5f;

    private static readonly TimeSpan MultiKillWindow = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan DropCooldown = TimeSpan.FromSeconds(0.75);
    private static readonly TimeSpan SwitchWindow = TimeSpan.FromSeconds(5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSStyleComponent, DamageChangedEvent>(OnDamaged);
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    public void Swing(EntityUid user, EntityUid weapon, bool heavy, bool finisher, int targets)
    {
        if (!HasComp<ActorComponent>(user))
            return;

        var points = (finisher ? FinisherHit : heavy ? HeavyHit : LightHit) + ExtraTarget * Math.Clamp(targets - 1, 0, 3);
        if (heavy && TryComp<FSMeleeChargeComponent>(weapon, out var charge))
            points += ChargeBonus * charge.LastCharge + (charge.WasFull ? FullChargeBonus : 0f);

        var style = EnsureComp<FSStyleComponent>(user);
        var now = _timing.CurTime;
        points *= Variety(style, finisher ? "finisher" : heavy ? "heavy" : "light");

        // Swapping weapons mid-fight is the most stylish thing a survivor can do.
        if (style.LastWeapon is { } last && last != weapon && now - style.LastWeaponTime < SwitchWindow)
            points *= SwitchBonus;

        style.LastWeapon = weapon;
        style.LastWeaponTime = now;
        CountHits(style, now, 1);
        Add((user, style), points, now);
    }

    // Extra mobs a swing reaches on its own: pierces, staggers, crashes, follow-up strikes.
    public void Bonus(EntityUid user, float points, int hits = 0)
    {
        if (points <= 0f || !HasComp<ActorComponent>(user))
            return;

        var style = EnsureComp<FSStyleComponent>(user);
        var now = _timing.CurTime;
        CountHits(style, now, hits);
        Add((user, style), points, now);
    }

    public void Kill(EntityUid user, bool heavy, bool finisher)
    {
        if (!HasComp<ActorComponent>(user))
            return;

        var style = EnsureComp<FSStyleComponent>(user);
        var now = _timing.CurTime;
        style.KillStreak = now - style.LastKill < MultiKillWindow ? style.KillStreak + 1 : 1;
        style.LastKill = now;

        var points = KillPoints + (finisher ? FinisherKill : 0f) + (heavy ? HeavyKill : 0f) + MultiKill * (style.KillStreak - 1);
        Add((user, style), points, now);
    }

    public int Rank(EntityUid user)
    {
        return TryComp<FSStyleComponent>(user, out var style) ? FSStyleComponent.RankOf(style.ScoreAt(_timing.CurTime)) : 0;
    }

    // A melee kill pays a little more for each style rank the killer holds.
    public float KillMoneyMultiplier(EntityUid killer, EntityUid target)
    {
        return _killPipeline.IsMeleeKill(killer, target) ? 1f + FSStyleComponent.KillMoneyPerRank * Rank(killer) : 1f;
    }

    public float ShowstopperMultiplier(EntityUid user, FSPerkLevelsComponent perks)
    {
        var level = perks.GetSlottedLevel("Showstopper");
        return level > 0 ? 1f + level * FSPerkBonusConstants.ShowstopperPerRankPerLevel * Rank(user) : 1f;
    }

    private void CountHits(FSStyleComponent style, TimeSpan now, int hits)
    {
        if (hits <= 0)
            return;

        style.Hits = style.HitsAt(now) + hits;
        style.LastHit = now;
    }

    private void Add(Entity<FSStyleComponent> ent, float points, TimeSpan now)
    {
        ent.Comp.Score = Math.Min(FSStyleComponent.MaxScore, ent.Comp.ScoreAt(now) + points);
        ent.Comp.Updated = now;
        Dirty(ent);
    }

    // Repeating the same move earns less each time in a row.
    private static float Variety(FSStyleComponent style, string move)
    {
        var repeats = 0;
        for (var i = style.RecentMoves.Count - 1; i >= 0 && style.RecentMoves[i] == move; i--)
        {
            repeats++;
        }

        style.RecentMoves.Add(move);
        if (style.RecentMoves.Count > 4)
            style.RecentMoves.RemoveAt(0);

        return repeats switch
        {
            0 => 1f,
            1 => 0.75f,
            _ => 0.45f,
        };
    }

    private void OnDamaged(EntityUid uid, FSStyleComponent style, DamageChangedEvent args)
    {
        var ent = new Entity<FSStyleComponent>(uid, style);
        if (!args.DamageIncreased || args.DamageDelta == null || args.DamageDelta.GetTotal() < DropDamage)
            return;

        var now = _timing.CurTime;
        if (now - ent.Comp.LastDrop < DropCooldown)
            return;

        var rank = FSStyleComponent.RankOf(ent.Comp.ScoreAt(now));
        if (rank == 0)
            return;

        ent.Comp.LastDrop = now;
        ent.Comp.Score = FSStyleComponent.Thresholds[rank - 1];
        ent.Comp.Updated = now;
        ent.Comp.Hits = 0;
        Dirty(ent);
    }

    // Showstopper: keeping the crowd on its feet keeps you on yours.
    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        if (!ev.WasMeleeKill)
            return;

        var level = ev.Perks.GetSlottedLevel("Showstopper");
        if (level <= 0 || Rank(ev.Killer) < FSPerkBonusConstants.ShowstopperHealRank)
            return;

        _damageable.HealEvenly(ev.Killer, FixedPoint2.New(-(level * FSPerkBonusConstants.ShowstopperHealPerLevel)));
    }
}
