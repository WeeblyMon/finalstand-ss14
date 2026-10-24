using System.Linq;
using Content.Server._FinalStand.CCC;
using Content.Server._FinalStand.Economy;
using Content.Server._FinalStand.Effects;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.MedicalOps;
using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.CCC;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Objectives;
using Content.Shared.Damage.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Objectives;

public sealed partial class FSWaveObjectiveSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    public const float SupplyDeliveryRange = 3f;
    private static readonly TimeSpan SupplyCheckInterval = TimeSpan.FromSeconds(0.5);
    private static readonly LocId TargetLabel = "fs-objective-marker-target";
    private static readonly LocId BountyLabel = "fs-objective-marker-bounty";
    private static readonly LocId SupplyLabel = "fs-objective-marker-supplies";
    public static readonly Color BountyTint = Color.FromHex("#FFD24A");

    private TimeSpan _nextSupplyCheck;

    private void InitializeKinds()
    {
        SubscribeLocalEvent<FSCccDamagedEvent>(OnCccDamaged);
        SubscribeLocalEvent<FSStructureDestroyedEvent>(OnStructureDestroyed);
        SubscribeLocalEvent<FSAllyHealedEvent>(OnAllyHealed);
        SubscribeLocalEvent<FSPlayerHurtEvent>(OnPlayerHurt);
        SubscribeLocalEvent<FSWaveEnemySpawnedEvent>(OnEnemySpawned);
        SubscribeLocalEvent<FSCreditsSpentEvent>(OnCreditsSpent);
    }

    private void SetUp(ActiveObjective current)
    {
        switch (current.Prototype.Kind)
        {
            case FSWaveObjectiveKind.HoldTheLine:
                var query = EntityQueryEnumerator<FinalStandCCCComponent, DamageableComponent>();
                if (query.MoveNext(out var ccc, out _, out var damageable))
                {
                    current.Ccc = ccc;
                    current.CccStartDamage = damageable.TotalDamage.Float();
                }
                break;

            case FSWaveObjectiveKind.Headhunter:
                var candidates = _waveRule.AliveEnemies()
                    .Where(e => !_mobState.IsDead(e))
                    .OrderByDescending(e => HasComp<FSSpecialZombieComponent>(e))
                    .ToList();
                if (candidates.Count > 0)
                    Mark(current, candidates[0], TargetLabel);
                break;

            case FSWaveObjectiveKind.Bounty:
                if (current.Prototype.Spawn is { } bountyProto)
                {
                    for (var i = 0; i < current.Target; i++)
                    {
                        if (_waveRule.SpawnExtraEnemy(bountyProto) is { } bounty)
                            Mark(current, bounty, BountyLabel, BountyTint);
                    }
                }
                current.Target = Math.Max(1, current.Marked.Count);
                break;

            case FSWaveObjectiveKind.SupplyRun:
                if (current.Prototype.Spawn is { } crateProto && _waveRule.TryPickSpawnerCoords(out var coords))
                {
                    var crate = Spawn(crateProto, coords);
                    Mark(current, crate, SupplyLabel);
                    current.Supply = crate;
                }
                current.Progress = SupplyDistance(current);
                break;

            case FSWaveObjectiveKind.Untouchable:
                current.Target = FSWaveObjectiveRules.UntouchedNeeded(current.Players);
                current.Progress = current.Players;
                break;
        }
    }

    private void TearDown(ActiveObjective current, bool completed)
    {
        foreach (var marked in current.Marked)
        {
            if (TerminatingOrDeleted(marked))
                continue;

            if (current.Prototype.Kind == FSWaveObjectiveKind.Bounty && !completed && !_mobState.IsDead(marked))
                QueueDel(marked);
            else
                RemComp<FSObjectiveMarkerComponent>(marked);
        }
        current.Marked.Clear();

        if (current.Supply is { } supply && !TerminatingOrDeleted(supply))
            QueueDel(supply);
        current.Supply = null;
    }

    private void Mark(ActiveObjective current, EntityUid uid, LocId label, Color? tint = null)
    {
        var marker = EnsureComp<FSObjectiveMarkerComponent>(uid);
        marker.Label = label;
        marker.Tint = tint;
        Dirty(uid, marker);
        current.Marked.Add(uid);
    }

    private void UpdateKinds(ActiveObjective current)
    {
        if (current.Prototype.Kind != FSWaveObjectiveKind.SupplyRun || _timing.CurTime < _nextSupplyCheck)
            return;

        _nextSupplyCheck = _timing.CurTime + SupplyCheckInterval;
        if (current.Supply is not { } supply || TerminatingOrDeleted(supply))
        {
            Fail();
            return;
        }

        var distance = SupplyDistance(current);
        if (distance <= SupplyDeliveryRange)
        {
            Complete();
            return;
        }

        if (distance != current.Progress)
        {
            current.Progress = distance;
            SendState(Filter.Broadcast());
        }
    }

    private int SupplyDistance(ActiveObjective current)
    {
        if (current.Supply is not { } supply || TerminatingOrDeleted(supply))
            return 0;

        var query = EntityQueryEnumerator<FinalStandCCCComponent, TransformComponent>();
        if (!query.MoveNext(out _, out _, out var cccXform))
            return 0;

        var crate = _transform.GetMapCoordinates(supply);
        var ccc = _transform.GetMapCoordinates(cccXform);
        return crate.MapId != ccc.MapId ? int.MaxValue : (int) MathF.Ceiling((crate.Position - ccc.Position).Length());
    }

    private void OnEnemySpawned(ref FSWaveEnemySpawnedEvent args)
    {
        if (Current is { Result: FSWaveObjectiveResult.InProgress, Prototype.Kind: FSWaveObjectiveKind.Headhunter } current
            && current.Marked.Count == 0)
        {
            Mark(current, args.Enemy, TargetLabel);
            SendState(Filter.Broadcast());
        }
    }

    private void OnEnemyDiedKinds(ActiveObjective current, FSWaveEnemyDiedEvent args)
    {
        switch (current.Prototype.Kind)
        {
            case FSWaveObjectiveKind.Headhunter when current.Marked.Contains(args.Enemy):
                current.Progress = 1;
                Complete();
                return;

            case FSWaveObjectiveKind.Bounty when current.Marked.Contains(args.Enemy):
                current.Progress++;
                if (current.Progress >= current.Target)
                    Complete();
                else
                    SendState(Filter.Broadcast());
                return;

            case FSWaveObjectiveKind.MeleeKills when IsMeleeKill(args.Killer):
                current.Progress++;
                if (current.Progress >= current.Target)
                    Complete();
                else
                    SendState(Filter.Broadcast());
                return;
        }
    }

    private bool IsMeleeKill(EntityUid? killer)
    {
        if (killer is not { } player || !_mind.TryGetMind(player, out _, out var mind) || mind.UserId == null)
            return false;

        return _hands.GetActiveItem(player) is not { } held || !HasComp<GunComponent>(held);
    }

    private void OnCccDamaged(ref FSCccDamagedEvent args)
    {
        if (Current is not { Result: FSWaveObjectiveResult.InProgress, Prototype.Kind: FSWaveObjectiveKind.HoldTheLine } current
            || current.Ccc != args.Ccc
            || !TryComp<DamageableComponent>(args.Ccc, out var damageable)
            || !TryComp<FinalStandCCCComponent>(args.Ccc, out var ccc))
            return;

        var lost = (int) MathF.Floor(FSWaveObjectiveRules.PercentLost(damageable.TotalDamage.Float() - current.CccStartDamage, ccc.MaxHealth));
        if (lost == current.Progress)
            return;

        current.Progress = lost;
        if (lost >= current.Target)
            Fail();
        else
            SendState(Filter.Broadcast());
    }

    private void OnStructureDestroyed(ref FSStructureDestroyedEvent args)
    {
        if (Current is { Result: FSWaveObjectiveResult.InProgress, Prototype.Kind: FSWaveObjectiveKind.Fortress })
            Fail();
    }

    private void OnAllyHealed(ref FSAllyHealedEvent args)
    {
        if (Current is not { Result: FSWaveObjectiveResult.InProgress, Prototype.Kind: FSWaveObjectiveKind.HealQuota } current
            || args.Amount <= 0f)
            return;

        var before = current.Progress;
        current.Progress = Math.Min(current.Target, (int) MathF.Floor(current.Progress + args.Amount));
        if (current.Progress >= current.Target)
            Complete();
        else if (current.Progress != before)
            SendState(Filter.Broadcast());
    }

    private void OnPlayerHurt(ref FSPlayerHurtEvent args)
    {
        if (Current is not { Result: FSWaveObjectiveResult.InProgress, Prototype.Kind: FSWaveObjectiveKind.Untouchable } current
            || !TryComp<ActorComponent>(args.Player, out var actor)
            || !current.Hurt.Add(actor.PlayerSession.UserId))
            return;

        current.Progress = Math.Max(0, current.Players - current.Hurt.Count);
        if (current.Progress < current.Target)
            Fail();
        else
            SendState(Filter.Broadcast());
    }

    private void OnCreditsSpent(ref FSCreditsSpentEvent args)
    {
        if (Current is { Result: FSWaveObjectiveResult.InProgress, Prototype.Kind: FSWaveObjectiveKind.NoSpending })
            Fail();
    }
}
