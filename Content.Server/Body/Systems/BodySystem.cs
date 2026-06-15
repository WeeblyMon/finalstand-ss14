using System.Numerics;
using Content.Server.Ghost;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Gibbing.Events;
using Content.Shared.Humanoid;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Body.Systems;

public sealed partial class BodySystem : SharedBodySystem
{
    [Dependency] private readonly GhostSystem _ghostSystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyComponent, MoveInputEvent>(OnRelayMoveInput);
        SubscribeLocalEvent<BodyComponent, ApplyMetabolicMultiplierEvent>(OnApplyMetabolicMultiplier);
        SubscribeLocalEvent<BodyPartComponent, AttemptEntityGibEvent>(OnGibTorsoAttempt);
    }

    private void OnRelayMoveInput(Entity<BodyComponent> ent, ref MoveInputEvent args)
    {
        if ((args.Entity.Comp.HeldMoveButtons &
             (MoveButtons.Down | MoveButtons.Left | MoveButtons.Up | MoveButtons.Right)) == 0x0)
        {
            return;
        }

        if (_mobState.IsDead(ent) && _mindSystem.TryGetMind(ent, out var mindId, out var mind))
        {
            mind.TimeOfDeath ??= _gameTiming.RealTime;
            _ghostSystem.OnGhostAttempt(mindId, canReturnGlobal: true, mind: mind);
        }
    }

    private void OnApplyMetabolicMultiplier(
        Entity<BodyComponent> ent,
        ref ApplyMetabolicMultiplierEvent args)
    {
        foreach (var organ in GetBodyOrgans(ent, ent))
        {
            RaiseLocalEvent(organ.Id, ref args);
        }
    }

    public override HashSet<EntityUid> GibBody(
        EntityUid bodyId,
        bool gibOrgans = false,
        BodyComponent? body = null,
        bool launchGibs = true,
        Vector2? splatDirection = null,
        float splatModifier = 1,
        Angle splatCone = default,
        SoundSpecifier? gibSoundOverride = null,
        GibType gib = GibType.Gib,
        GibContentsOption contents = GibContentsOption.Drop,
        List<string>? allowedContainers = null,
        List<string>? excludedContainers = null)
    {
        if (!Resolve(bodyId, ref body, logMissing: false)
            || TerminatingOrDeleted(bodyId)
            || EntityManager.IsQueuedForDeletion(bodyId))
        {
            return new HashSet<EntityUid>();
        }

        var xform = Transform(bodyId);
        if (xform.MapUid is null)
            return new HashSet<EntityUid>();

        var gibs = base.GibBody(bodyId, gibOrgans, body, launchGibs: launchGibs, splatDirection: splatDirection,
            splatModifier: splatModifier, splatCone: splatCone, gib: gib, contents: contents,
            allowedContainers: allowedContainers, excludedContainers: excludedContainers);

        var ev = new BeingGibbedEvent(gibs);
        RaiseLocalEvent(bodyId, ref ev);

        if (launchGibs)
        {
            foreach (var giblet in gibs)
            {
                var impulse = GibletLaunchImpulse + _random.NextFloat(GibletLaunchImpulseVariance);
                var scatterVec = _random.NextAngle().ToVec() * impulse * splatModifier;
                _physics.ApplyLinearImpulse(giblet, scatterVec);
            }
        }

        QueueDel(bodyId);

        return gibs;
    }

    public override HashSet<EntityUid> GibPart(
        EntityUid partId,
        BodyPartComponent? part = null,
        bool launchGibs = true,
        Vector2? splatDirection = null,
        float splatModifier = 1,
        Angle splatCone = default,
        SoundSpecifier? gibSoundOverride = null)
    {
        if (!Resolve(partId, ref part, logMissing: false)
            || TerminatingOrDeleted(partId)
            || EntityManager.IsQueuedForDeletion(partId))
            return new HashSet<EntityUid>();

        if (Transform(partId).MapUid is null)
            return new HashSet<EntityUid>();

        var gibs = base.GibPart(partId, part, launchGibs: launchGibs,
            splatDirection: splatDirection, splatModifier: splatModifier, splatCone: splatCone);

        var ev = new BeingGibbedEvent(gibs);
        RaiseLocalEvent(partId, ref ev);

        if (gibs.Count > 0)
            QueueDel(partId);

        return gibs;
    }

    public override bool BurnPart(EntityUid partId, BodyPartComponent? part = null)
    {
        if (!Resolve(partId, ref part, logMissing: false)
            || TerminatingOrDeleted(partId)
            || EntityManager.IsQueuedForDeletion(partId))
            return false;

        return base.BurnPart(partId, part);
    }

    private void OnGibTorsoAttempt(Entity<BodyPartComponent> ent, ref AttemptEntityGibEvent args)
    {
        if (ent.Comp.PartType == BodyPartType.Chest)
            args.Cancelled = true;
    }
}
