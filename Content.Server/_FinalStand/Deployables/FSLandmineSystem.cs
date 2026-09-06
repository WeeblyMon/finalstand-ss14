using Content.Shared._FinalStand.Deployables;
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
        ent.Comp.OwnerPlayer = args.User;

        if (!TryComp<FSLandmineComponent>(args.Item, out var item))
            return;

        ent.Comp.Detonations = item.Detonations;
        ent.Comp.IntensityMultiplier = item.IntensityMultiplier;
        ent.Comp.HighExplosive = item.HighExplosive;
        Dirty(ent);
    }

    private void OnTrigger(Entity<FSLandmineComponent> ent, ref TriggerEvent args)
    {
        if (!Transform(ent).Anchored)
            return;

        var comp = ent.Comp;

        var total = comp.HighExplosive ? comp.HighExplosiveTotalIntensity : comp.TotalIntensity;
        var max = comp.HighExplosive ? comp.HighExplosiveMaxIntensity : comp.MaxIntensity;

        var cause = comp.OwnerPlayer is { } owner && !TerminatingOrDeleted(owner) ? owner : args.User;
        _explosion.QueueExplosion(ent.Owner, comp.ExplosionType, total * comp.IntensityMultiplier,
            comp.IntensitySlope, max, canCreateVacuum: false, user: cause);

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
