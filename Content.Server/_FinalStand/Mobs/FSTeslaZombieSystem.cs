using System.Numerics;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Utility;
using Content.Server._FinalStand.Spawners;
using Content.Server._FinalStand.CCC;
using Content.Server.NPC.HTN;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Mobs;

public sealed partial class FSTeslaZombieSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private PointLightSystem _pointLight = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private FSTargetAcquisitionSystem _targeting = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;

    private readonly ObjectPool<HashSet<Entity<FSFriendlyFireComponent>>> _chainSetPool =
        new DefaultObjectPool<HashSet<Entity<FSFriendlyFireComponent>>>(
            new SetPolicy<Entity<FSFriendlyFireComponent>>());

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

        if (_targeting.AcquireTarget(uid, comp.DetectionRange) is not { } target)
            return;

        var myPos = _transform.GetMapCoordinates(uid);
        var dir = _transform.GetMapCoordinates(target).Position - myPos.Position;
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

        if (!_examine.InRangeUnOccluded(uid, primaryTarget.Value, comp.DetectionRange, null))
            return;

        var primaryDmg = new DamageSpecifier();
        primaryDmg.DamageDict["Shock"] = comp.PrimaryDamageShock;
        _damageable.TryChangeDamage(primaryTarget.Value, primaryDmg, ignoreResistances: false, origin: uid);

        DrawTeslaBeam(uid, primaryTarget.Value);
        Spawn(HitEffect, new EntityCoordinates(primaryTarget.Value, Vector2.Zero));

        var chainDmg = new DamageSpecifier();
        chainDmg.DamageDict["Shock"] = comp.ChainDamageShock;

        var chainCount = 0;
        var chainCandidates = _chainSetPool.Get();
        _lookup.GetEntitiesInRange<FSFriendlyFireComponent>(primaryMapPos, comp.ChainRange, chainCandidates);

        foreach (var (chainUid, _) in chainCandidates)
        {
            if (chainCount >= comp.MaxChainTargets) break;
            if (chainUid == primaryTarget.Value) continue;
            if (!TryComp<MobStateComponent>(chainUid, out var chainMobState)) continue;
            if (chainMobState.CurrentState != MobState.Alive) continue;
            if (!_examine.InRangeUnOccluded(primaryTarget.Value, chainUid, comp.ChainRange, null)) continue;

            _damageable.TryChangeDamage(chainUid, chainDmg, ignoreResistances: false, origin: uid);
            DrawTeslaBeam(primaryTarget.Value, chainUid);
            Spawn(HitEffect, new EntityCoordinates(chainUid, Vector2.Zero));
            chainCount++;
        }
        _chainSetPool.Return(chainCandidates);

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
