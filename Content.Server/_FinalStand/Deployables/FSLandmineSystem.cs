using Content.Server._FinalStand.Perks;
﻿using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Explosion.EntitySystems;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Trigger;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Deployables;

public sealed class FSLandmineSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedExplosionSystem _explosion = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSImplosionSystem _implosion = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSLandmineComponent, FSDeployableDeployedEvent>(OnDeployed);
        SubscribeLocalEvent<FSLandmineComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<FSLandmineComponent, StepTriggerAttemptEvent>(OnStepAttempt);
    }

    private void OnStepAttempt(Entity<FSLandmineComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (HasComp<FSFriendlyFireComponent>(args.Tripper))
            args.Cancelled = true;
    }

    private void OnDeployed(Entity<FSLandmineComponent> ent, ref FSDeployableDeployedEvent args)
    {
        if (!TryComp<FSLandmineComponent>(args.Item, out var item))
            return;

        ent.Comp.Detonations = item.Detonations;
        ent.Comp.IntensityMultiplier = item.IntensityMultiplier;
        ent.Comp.HighExplosive = item.HighExplosive;
        Dirty(ent);
    }

    private EntityUid? DeployerOf(EntityUid mine)
    {
        if (!TryComp<FSDeployedByComponent>(mine, out var deployed) || deployed.DeployedBy is not { } body)
            return null;

        return TerminatingOrDeleted(body) ? null : body;
    }

    private void OnTrigger(Entity<FSLandmineComponent> ent, ref TriggerEvent args)
    {
        if (!Transform(ent).Anchored)
            return;

        var comp = ent.Comp;

        var total = comp.HighExplosive ? comp.HighExplosiveTotalIntensity : comp.TotalIntensity;
        var max = comp.HighExplosive ? comp.HighExplosiveMaxIntensity : comp.MaxIntensity;

        var cause = DeployerOf(ent) ?? args.User;
        total *= comp.IntensityMultiplier;
        var slope = comp.IntensitySlope;
        if (_implosion.TryGetFactors(cause, out var implosion))
        {
            total *= implosion.Total;
            slope *= implosion.Slope;
            max *= implosion.Max;
        }

        _explosion.QueueExplosion(ent.Owner, comp.ExplosionType, total,
            slope, max, canCreateVacuum: false, user: cause);

        args.Handled = true;

        comp.Detonations--;
        Dirty(ent);

        if (comp.Detonations <= 0)
        {
            QueueDel(ent);
            return;
        }

        _toggle.TryDeactivate(ent.Owner);

        if (!TryComp<FSArmingDelayComponent>(ent, out var arming))
            return;

        arming.Armed = false;
        arming.ArmAt = _timing.CurTime + TimeSpan.FromSeconds(arming.Delay);
        Dirty(ent.Owner, arming);
    }
}
