using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSLifeLeechSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;

    private static readonly float[] HealByLevel = [0f, 1f, 2f, 4f, 6f];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("LifeLeech");
        if (level <= 0) return;

        _damageable.HealEvenly(ev.Killer, FixedPoint2.New(-HealByLevel[level]));
    }
}
