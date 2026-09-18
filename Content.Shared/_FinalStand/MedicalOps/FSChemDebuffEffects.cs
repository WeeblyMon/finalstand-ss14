using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Mobs.Components;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSSuppressArmorRegenSystem : EntityEffectSystem<MobStateComponent, FSSuppressArmorRegen>
{
    [Dependency] private IGameTiming _timing = default!;

    protected override void Effect(Entity<MobStateComponent> entity, ref EntityEffectEvent<FSSuppressArmorRegen> args)
    {
        var until = _timing.CurTime + TimeSpan.FromSeconds(args.Effect.Duration);
        var comp = EnsureComp<FSArmorSuppressedComponent>(entity);

        if (comp.Until < until)
            comp.Until = until;
    }
}

public sealed partial class FSSuppressArmorRegen : EntityEffectBase<FSSuppressArmorRegen>
{
    [DataField]
    public float Duration = 8f;
}

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

public sealed class FSChemDebuffSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSVulnerableComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(Entity<FSVulnerableComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Until <= _timing.CurTime)
            return;

        if (args.Damage.GetTotal() <= 0)
            return;

        args.Damage *= ent.Comp.Multiplier;
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

        var suppressed = EntityQueryEnumerator<FSArmorSuppressedComponent>();
        while (suppressed.MoveNext(out var uid, out var comp))
        {
            if (comp.Until <= now)
                RemCompDeferred<FSArmorSuppressedComponent>(uid);
        }
    }
}
