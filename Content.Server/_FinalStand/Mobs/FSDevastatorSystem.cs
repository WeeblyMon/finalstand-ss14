using Content.Shared._FinalStand.Mobs;
using Content.Shared.Audio;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSDevastatorSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDevastatorComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FSDevastatorComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FSDevastatorComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<FSDevastatorComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid uid, FSDevastatorComponent comp, DamageModifyEvent args)
    {
        // 0% resistance at full HP, 80% at near-death — scales linearly with BerserkRatio
        var multiplier = 1f - 0.8f * comp.BerserkRatio;
        args.Damage *= multiplier;
    }

    private void OnMobStateChanged(EntityUid uid, FSDevastatorComponent comp, MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            _ambientSound.SetAmbience(uid, false);
    }

    private void OnRefreshSpeed(EntityUid uid, FSDevastatorComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(comp.CurrentSpeedMultiplier, comp.CurrentSpeedMultiplier);
    }

    private void OnMeleeHit(EntityUid uid, FSDevastatorComponent comp, MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        // Lifesteal
        _damageable.HealEvenly(uid, FixedPoint2.New(-comp.LifestealAmount));

        // Apply bonus damage based on berserk multiplier
        if (comp.CurrentDamageMultiplier > 1f)
        {
            var bonus = args.BaseDamage * (comp.CurrentDamageMultiplier - 1f);
            args.BonusDamage += bonus;
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSDevastatorComponent, DamageableComponent, MobThresholdsComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var damageable, out var thresholds, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (!_thresholds.TryGetDeadThreshold(uid, out var dead, thresholds) || dead.Value <= 0)
                continue;

            var maxDamage = dead.Value.Float();
            var hpRatio = Math.Clamp(1f - damageable.TotalDamage.Float() / maxDamage, 0f, 1f);
            var newSpeedMult = 1f + (comp.MaxSpeedMultiplier - 1f) * (1f - hpRatio);
            var newDamageMult = 1f + (comp.MaxDamageMultiplier - 1f) * (1f - hpRatio);

            comp.CurrentDamageMultiplier = newDamageMult;

            var dirty = false;
            var newRatio = 1f - hpRatio;
            if (MathF.Abs(newRatio - comp.BerserkRatio) > 0.01f)
            {
                comp.BerserkRatio = newRatio;
                dirty = true;
            }

            if (MathF.Abs(newSpeedMult - comp.CurrentSpeedMultiplier) > 0.02f)
            {
                comp.CurrentSpeedMultiplier = newSpeedMult;
                _movement.RefreshMovementSpeedModifiers(uid);
            }

            if (dirty)
                Dirty(uid, comp);
        }
    }
}
