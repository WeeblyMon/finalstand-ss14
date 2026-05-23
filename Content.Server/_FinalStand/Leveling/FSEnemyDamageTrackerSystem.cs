using Content.Shared.Damage.Systems;

namespace Content.Server._FinalStand.Leveling;

public sealed class FSEnemyDamageTrackerSystem : EntitySystem
{
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
}
