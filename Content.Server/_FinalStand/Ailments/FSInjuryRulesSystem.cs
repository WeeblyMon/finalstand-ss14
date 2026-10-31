using Content.Shared._FinalStand.Ailments;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Ailments;

public sealed partial class FSInjuryRulesSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSAilmentSystem _ailments = default!;

    private static readonly FSInjurySpec HazardFracture = new() { Fracture = true };

    private readonly List<PendingHit> _pending = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSInjuryOnHitComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FSInjuryOnHitComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<FSAilmentStateComponent, DamageChangedEvent>(OnDamageChanged, after: [typeof(MobThresholdSystem)]);
        SubscribeLocalEvent<FSAilmentStateComponent, ExtinguishedEvent>(OnExtinguished);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _pending.Clear();
    }

    public void NoteIgnition(EntityUid target, EntityUid? source)
    {
        if (source != null && TryComp<FSAilmentStateComponent>(target, out var state))
            state.FireSource = source;
    }

    private void OnMeleeHit(Entity<FSInjuryOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (args.Handled)
            return;

        foreach (var target in args.HitEntities)
        {
            if (HasComp<FSAilmentStateComponent>(target))
                _pending.Add(new PendingHit(target, args.User, ent.Comp.Injury));
        }
    }

    private void OnProjectileHit(Entity<FSInjuryOnHitComponent> ent, ref ProjectileHitEvent args)
    {
        if (args.Shooter is { } shooter && HasComp<FSAilmentStateComponent>(args.Target))
            _pending.Add(new PendingHit(args.Target, shooter, ent.Comp.Injury));
    }

    private void OnDamageChanged(Entity<FSAilmentStateComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased)
            return;

        ent.Comp.LastDamage = _timing.CurTime;

        if (args.DamageDelta is not { } delta || _ailments.InInternalDamage)
            return;

        var dealt = DamageSpecifier.GetPositive(delta);
        if (args.Origin is { } origin)
        {
            var index = _pending.FindIndex(hit => hit.Target == ent.Owner && hit.Origin == origin);
            if (index < 0)
                return;

            var hit = _pending[index];
            _pending.RemoveAt(index);
            _ailments.InflictHit(ent, hit.Injury, dealt.GetTotal().Float(), origin);
            return;
        }

        var rules = _ailments.Rules;
        if (dealt.DamageDict.TryGetValue("Heat", out var heat) && TryComp<FlammableComponent>(ent, out var flammable) && flammable.OnFire)
            _ailments.AddMeter(ent, FSAilmentMeter.Burn, heat.Float() * rules.BurnMeterPerHeat, ent.Comp.FireSource, draining: false);

        if (dealt.DamageDict.TryGetValue("Blunt", out var blunt) && blunt >= rules.HazardFractureBlunt)
            _ailments.Inflict(ent, HazardFracture);
    }

    private void OnExtinguished(Entity<FSAilmentStateComponent> ent, ref ExtinguishedEvent args)
    {
        ent.Comp.FireSource = null;
        _ailments.DrainMeter(ent, FSAilmentMeter.Burn);
    }

    private readonly record struct PendingHit(EntityUid Target, EntityUid Origin, FSInjurySpec Injury);
}
