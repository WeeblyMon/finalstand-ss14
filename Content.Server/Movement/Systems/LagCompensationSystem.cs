using System.Numerics;
using Content.Server.Movement.Components;
using Content.Shared.CCVar;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Movement.Systems;

/// <summary>
/// Stores a buffer of previous positions of the relevant entity.
/// Can be used to check the entity's position at a recent point in time.
/// </summary>
public sealed partial class LagCompensationSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IConfigurationManager _cfg = default!; // fs
    [Dependency] private IPlayerManager _players = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    // I figured 500 ping is max, so 1.5 is 750.
    // Max ping I've had is 350ms from aus to spain.
    public static readonly TimeSpan BufferTime = TimeSpan.FromMilliseconds(750);

    private readonly Dictionary<NetUserId, GameTick> _viewTicks = new(); // fs
    private bool _enabled;
    private int _maxRewindMs;
    private int _rewindSlackMs; // FINALSTAND

    public override void Initialize()
    {
        base.Initialize();
        Log.Level = LogLevel.Info;
        SubscribeLocalEvent<LagCompensationComponent, MoveEvent>(OnLagMove);

        SubscribeNetworkEvent<RequestShootEvent>(OnShootRequest, before: [typeof(SharedGunSystem)]); // fs
        SubscribeNetworkEvent<LightAttackEvent>(OnAttack, before: [typeof(SharedMeleeWeaponSystem)]);
        SubscribeNetworkEvent<HeavyAttackEvent>(OnAttack, before: [typeof(SharedMeleeWeaponSystem)]);
        SubscribeNetworkEvent<DisarmAttackEvent>(OnAttack, before: [typeof(SharedMeleeWeaponSystem)]);

        Subs.CVar(_cfg, CCVars.FSLagCompensation, v => _enabled = v, true);
        Subs.CVar(_cfg, CCVars.FSLagCompensationMaxRewindMs, v => _maxRewindMs = v, true);
        Subs.CVar(_cfg, CCVars.FSLagCompensationRewindSlackMs, v => _rewindSlackMs = v, true); // FINALSTAND

        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (args.NewStatus == SessionStatus.Disconnected)
            _viewTicks.Remove(args.Session.UserId);
    }

    private void OnShootRequest(RequestShootEvent ev, EntitySessionEventArgs args)
    {
        SetViewTick(args.SenderSession, ev.ViewTick);
    }

    private void OnAttack(AttackEvent ev, EntitySessionEventArgs args)
    {
        SetViewTick(args.SenderSession, ev.ViewTick);
    }

    public void SetViewTick(ICommonSession session, GameTick viewTick)
    {
        if (viewTick == GameTick.Zero || viewTick > _timing.CurTick)
            return;

        _viewTicks[session.UserId] = viewTick;
    }

    public double GetRewindTicks(ICommonSession? session)
    {
        if (!_enabled || session == null || !_viewTicks.TryGetValue(session.UserId, out var viewTick))
            return 0;

        var rewind = _timing.CurTick.Value - viewTick.Value + 0.5;
        // FINALSTAND: the view tick is the client's word, so cap it at what its measured latency explains.
        // Otherwise a low-ping client could always claim the full window and shoot where enemies used to be.
        var maxMs = Math.Min(_maxRewindMs, session.Channel.Ping + _rewindSlackMs);
        var max = maxMs / 1000.0 * _timing.TickRate;
        return Math.Clamp(rewind, 0, max);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var historyTicks = (uint) Math.Ceiling(BufferTime.TotalSeconds * _timing.TickRate) + 2; // fs
        if (_timing.CurTick.Value <= historyTicks)
            return;

        var cutoff = _timing.CurTick.Value - historyTicks; // fs
        var query = AllEntityQuery<LagCompensationComponent>();

        while (query.MoveNext(out var comp))
        {
            var positions = comp.Positions; // fs
            var remove = 0;
            while (remove + 1 < positions.Count && positions[remove + 1].Tick.Value <= cutoff)
            {
                remove++; // fs
            }

            if (remove > 0) // fs
                positions.RemoveRange(0, remove);
        }
    }

    private void OnLagMove(EntityUid uid, LagCompensationComponent component, ref MoveEvent args)
    {
        if (!args.NewPosition.EntityId.IsValid())
            return; // probably being sent to nullspace for deletion.

        var tick = _timing.CurTick; // fs
        var positions = component.Positions;

        if (positions.Count == 0 && args.OldPosition.EntityId.IsValid() && tick.Value > 0)
            positions.Add((new GameTick(tick.Value - 1), args.OldPosition, args.OldRotation));

        if (positions.Count > 0 && positions[^1].Tick == tick)
            positions[^1] = (tick, args.NewPosition, args.NewRotation);
        else
            positions.Add((tick, args.NewPosition, args.NewRotation));
    }

    public (EntityCoordinates Coordinates, Angle Angle) GetCoordinatesAngle(EntityUid uid, ICommonSession? pSession,
        TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform))
            return (EntityCoordinates.Invalid, Angle.Zero);

        var rewind = GetRewindTicks(pSession); // fs
        if (rewind <= 0 || !TryComp<LagCompensationComponent>(uid, out var lag))
            return (xform.Coordinates, xform.LocalRotation);

        return Sample(lag, _timing.CurTick.Value - rewind, xform); // fs
    }

    public MapCoordinates GetPastMapCoordinates(EntityUid uid, double tick, LagCompensationComponent? lag = null, TransformComponent? xform = null) // fs
    {
        if (!Resolve(uid, ref xform))
            return MapCoordinates.Nullspace;

        if (!Resolve(uid, ref lag, false) || tick >= _timing.CurTick.Value) // fs
            return _transform.GetMapCoordinates(uid, xform);

        return _transform.ToMapCoordinates(Sample(lag, tick, xform).Coordinates); // fs
    }

    private (EntityCoordinates Coordinates, Angle Angle) Sample(LagCompensationComponent lag, double tick, TransformComponent xform)
    {
        var positions = lag.Positions;
        if (positions.Count == 0)
            return (xform.Coordinates, xform.LocalRotation);

        var floor = Math.Floor(tick);
        var fraction = (float) (tick - floor);
        var from = At(positions, floor);
        var to = At(positions, floor + 1);

        if (fraction <= 0f || from.Coordinates.EntityId != to.Coordinates.EntityId)
            return fraction < 0.5f ? (from.Coordinates, from.Angle) : (to.Coordinates, to.Angle);

        var position = Vector2.Lerp(from.Coordinates.Position, to.Coordinates.Position, fraction);
        return (new EntityCoordinates(from.Coordinates.EntityId, position), from.Angle);
    }

    private static (GameTick Tick, EntityCoordinates Coordinates, Angle Angle) At(
        List<(GameTick Tick, EntityCoordinates Coordinates, Angle Angle)> positions,
        double tick)
    {
        for (var i = positions.Count - 1; i >= 0; i--)
        {
            if (positions[i].Tick.Value <= tick) // fs
                return positions[i];
        }

        return positions[0]; // fs
    }

    public Angle GetAngle(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (_, angle) = GetCoordinatesAngle(uid, session, xform);
        return angle;
    }

    public EntityCoordinates GetCoordinates(EntityUid uid, ICommonSession? session, TransformComponent? xform = null)
    {
        var (coordinates, _) = GetCoordinatesAngle(uid, session, xform);
        return coordinates;
    }
}
