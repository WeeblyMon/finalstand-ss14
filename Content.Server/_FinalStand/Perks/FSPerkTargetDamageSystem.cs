// Perks that depend on who is being hit, applied on the zombie so bullets, melee and explosions all count.
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed class FSPerkTargetDamageSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private FSUnderdogSystem _underdog = default!;
    [Dependency] private IGameTiming _timing = default!;

    private readonly List<(EntityUid Target, int Level)> _pendingShred = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveSpawnedTagComponent, DamageModifyEvent>(OnZombieDamaged);
        SubscribeLocalEvent<FSShredderComponent, DamageModifyEvent>(OnShreddedDamaged);
    }

    private void OnZombieDamaged(Entity<WaveSpawnedTagComponent> ent, ref DamageModifyEvent args)
    {
        if (args.Origin is not { } attacker || args.Damage.GetTotal() <= 0)
            return;

        if (!_mind.TryGetMind(attacker, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return;

        var mult = 1f;

        var execLevel = perks.GetSlottedLevel("Executioner");
        if (execLevel > 0 && HealthFraction(ent) < FSPerkBonusConstants.ExecutionerHealthFraction)
            mult *= 1f + execLevel * FSPerkBonusConstants.ExecutionerPerLevel;

        var specLevel = perks.GetSlottedLevel("SpecialisedKilling");
        if (specLevel > 0 && HasComp<FSSpecialZombieComponent>(ent))
            mult *= 1f + specLevel * FSPerkBonusConstants.SpecialisedKillingPerLevel;

        mult *= _underdog.GetMultiplier(attacker, perks);

        if (Math.Abs(mult - 1f) > 0.0001f)
            args.Damage *= mult;

        var shredLevel = perks.GetSlottedLevel("Shredder");
        if (shredLevel > 0)
            _pendingShred.Add((ent, shredLevel));
    }

    private void OnShreddedDamaged(Entity<FSShredderComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.Stacks > 0 && args.Damage.GetTotal() > 0)
            args.Damage *= 1f + ent.Comp.Stacks * ent.Comp.PerStack;
    }

    private void AddShredderStack(EntityUid target, int level)
    {
        var shred = EnsureComp<FSShredderComponent>(target);
        shred.Stacks = Math.Min(shred.Stacks + 1, FSPerkBonusConstants.ShredderMaxStacks);
        shred.PerStack = Math.Max(shred.PerStack, FSPerkBonusConstants.ShredderPerStack[level - 1]);
        shred.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(FSPerkBonusConstants.ShredderSeconds);
    }

    private float HealthFraction(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable)
            || !_thresholds.TryGetThresholdForState(uid, MobState.Dead, out var dead)
            || dead <= 0)
            return 1f;

        return 1f - (damageable.TotalDamage / dead.Value).Float();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var (target, level) in _pendingShred)
        {
            if (!TerminatingOrDeleted(target))
                AddShredderStack(target, level);
        }
        _pendingShred.Clear();

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSShredderComponent>();
        while (query.MoveNext(out var uid, out var shred))
        {
            if (now >= shred.ExpiresAt)
                RemCompDeferred<FSShredderComponent>(uid);
        }
    }
}
