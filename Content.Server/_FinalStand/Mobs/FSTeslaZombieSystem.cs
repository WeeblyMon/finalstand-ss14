using System.Numerics;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.Station;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSTeslaZombieSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string BeamSegment = "FSTeslaBeamSegment";
    private const string HitEffect = "FSTeslaHitEffect";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSTeslaZombieComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, FSTeslaZombieComponent comp, ComponentShutdown args)
    {
        if (!comp.IsFiring)
            return;
        _pointLight.SetEnabled(uid, false);
        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CanMove = true;
            Dirty(uid, mover);
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSTeslaZombieComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.IsFiring)
                UpdateFiring(uid, comp, frameTime);
            else
                UpdateIdle(uid, comp, frameTime);
        }
    }

    private void UpdateIdle(EntityUid uid, FSTeslaZombieComponent comp, float frameTime)
    {
        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Alive)
            return;

        if (comp.CooldownAccumulator > 0f)
        {
            comp.CooldownAccumulator -= frameTime;
            return;
        }

        var myPos = _transform.GetMapCoordinates(uid);

        var candidates = new HashSet<Entity<ActorComponent>>();
        _lookup.GetEntitiesInRange<ActorComponent>(myPos, comp.DetectionRange, candidates);

        EntityUid? target = null;
        float nearestDist = float.MaxValue;
        foreach (var (targetUid, _) in candidates)
        {
            if (HasComp<WaveSpawnedTagComponent>(targetUid)) continue;
            if (HasComp<GhostComponent>(targetUid)) continue;
            var dist = Vector2.Distance(myPos.Position, _transform.GetMapCoordinates(targetUid).Position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                target = targetUid;
            }
        }

        if (target == null)
        {
            var cccSet = new HashSet<Entity<FinalStandCCCComponent>>();
            _lookup.GetEntitiesInRange<FinalStandCCCComponent>(myPos, comp.DetectionRange, cccSet);
            foreach (var (cccUid, _) in cccSet)
            {
                target = cccUid;
                break;
            }
            if (target == null)
                return;
        }

        var dir = _transform.GetMapCoordinates(target.Value).Position - myPos.Position;
        if (dir != Vector2.Zero)
            _transform.SetLocalRotation(uid, new Angle(dir) - MathF.PI / 2f);

        comp.IsFiring = true;
        comp.Target = target;
        comp.FireAccumulator = 0f;
        Dirty(uid, comp);

        _pointLight.SetEnabled(uid, true);

        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CanMove = false;
            Dirty(uid, mover);
        }
    }

    private void UpdateFiring(EntityUid uid, FSTeslaZombieComponent comp, float frameTime)
    {
        if (TryComp<MobStateComponent>(uid, out var mobState) && mobState.CurrentState != MobState.Alive)
        {
            StopFiring(uid, comp);
            return;
        }

        comp.FireAccumulator += frameTime;

        if (comp.FireAccumulator >= comp.FireDuration)
        {
            ShootTeslaBeam(uid, comp);
            StopFiring(uid, comp);
        }
    }

    private void ShootTeslaBeam(EntityUid uid, FSTeslaZombieComponent comp)
    {
        EntityUid? primaryTarget = null;

        if (comp.Target.HasValue && !TerminatingOrDeleted(comp.Target.Value))
        {
            if (!TryComp<MobStateComponent>(comp.Target.Value, out var ts) || ts.CurrentState == MobState.Alive)
                primaryTarget = comp.Target.Value;
        }

        if (primaryTarget == null)
            return;

        var myMapPos = _transform.GetMapCoordinates(uid);
        var primaryMapPos = _transform.GetMapCoordinates(primaryTarget.Value);

        if (primaryMapPos.MapId != myMapPos.MapId ||
            Vector2.Distance(myMapPos.Position, primaryMapPos.Position) > comp.DetectionRange)
            return;

        var primaryDmg = new DamageSpecifier();
        primaryDmg.DamageDict["Shock"] = comp.PrimaryDamageShock;
        _damageable.TryChangeDamage(primaryTarget.Value, primaryDmg, ignoreResistances: false, origin: uid);

        DrawTeslaBeam(uid, primaryTarget.Value);
        Spawn(HitEffect, new EntityCoordinates(primaryTarget.Value, Vector2.Zero));

        var chainDmg = new DamageSpecifier();
        chainDmg.DamageDict["Shock"] = comp.ChainDamageShock;

        var chainCount = 0;
        var chainQuery = EntityQueryEnumerator<FSFriendlyFireComponent, MobStateComponent>();
        while (chainQuery.MoveNext(out var chainUid, out _, out var chainMobState))
        {
            if (chainCount >= comp.MaxChainTargets) break;
            if (chainUid == primaryTarget.Value) continue;
            if (chainMobState.CurrentState != MobState.Alive) continue;

            var chainMapPos = _transform.GetMapCoordinates(chainUid);
            if (chainMapPos.MapId != primaryMapPos.MapId) continue;
            if (Vector2.Distance(chainMapPos.Position, primaryMapPos.Position) > comp.ChainRange) continue;

            _damageable.TryChangeDamage(chainUid, chainDmg, ignoreResistances: false, origin: uid);
            DrawTeslaBeam(primaryTarget.Value, chainUid);
            Spawn(HitEffect, new EntityCoordinates(chainUid, Vector2.Zero));
            chainCount++;
        }

        _audio.PlayPvs(comp.FireSound, uid);
    }

    private void DrawTeslaBeam(EntityUid from, EntityUid to)
    {
        var fromPos = _transform.GetMapCoordinates(from);
        var toPos = _transform.GetMapCoordinates(to);

        if (fromPos.MapId != toPos.MapId)
            return;

        var dir = toPos.Position - fromPos.Position;
        if (dir.LengthSquared() < 0.01f)
            return;

        var distance = dir.Length();
        var normalized = Vector2.Normalize(dir);
        var angle = dir.ToAngle();
        var segCount = Math.Max(1, (int)Math.Ceiling(distance));

        for (var i = 0; i < segCount; i++)
        {
            var pos = new MapCoordinates(fromPos.Position + normalized * i, fromPos.MapId);
            var seg = Spawn(BeamSegment, pos);
            _transform.SetWorldRotation(seg, angle);
        }
    }

    private void StopFiring(EntityUid uid, FSTeslaZombieComponent comp)
    {
        comp.IsFiring = false;
        comp.Target = null;
        comp.CooldownAccumulator = comp.AttackCooldown;
        Dirty(uid, comp);

        _pointLight.SetEnabled(uid, false);

        if (TryComp<InputMoverComponent>(uid, out var mover))
        {
            mover.CanMove = true;
            Dirty(uid, mover);
        }

        if (TryComp<HTNComponent>(uid, out var htn))
            _htn.Replan(htn);
    }
}
