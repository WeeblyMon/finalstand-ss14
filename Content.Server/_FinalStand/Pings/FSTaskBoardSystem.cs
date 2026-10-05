// Request pings become tasks for one department. Tasks can be claimed, close themselves when the job is done, and expire.
// Badly damaged player-built fortifications raise their own repair task, a few at most.
using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Repair;
using Content.Shared._FinalStand.Departments;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Mech;
using Content.Shared._FinalStand.Pings;
using Content.Shared._FinalStand.Repair;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Pings;

public sealed partial class FSTaskBoardSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private ISharedPlayerManager _players = default!;
    [Dependency] private FSStructureHealthSystem _health = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan UnclaimedLife = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ClaimedLife = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan AutoTaskWindow = TimeSpan.FromSeconds(60);
    private const int AutoTasksPerWindow = 2;
    private const float AutoRepairBelow = 0.3f;
    private const float RepairedAbove = 0.9f;
    private const float ClaimRange = 4f;
    private const float ArrivedRange = 1.5f;

    private readonly List<BoardTask> _board = new();
    private readonly HashSet<EntityUid> _autoRaised = new();
    private readonly Queue<TimeSpan> _autoTimes = new();
    private TimeSpan _next;
    private bool _dirty;
    private int _nextId;

    private sealed class BoardTask
    {
        public int Id;
        public FSPingType Type;
        public FSPingAudience Audience;
        public EntityCoordinates Coords;
        public EntityUid? Target;
        public EntityUid Requester;
        public string RequesterName = string.Empty;
        public string Label = string.Empty;
        public string? Build;
        public EntityUid? Claimer;
        public string? ClaimerName;
        public TimeSpan Created;
        public int BayStage;
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<PlayerAttachedEvent>(_ => _dirty = true);
    }

    private void OnWaveEnded(ref WaveEndedEvent ev)
    {
        _autoRaised.Clear();
    }

    public void Open(FSPingType type, FSPingAudience audience, EntityCoordinates coords, EntityUid? target,
        EntityUid requester, string label, string? build = null)
    {
        var existing = _board.FirstOrDefault(t => t.Type == type
            && (target != null ? t.Target == target : t.Coords.TryDistance(EntityManager, coords, out var d) && d < 0.5f));
        if (existing != null)
        {
            existing.Created = _timing.CurTime;
            existing.Label = label;
            _dirty = true;
            return;
        }

        _board.Add(new BoardTask
        {
            Id = ++_nextId,
            Type = type,
            Audience = audience,
            Coords = coords,
            Target = target,
            Requester = requester,
            RequesterName = Name(requester),
            Label = label,
            Build = build,
            Created = _timing.CurTime,
            BayStage = target is { } bay && TryComp<FSMechBayComponent>(bay, out var bayComp) ? bayComp.Installed.Count : -1,
        });
        _dirty = true;
    }

    public IEnumerable<(FSPingType Type, EntityUid? Target, EntityUid? Claimer)> Snapshot()
    {
        foreach (var task in _board)
            yield return (task.Type, task.Target, task.Claimer);
    }

    public void Claim(EntityUid claimer, EntityCoordinates coords, EntityUid? target)
    {
        BoardTask? best = null;
        var bestDistance = ClaimRange;
        foreach (var task in _board)
        {
            if (task.Claimer != null || !InAudience(claimer, task.Audience))
                continue;

            if (target != null && task.Target == target)
            {
                best = task;
                break;
            }

            if (!task.Coords.TryDistance(EntityManager, coords, out var distance) || distance >= bestDistance)
                continue;

            bestDistance = distance;
            best = task;
        }

        if (best == null)
            return;

        best.Claimer = claimer;
        best.ClaimerName = Name(claimer);
        best.Created = _timing.CurTime;
        _dirty = true;
    }

    public bool InAudience(EntityUid uid, FSPingAudience audience)
    {
        return FSPingAudiences.Includes(audience, CompOrNull<FSDepartmentAccessComponent>(uid));
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + Interval;
        RaiseAutoTasks();

        for (var i = _board.Count - 1; i >= 0; i--)
        {
            var task = _board[i];
            var life = task.Claimer != null ? ClaimedLife : UnclaimedLife;
            if (_timing.CurTime - task.Created < life && !Done(task))
                continue;

            _board.RemoveAt(i);
            _dirty = true;
        }

        if (_dirty)
            SendBoards();
    }

    private bool Done(BoardTask task)
    {
        if (task.Target is { } target && TerminatingOrDeleted(target))
            return true;

        switch (task.Type)
        {
            case FSPingType.Repair:
                return task.Target is { } structure && (_health.Fraction(structure) ?? 1f) >= RepairedAbove;
            case FSPingType.Revive:
                return task.Target is { } patient && !_mobState.IsIncapacitated(patient);
            case FSPingType.BuildHere:
                return BuiltOn(task.Coords);
            case FSPingType.Bay:
                return task.Target is { } bay && TryComp<FSMechBayComponent>(bay, out var comp) && comp.Installed.Count != task.BayStage;
            case FSPingType.NeedChems:
                return TerminatingOrDeleted(task.Requester)
                       || task.Claimer is { } medic && _transform.InRange(medic, task.Requester, ArrivedRange);
            default:
                return false;
        }
    }

    private bool BuiltOn(EntityCoordinates coords)
    {
        if (_transform.GetGrid(coords) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var anchored = _map.GetAnchoredEntitiesEnumerator(gridUid, grid, _map.TileIndicesFor(gridUid, grid, coords));
        while (anchored.MoveNext(out var uid))
        {
            if (HasComp<FSDeployedByComponent>(uid))
                return true;
        }

        return false;
    }

    private void RaiseAutoTasks()
    {
        while (_autoTimes.Count > 0 && _timing.CurTime - _autoTimes.Peek() > AutoTaskWindow)
            _autoTimes.Dequeue();

        if (_autoTimes.Count >= AutoTasksPerWindow)
            return;

        var query = EntityQueryEnumerator<FSDeployedByComponent, FSStructureHealthComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var deployedBy, out _, out var xform))
        {
            if (_autoRaised.Contains(uid) || _health.Fraction(uid) is not { } health || health >= AutoRepairBelow)
                continue;

            _autoRaised.Add(uid);
            _autoTimes.Enqueue(_timing.CurTime);
            var requester = deployedBy.DeployedBy is { } builder && Exists(builder) ? builder : uid;
            Open(FSPingType.Repair, FSPingAudience.Engineering, xform.Coordinates, uid, requester,
                Loc.GetString("fs-ping-repair", ("name", Name(uid)), ("percent", (int) MathF.Round(health * 100f))));
            if (_autoTimes.Count >= AutoTasksPerWindow)
                return;
        }
    }

    private void SendBoards()
    {
        _dirty = false;
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } player)
                continue;

            var list = new List<FSTaskEntry>();
            foreach (var task in _board)
            {
                if (task.Audience == FSPingAudience.Everyone || !InAudience(player, task.Audience))
                    continue;

                list.Add(new FSTaskEntry
                {
                    Id = task.Id,
                    Type = task.Type,
                    Label = task.Label,
                    Coords = GetNetCoordinates(task.Coords),
                    Target = task.Target is { } target ? GetNetEntity(target) : null,
                    ClaimedBy = task.ClaimerName,
                    Requester = task.RequesterName,
                });
            }

            RaiseNetworkEvent(new FSTaskBoardEvent(list), session);
        }
    }
}
