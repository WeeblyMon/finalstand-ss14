// Scavenger perk: kills can drop a private supply cache that gives its owner ammo, a heal or credits.
using Content.Server._FinalStand.Ammo;
using Content.Server._FinalStand.Economy;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.WaveHud;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Mind;
using Robust.Server.GameStates;
using Robust.Shared.Player;
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
    [Dependency] private PvsOverrideSystem _pvsOverride = default!;

    private static readonly EntProtoId CacheProto = "FSScavengerCache";
    private static readonly Color AmmoColor = Color.FromHex("#7FD4FF");
    private static readonly Color HealColor = Color.FromHex("#4ADE80");
    private static readonly Color CreditsColor = Color.FromHex("#F0B429");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieKilledByPlayerEvent>(OnZombieKilled);
        SubscribeLocalEvent<FSScavengerCacheComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<FSScavengerCacheComponent, StepTriggeredOffEvent>(OnStepped);
    }

    private void OnZombieKilled(ref FSZombieKilledByPlayerEvent ev)
    {
        var level = ev.Perks.GetSlottedLevel("Scavenger");
        if (level <= 0 || !_random.Prob(FSPerkBonusConstants.ScavengerChance[level - 1]))
            return;

        var cache = Spawn(CacheProto, Transform(ev.Zombie).Coordinates);
        EnsureComp<FSScavengerCacheComponent>(cache).OwnerMind = ev.MindId;

        if (TryComp<ActorComponent>(ev.Killer, out var actor))
            _pvsOverride.AddForceSend(cache, actor.PlayerSession);
    }

    private void OnStepAttempt(Entity<FSScavengerCacheComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue |= ent.Comp.OwnerMind is { } owner
            && _mind.TryGetMind(args.Tripper, out var mindId, out _)
            && mindId == owner;
    }

    private void OnStepped(Entity<FSScavengerCacheComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (TerminatingOrDeleted(ent) || EntityManager.IsQueuedForDeletion(ent))
            return;

        var user = args.Tripper;

        string message;
        Color color;
        switch (_random.Next(3))
        {
            case 0:
                _ammo.RefillAllAmmo(user);
                (message, color) = ("fs-scavenger-ammo", AmmoColor);
                break;
            case 1:
                _damageable.HealEvenly(user, FixedPoint2.New(-FSPerkBonusConstants.ScavengerHeal));
                (message, color) = ("fs-scavenger-heal", HealColor);
                break;
            default:
                if (_mind.TryGetMind(user, out var mindId, out _))
                    _wallet.GiveCredits(mindId, FSPerkBonusConstants.ScavengerCredits);
                (message, color) = ("fs-scavenger-credits", CreditsColor);
                break;
        }

        if (TryComp<ActorComponent>(user, out var actor))
        {
            RaiseNetworkEvent(new FSFloatingTextEvent
            {
                Target = GetNetEntity(user),
                Text = Loc.GetString(message,
                    ("heal", FSPerkBonusConstants.ScavengerHeal), ("credits", FSPerkBonusConstants.ScavengerCredits)),
                Color = color,
            }, actor.PlayerSession);
        }

        QueueDel(ent);
    }
}
