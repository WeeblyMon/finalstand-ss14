using Content.Shared.Clothing;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew;

namespace Content.Shared.Damage.Systems;

public sealed partial class SlowOnDamageSystem : EntitySystem
{
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifierSystem = default!;
    [Dependency] private DamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SlowOnDamageComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<SlowOnDamageComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);

        SubscribeLocalEvent<ClothingSlowOnDamageModifierComponent, InventoryRelayedEvent<ModifySlowOnDamageSpeedEvent>>(OnModifySpeed);
        SubscribeLocalEvent<ClothingSlowOnDamageModifierComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<ClothingSlowOnDamageModifierComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<ClothingSlowOnDamageModifierComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);

        SubscribeLocalEvent<IgnoreSlowOnDamageComponent, StatusEffectAppliedEvent>(OnIgnoreApplied);
        SubscribeLocalEvent<IgnoreSlowOnDamageComponent, StatusEffectRemovedEvent>(OnIgnoreRemoved);
        SubscribeLocalEvent<IgnoreSlowOnDamageComponent, StatusEffectRelayedEvent<ModifySlowOnDamageSpeedEvent>>(OnIgnoreModifySpeed);
        // FINALSTAND: on the mob itself too, where wave enemies and the old-style adrenaline status put it; only the
        // status-effect relay was handled, so neither did anything.
        SubscribeLocalEvent<IgnoreSlowOnDamageComponent, ModifySlowOnDamageSpeedEvent>(OnIgnoreModifyOwnSpeed);
        SubscribeLocalEvent<IgnoreSlowOnDamageComponent, ComponentStartup>(OnIgnoreChanged);
        SubscribeLocalEvent<IgnoreSlowOnDamageComponent, ComponentShutdown>(OnIgnoreChanged);
    }

    private void OnRefreshMovespeed(EntityUid uid, SlowOnDamageComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<DamageableComponent>(uid, out var damage))
            return;

        var totalDamage = _damage.GetTotalDamage((uid, damage));

        if (totalDamage == FixedPoint2.Zero)
            return;

        // Get closest threshold
        FixedPoint2 closest = FixedPoint2.Zero;
        var total = totalDamage;
        foreach (var thres in component.SpeedModifierThresholds)
        {
            if (total >= thres.Key && thres.Key > closest)
                closest = thres.Key;
        }

        if (closest != FixedPoint2.Zero)
        {
            var speed = component.SpeedModifierThresholds[closest];

            var ev = new ModifySlowOnDamageSpeedEvent(speed);
            RaiseLocalEvent(uid, ref ev);
            args.ModifySpeed(ev.Speed, ev.Speed);
        }
    }

    private void OnDamageChanged(EntityUid uid, SlowOnDamageComponent component, DamageChangedEvent args)
    {
        // FINALSTAND: a mob that ignores the slowdown outright has nothing to refresh; every hit on every zombie did.
        if (HasComp<IgnoreSlowOnDamageComponent>(uid))
            return;

        // We -could- only refresh if it crossed a threshold but that would kind of be a lot of duplicated
        // code and this isn't a super hot path anyway since basically only humans have this

        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(uid);
    }

    private void OnModifySpeed(Entity<ClothingSlowOnDamageModifierComponent> ent, ref InventoryRelayedEvent<ModifySlowOnDamageSpeedEvent> args)
    {
        var dif = 1 - args.Args.Speed;
        if (dif <= 0)
            return;

        // reduces the slowness modifier by the given coefficient
        args.Args.Speed += dif * ent.Comp.Modifier;
    }

    private void OnExamined(Entity<ClothingSlowOnDamageModifierComponent> ent, ref ExaminedEvent args)
    {
        var msg = Loc.GetString("slow-on-damage-modifier-examine", ("mod", ent.Comp.Modifier * 100));
        args.PushMarkup(msg);
    }

    private void OnGotEquipped(Entity<ClothingSlowOnDamageModifierComponent> ent, ref ClothingGotEquippedEvent args)
    {
        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(args.Wearer);
    }

    private void OnGotUnequipped(Entity<ClothingSlowOnDamageModifierComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(args.Wearer);
    }

    private void OnIgnoreApplied(Entity<IgnoreSlowOnDamageComponent> ent, ref StatusEffectAppliedEvent args)
    {
        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(args.Target);
    }

    private void OnIgnoreRemoved(Entity<IgnoreSlowOnDamageComponent> ent, ref StatusEffectRemovedEvent args)
    {
        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(args.Target);
    }

    private void OnIgnoreModifySpeed(Entity<IgnoreSlowOnDamageComponent> ent, ref StatusEffectRelayedEvent<ModifySlowOnDamageSpeedEvent> args)
    {
        args.Args = args.Args with { Speed = 1f };
    }

    // FINALSTAND: a shutting-down one no longer counts, so the refresh it triggers puts the slowdown back.
    private void OnIgnoreModifyOwnSpeed(Entity<IgnoreSlowOnDamageComponent> ent, ref ModifySlowOnDamageSpeedEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.Speed = 1f;
    }

    // FINALSTAND: damage doesn't refresh speed while it's there, so it has to on the way in and out.
    private void OnIgnoreChanged<T>(Entity<IgnoreSlowOnDamageComponent> ent, ref T args)
    {
        _movementSpeedModifierSystem.RefreshMovementSpeedModifiers(ent.Owner);
    }
}

[ByRefEvent]
public record struct ModifySlowOnDamageSpeedEvent(float Speed) : IInventoryRelayEvent
{
    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}
