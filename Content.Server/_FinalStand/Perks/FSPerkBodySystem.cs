using Content.Server._FinalStand.Shop;
using Content.Shared._FinalStand.Perks;
using Content.Shared.Mind.Components;

namespace Content.Server._FinalStand.Perks;

// Keeps the perk components that must live on the body in step with the slotted perks.
public sealed partial class FSPerkBodySystem : EntitySystem
{
    [Dependency] private FSPlayerUpgradesSystem _upgrades = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MindContainerComponent, FSPerksChangedEvent>(OnPerksChanged);
    }

    private void OnPerksChanged(EntityUid body, MindContainerComponent _, ref FSPerksChangedEvent args)
    {
        var perks = args.Perks;

        var ravage = perks.GetSlottedLevel("Ravage");
        if (ravage > 0)
        {
            var speed = EnsureComp<FSPerkMeleeSpeedComponent>(body);
            speed.Multiplier = 1f + ravage * FSPerkBonusConstants.RavageAttackSpeedPerLevel;
            Dirty(body, speed);
        }
        else
        {
            RemComp<FSPerkMeleeSpeedComponent>(body);
        }

        // Kept at level 0 rather than removed, so unslotting and reslotting cannot reset the once-per-life use.
        var undying = perks.GetSlottedLevel("Undying");
        if (undying > 0 || HasComp<FSUndyingComponent>(body))
            EnsureComp<FSUndyingComponent>(body).Level = undying;

        var manOnFire = perks.GetSlottedLevel("ManOnFire");
        if (manOnFire > 0)
            EnsureComp<FSManOnFireComponent>(body).Level = manOnFire;
        else
            RemComp<FSManOnFireComponent>(body);

        _upgrades.ReconcileHeldMagazines(body);
    }
}
