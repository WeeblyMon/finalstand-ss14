using Content.Server._FinalStand.NPC;
using Content.Server._FinalStand.Spawners;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSAggravatorSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private FSZombieRetaliationSystem _retaliation = default!;
    [Dependency] private FSPerkNotifySystem _notify = default!;

    private readonly HashSet<Entity<WaveSpawnedTagComponent>> _nearby = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    public float GetMultiplier(EntityUid mindId, FSPerkLevelsComponent perks)
    {
        if (perks.GetSlottedLevel("Aggravator") <= 0 || !TryComp<FSAggravatorComponent>(mindId, out var aggro))
            return 1f;

        return 1f + aggro.Stacks * FSPerkBonusConstants.AggravatorDamagePerStack;
    }

    public int GetStacks(EntityUid mindId) => CompOrNull<FSAggravatorComponent>(mindId)?.Stacks ?? 0;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSAggravatorComponent>();
        while (query.MoveNext(out var mindId, out var aggro))
        {
            if (aggro.Stacks <= 0 || now < aggro.NextDecay)
                continue;

            aggro.Stacks--;
            aggro.NextDecay = now + TimeSpan.FromSeconds(FSPerkBonusConstants.AggravatorDecaySeconds);
            _notify.SendStacks(mindId, "Aggravator", aggro.Stacks);
        }
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("Aggravator");
        if (level <= 0)
            return;

        var aggro = EnsureComp<FSAggravatorComponent>(ev.MindId);
        aggro.Stacks = Math.Min(aggro.Stacks + 1, level * FSPerkBonusConstants.AggravatorStacksPerLevel);
        aggro.NextDecay = _timing.CurTime + TimeSpan.FromSeconds(FSPerkBonusConstants.AggravatorDecaySeconds);
        _notify.SendStacks(ev.MindId, "Aggravator", aggro.Stacks);

        TauntAround(ev.Killer, aggro.Stacks * FSPerkBonusConstants.AggravatorRadiusPerStack);
    }

    public int TauntAround(EntityUid player, float radius)
    {
        if (radius <= 0f || TerminatingOrDeleted(player))
            return 0;

        _nearby.Clear();
        _lookup.GetEntitiesInRange(Transform(player).Coordinates, radius, _nearby);

        var taunted = 0;
        foreach (var zombie in _nearby)
        {
            if (_retaliation.Taunt(zombie, player, FSPerkBonusConstants.AggravatorTauntSeconds))
                taunted++;
        }

        return taunted;
    }
}

[RegisterComponent]
public sealed partial class FSAggravatorComponent : Component
{
    public int Stacks;
    public TimeSpan NextDecay;
}
