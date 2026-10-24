using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSApplyVulnerabilitySystem : EntityEffectSystem<MobStateComponent, FSApplyVulnerability>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<FSApplyVulnerability> args)
    {
        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.Duration);
        var comp = EnsureComp<FSVulnerableComponent>(entity);

        comp.Multiplier = args.Effect.Multiplier;

        if (comp.Until < until)
            comp.Until = until;
    }
}

public sealed partial class FSApplyVulnerability : EntityEffectBase<FSApplyVulnerability>
{
    [DataField]
    public float Duration = 8f;

    [DataField]
    public float Multiplier = 1.25f;
}

public sealed partial class FSApplySlowSystem : EntityEffectSystem<MobStateComponent, FSApplySlow>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<FSApplySlow> args)
    {
        if (HasComp<FSFriendlyFireComponent>(entity))
            return;

        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.Duration);
        var fresh = !TryComp<FSChemSlowedComponent>(entity, out var comp);
        comp ??= AddComp<FSChemSlowedComponent>(entity);

        if (comp.Until < until)
            comp.Until = until;

        if (fresh || comp.Multiplier != args.Effect.Multiplier)
        {
            comp.Multiplier = args.Effect.Multiplier;
            _movement.RefreshMovementSpeedModifiers(entity.Owner);
        }
    }
}

public sealed partial class FSApplySlow : EntityEffectBase<FSApplySlow>
{
    [DataField]
    public float Duration = 3f;

    [DataField]
    public float Multiplier = 0.6f;
}

public sealed partial class FSChemDebuffSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;

    private readonly List<EntityUid> _expired = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSVulnerableComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<FSChemSlowedComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
    }

    private void OnDamageModify(Entity<FSVulnerableComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Until <= _timing.CurTime)
            return;

        if (args.Damage.GetTotal() <= 0)
            return;

        args.Damage *= ent.Comp.Multiplier;
    }

    private void OnRefreshSpeed(Entity<FSChemSlowedComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Until > _timing.CurTime)
            args.ModifySpeed(ent.Comp.Multiplier);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;

        var vulnerable = EntityQueryEnumerator<FSVulnerableComponent>();
        while (vulnerable.MoveNext(out var uid, out var comp))
        {
            if (comp.Until <= now)
                RemCompDeferred<FSVulnerableComponent>(uid);
        }

        _expired.Clear();
        var slowed = EntityQueryEnumerator<FSChemSlowedComponent>();
        while (slowed.MoveNext(out var uid, out var comp))
        {
            if (comp.Until <= now)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            RemComp<FSChemSlowedComponent>(uid);
            _movement.RefreshMovementSpeedModifiers(uid);
        }
    }
}
