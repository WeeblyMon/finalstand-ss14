using System.Linq;
using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class BeamChainingUpgradeSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const float ChainRange = 5f;
    private const string ChainBeamSegment = "FSChainBeamSegment";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(ev.Weapon.Value, out var state) || state.BeamChainTargets <= 0)
            return;

        var targetPos = _transform.GetWorldPosition(ev.Target);
        var mapId = Transform(ev.Target).MapID;

        var nearby = new HashSet<Entity<WaveSpawnedTagComponent>>();
        _lookup.GetEntitiesInRange<WaveSpawnedTagComponent>(new MapCoordinates(targetPos, mapId), ChainRange, nearby);

        var primaryTarget = ev.Target;
        var chainDamage   = ev.Damage;
        var shooter       = ev.Shooter;

        var candidates = nearby
            .Where(e => e.Owner != primaryTarget && !_mobState.IsDead(e.Owner))
            .OrderBy(e => (_transform.GetWorldPosition(e.Owner) - targetPos).LengthSquared())
            .Take(state.BeamChainTargets);

        foreach (var chainTarget in candidates)
        {
            _damageable.TryChangeDamage(chainTarget.Owner, chainDamage, ignoreResistances: false, origin: shooter);
            DrawChainBeam(primaryTarget, chainTarget.Owner);
        }
    }

    // Physics-free sprites only — BeamSystem spawns physics entities that cancel primary damage.
    private void DrawChainBeam(EntityUid from, EntityUid to)
    {
        var fromMapCoords = _transform.GetMapCoordinates(from);
        var toMapCoords   = _transform.GetMapCoordinates(to);

        if (fromMapCoords.MapId != toMapCoords.MapId)
            return;

        var dir = toMapCoords.Position - fromMapCoords.Position;
        if (dir.LengthSquared() < 0.01f)
            return;

        var distance   = dir.Length();
        var normalized = Vector2.Normalize(dir);
        var angle      = dir.ToAngle();
        var segCount   = Math.Max(1, (int)Math.Ceiling(distance));

        for (var i = 0; i < segCount; i++)
        {
            var pos = new MapCoordinates(fromMapCoords.Position + normalized * i, fromMapCoords.MapId);
            var seg = Spawn(ChainBeamSegment, pos);
            _transform.SetWorldRotation(seg, angle);
        }
    }
}
