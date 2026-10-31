using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Alert;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Fortifications;

[RegisterComponent]
[Access(typeof(FSFiringStepSystem))]
public sealed partial class FSFiringStepComponent : Component
{
    [DataField]
    public float Spread = 0.7f;

    [DataField]
    public float MeleeDamage = 0.85f;
}

public sealed partial class FSFiringStepSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private AlertsSystem _alerts = default!;
    [Dependency] private IGameTiming _timing = default!;

    private static readonly ProtoId<AlertPrototype> Alert = "FSFiringStep";
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.25);
    private const float MeleeRange = 1.5f;

    private readonly Dictionary<EntityUid, FSFiringStepComponent> _onStep = new();
    private readonly List<(EntityUid Player, EntityUid? Gun)> _offStep = new();
    private readonly Dictionary<(EntityUid Grid, Vector2i Tile), FSFiringStepComponent> _stepTiles = new();
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSFiringStepBuffComponent, DamageModifyEvent>(OnDamageModify);
    }

    public void ApplyGunModifiers(EntityUid holder, ref GunRefreshModifiersEvent args)
    {
        if (!TryComp<FSFiringStepBuffComponent>(holder, out var buff))
            return;

        args.MinAngle *= buff.Spread;
        args.MaxAngle *= buff.Spread;
        args.AngleIncrease *= buff.Spread;
    }

    private void OnDamageModify(Entity<FSFiringStepBuffComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } origin
            || !_transform.InRange(origin, ent.Owner, MeleeRange))
            return;

        args.Damage *= ent.Comp.MeleeDamage;
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _next)
            return;

        _next = _timing.CurTime + Interval;
        _onStep.Clear();
        _stepTiles.Clear();

        var steps = EntityQueryEnumerator<FSFiringStepComponent, TransformComponent>();
        while (steps.MoveNext(out var step, out var xform))
        {
            if (!xform.Anchored || xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
                continue;

            var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
            _stepTiles[(gridUid, tile)] = step;
            _stepTiles[(gridUid, tile - xform.LocalRotation.GetCardinalDir().ToIntVec())] = step;
        }

        if (_stepTiles.Count > 0)
        {
            var players = EntityQueryEnumerator<FSFriendlyFireComponent, TransformComponent>();
            while (players.MoveNext(out var uid, out _, out var xform))
            {
                if (xform.GridUid is { } gridUid
                    && TryComp<MapGridComponent>(gridUid, out var grid)
                    && _stepTiles.TryGetValue((gridUid, _map.TileIndicesFor(gridUid, grid, xform.Coordinates)), out var step))
                    _onStep[uid] = step;
            }
        }

        _offStep.Clear();
        var buffed = EntityQueryEnumerator<FSFiringStepBuffComponent>();
        while (buffed.MoveNext(out var uid, out var buff))
        {
            if (!_onStep.ContainsKey(uid))
                _offStep.Add((uid, buff.Gun));
        }

        foreach (var (player, gun) in _offStep)
        {
            RemComp<FSFiringStepBuffComponent>(player);
            _alerts.ClearAlert(player, Alert);
            Refresh(gun);
        }

        foreach (var (player, step) in _onStep)
        {
            if (!TryComp<FSFiringStepBuffComponent>(player, out var buff))
            {
                buff = AddComp<FSFiringStepBuffComponent>(player);
                _alerts.ShowAlert(player, Alert);
            }

            if (!buff.Spread.Equals(step.Spread) || !buff.MeleeDamage.Equals(step.MeleeDamage))
            {
                buff.Spread = step.Spread;
                buff.MeleeDamage = step.MeleeDamage;
                Dirty(player, buff);
            }

            _hands.TryGetActiveItem(player, out var held);
            var gun = HasComp<GunComponent>(held) ? held : null;
            if (gun == buff.Gun)
                continue;

            var old = buff.Gun;
            buff.Gun = gun;
            Refresh(old);
            Refresh(gun);
        }
    }

    private void Refresh(EntityUid? gun)
    {
        if (gun != null && !TerminatingOrDeleted(gun.Value))
            _gun.RefreshModifiers(gun.Value);
    }
}
