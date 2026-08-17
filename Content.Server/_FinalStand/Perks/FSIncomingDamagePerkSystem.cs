using Content.Server._FinalStand.Leveling;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._FinalStand.Perks;

// Every incoming-damage perk. Driven by FSIncomingDamageModifyEvent, raised by the weapon-resistance
// system since it owns the one allowed (HandsComponent, DamageModifyEvent) subscription.
public sealed partial class FSIncomingDamagePerkSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSIncomingDamageModifyEvent>(OnIncomingDamage);
    }

    private void OnIncomingDamage(ref FSIncomingDamageModifyEvent ev)
        => ApplyIncomingPerkModifiers(ev.Target, ev.Args);

    public void ApplyIncomingPerkModifiers(EntityUid uid, DamageModifyEvent args)
    {
        if (!_mind.TryGetMind(uid, out var mindId, out _)) return;
        if (!TryComp<FSPerkLevelsComponent>(mindId, out var augs)) return;

        var juggLevel = augs.GetSlottedLevel("Juggernaught");
        if (juggLevel > 0 && args.Origin != null && HasComp<FSZombieVisualsComponent>(args.Origin.Value))
            args.Damage *= 1f - juggLevel * FSPerkBonusConstants.JuggernaughtPerLevel;

        var snsLevel = augs.GetSlottedLevel("SwordAndShield");
        if (snsLevel > 0
            && _hands.TryGetActiveItem(uid, out var activeHeld) && activeHeld.HasValue
            && HasComp<MeleeWeaponComponent>(activeHeld.Value) && !HasComp<GunComponent>(activeHeld.Value))
        {
            args.Damage *= 1f - snsLevel * FSPerkBonusConstants.SwordAndShieldResistPerLevel;
        }

        if (augs.GetSlottedLevel("GlassCannon") > 0)
            args.Damage *= FSPerkBonusConstants.GlassCannonIncomingMultiplier;

        var pacifistLevel = augs.GetSlottedLevel("Pacifist");
        if (pacifistLevel > 0)
            args.Damage *= 1f - pacifistLevel * FSPerkBonusConstants.PacifistResistPerLevel;

        var rampLevel = augs.GetSlottedLevel("Rampage");
        if (rampLevel > 0 && TryComp<FSRampageComponent>(mindId, out var ramp) && ramp.Stacks > 0)
            args.Damage *= Math.Max(0f, 1f - ramp.Stacks * rampLevel * FSPerkBonusConstants.RampageResistPerLevel);
    }
}
