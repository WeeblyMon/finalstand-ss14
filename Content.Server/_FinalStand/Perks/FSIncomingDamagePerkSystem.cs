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

// Every incoming-damage-reduction perk: Juggernaught, Sword and Shield's resistance half, Glass
// Cannon, Pacifist, Rampage. Driven by FSIncomingDamageModifyEvent, which the weapon-resistance
// system raises once it has applied its own modifiers — Robust Toolbox allows only one directed
// subscriber per (component, event) pair and that system owns (HandsComponent, DamageModifyEvent).
public sealed class FSIncomingDamagePerkSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

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

        // Glass Cannon: doubles all incoming damage.
        if (augs.GetSlottedLevel("GlassCannon") > 0)
            args.Damage *= FSPerkBonusConstants.GlassCannonIncomingMultiplier;

        // Pacifist: high incoming damage resistance.
        var pacifistLevel = augs.GetSlottedLevel("Pacifist");
        if (pacifistLevel > 0)
            args.Damage *= 1f - pacifistLevel * FSPerkBonusConstants.PacifistResistPerLevel;

        // Rampage: stacks-based incoming damage resistance.
        var rampLevel = augs.GetSlottedLevel("Rampage");
        if (rampLevel > 0 && TryComp<FSRampageComponent>(mindId, out var ramp) && ramp.Stacks > 0)
            args.Damage *= Math.Max(0f, 1f - ramp.Stacks * rampLevel * FSPerkBonusConstants.RampageResistPerLevel);
    }
}
