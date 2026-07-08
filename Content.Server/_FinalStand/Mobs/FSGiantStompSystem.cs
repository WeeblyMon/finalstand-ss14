using System.Numerics;
using Content.Server._FinalStand.Upgrades.Effects;
using Content.Server.Explosion.EntitySystems;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSGiantStompSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly KnockbackUpgradeSystem _knockback = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string StompExplosionProto = "FSGiantStompExplosion";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSGiantStompComponent, MobStateChangedEvent>(OnGiantDied);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSGiantStompComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_mobState.IsAlive(uid))
                continue;

            comp.StompAccumulator += frameTime;
            if (comp.StompAccumulator < comp.StompCooldown)
                continue;

            if (TryPerformStomp(uid, comp))
                comp.StompAccumulator = 0f;
        }
    }

    private bool TryPerformStomp(EntityUid uid, FSGiantStompComponent comp)
    {
        var giantPos = _transform.GetWorldPosition(uid);
        var mapId = Transform(uid).MapID;
        var epicenter = new MapCoordinates(giantPos, mapId);
        var targets = new HashSet<Entity<ActorComponent>>();
        _lookup.GetEntitiesInRange<ActorComponent>(epicenter, comp.StompRadius, targets);

        var validTargets = new List<EntityUid>();
        foreach (var (targetUid, _) in targets)
        {
            if (_mobState.IsAlive(targetUid))
                validTargets.Add(targetUid);
        }

        if (validTargets.Count == 0)
            return false;

        // TODO(finalstand): tune stomp damage — 3x FSZombieNormal melee (Slash: 10) = Blunt: 30
        var stompDamage = new DamageSpecifier();
        stompDamage.DamageDict["Blunt"] = FixedPoint2.New(30);

        foreach (var targetUid in validTargets)
        {
            _damageable.TryChangeDamage(targetUid, stompDamage, ignoreResistances: false, origin: uid);
            _knockback.ApplyKnockback(targetUid, uid, 3);
        }

        var shakeSet = new HashSet<Entity<ActorComponent>>();
        _lookup.GetEntitiesInRange<ActorComponent>(epicenter, comp.ShakeRadius, shakeSet);
        foreach (var (shakeUid, _) in shakeSet)
        {
            var shakePos = _transform.GetWorldPosition(shakeUid);
            var dist = Vector2.Distance(giantPos, shakePos);

            var shakeMag = 0.6f * MathF.Max(0f, 1f - dist / comp.ShakeRadius);
            if (shakeMag < 0.01f)
                continue;

            var shakeDir = shakePos - giantPos;
            if (shakeDir == Vector2.Zero)
                shakeDir = new Vector2(1f, 0f);
            _recoil.KickCamera(shakeUid, Vector2.Normalize(shakeDir) * shakeMag);
        }

        // Visual-only explosion at stomp location — tileBreakScale: 0 / maxTileBreak: 0 = no structural damage.
        _explosion.QueueExplosion(
            epicenter,
            StompExplosionProto,
            totalIntensity: 60f,
            slope: 2f,
            maxTileIntensity: 4f,
            cause: uid,
            tileBreakScale: 0f,
            maxTileBreak: 0,
            canCreateVacuum: false,
            addLog: false);

        if (comp.StompSound != null)
            _audio.PlayPvs(comp.StompSound, uid);

        return true;
    }

    private void OnGiantDied(EntityUid uid, FSGiantStompComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        Spawn("ArtifactFragment", Transform(uid).Coordinates);
    }
}
