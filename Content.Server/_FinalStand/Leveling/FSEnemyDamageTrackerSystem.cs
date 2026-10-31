using Content.Shared.Damage.Systems;

namespace Content.Server._FinalStand.Leveling;

// Damage each attacker actually dealt to a wave enemy, used for both XP and credit assists.
public sealed class FSEnemyDamageTrackerSystem : EntitySystem
{
    // Below this share of the damage a hit doesn't earn an assist, so spraying everything isn't a strategy.
    public const float AssistMinContribution = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSEnemyDamageTrackerComponent, DamageDealtEvent>(OnEnemyDamaged);
    }

    private void OnEnemyDamaged(EntityUid uid, FSEnemyDamageTrackerComponent comp, ref DamageDealtEvent args)
    {
        if (args.Origin == null) return;

        var total = args.Damage.GetTotal().Float();
        if (total <= 0f) return;

        comp.DamageByPlayer.TryGetValue(args.Origin.Value, out var prev);
        comp.DamageByPlayer[args.Origin.Value] = prev + total;
    }

    // Everyone other than the killer who dealt a real share of the damage.
    public void GetAssisters(FSEnemyDamageTrackerComponent tracker, EntityUid? killer, List<EntityUid> assisters)
    {
        assisters.Clear();

        var total = 0f;
        foreach (var dealt in tracker.DamageByPlayer.Values)
            total += dealt;

        if (total <= 0f)
            return;

        foreach (var (attacker, dealt) in tracker.DamageByPlayer)
        {
            if (attacker != killer && dealt / total >= AssistMinContribution)
                assisters.Add(attacker);
        }
    }
}
