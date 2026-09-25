// Scavenger perk: kills can drop a supply cache that gives whoever opens it ammo, a heal or credits.
using Content.Server._FinalStand.Ammo;
using Content.Server._FinalStand.Economy;
using Content.Server.Popups;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Interaction;
using Content.Shared.Mind;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Perks;

public sealed class FSScavengerSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private WaveAmmoBoxSystem _ammo = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private FSPlayerWalletSystem _wallet = default!;
    [Dependency] private PopupSystem _popup = default!;

    private static readonly EntProtoId CacheProto = "FSScavengerCache";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
        SubscribeLocalEvent<FSScavengerCacheComponent, ActivateInWorldEvent>(OnActivate);
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("Scavenger");
        if (level <= 0 || !_random.Prob(FSPerkBonusConstants.ScavengerChance[level - 1]))
            return;

        Spawn(CacheProto, Transform(ev.Zombie).Coordinates);
    }

    private void OnActivate(Entity<FSScavengerCacheComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || TerminatingOrDeleted(ent))
            return;

        args.Handled = true;
        var user = args.User;

        string message;
        switch (_random.Next(3))
        {
            case 0:
                _ammo.RefillAllAmmo(user);
                message = "fs-scavenger-ammo";
                break;
            case 1:
                _damageable.HealEvenly(user, FixedPoint2.New(-FSPerkBonusConstants.ScavengerHeal));
                message = "fs-scavenger-heal";
                break;
            default:
                if (_mind.TryGetMind(user, out var mindId, out _))
                    _wallet.GiveCredits(mindId, FSPerkBonusConstants.ScavengerCredits);
                message = "fs-scavenger-credits";
                break;
        }

        _popup.PopupEntity(Loc.GetString(message, ("credits", FSPerkBonusConstants.ScavengerCredits)), user, user);
        QueueDel(ent);
    }
}
