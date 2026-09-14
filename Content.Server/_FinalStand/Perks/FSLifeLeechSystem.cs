using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSLifeLeechSystem : EntitySystem
{
    [Dependency] private DamageableSystem _damageable = default!;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("LifeLeech");
        if (level <= 0) return;

        _damageable.HealEvenly(ev.Killer, FixedPoint2.New(-FSPerkBonusConstants.LifeLeechHeal[level - 1]));
    }
}
