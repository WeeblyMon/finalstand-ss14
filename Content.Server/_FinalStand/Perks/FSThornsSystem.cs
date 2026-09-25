// Thorns perk: kills bank stacks; each zombie hit spends one and reflects part of the damage.
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Systems;
using Content.Shared.Mind;

namespace Content.Server._FinalStand.Perks;

public sealed class FSThornsSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
        SubscribeLocalEvent<FSIncomingDamageModifyEvent>(OnIncomingDamage);
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("Thorns");
        if (level <= 0)
            return;

        var thorns = EnsureComp<FSThornsComponent>(ev.MindId);
        var max = FSPerkBonusConstants.ThornsMaxStacks[level - 1];
        if (thorns.Stacks >= max)
            return;

        thorns.Stacks++;
        _notify.SendStacks(ev.MindId, "Thorns", thorns.Stacks);
    }

    private void OnIncomingDamage(ref FSIncomingDamageModifyEvent ev)
    {
        if (ev.Args.Origin is not { } zombie
            || !HasComp<WaveSpawnedTagComponent>(zombie)
            || ev.Args.Damage.GetTotal() <= 0
            || !_mind.TryGetMind(ev.Target, out var mindId, out _)
            || !TryComp<FSPerkLevelsComponent>(mindId, out var perks))
            return;

        var level = perks.GetSlottedLevel("Thorns");
        if (level <= 0 || !TryComp<FSThornsComponent>(mindId, out var thorns) || thorns.Stacks <= 0)
            return;

        thorns.Stacks--;
        _notify.SendStacks(mindId, "Thorns", thorns.Stacks);

        var reflected = ev.Args.Damage * FSPerkBonusConstants.ThornsReflect[level - 1];
        _damageable.TryChangeDamage(zombie, reflected, origin: ev.Target);
    }
}
