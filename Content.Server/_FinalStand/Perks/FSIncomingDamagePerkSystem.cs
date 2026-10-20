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
    [Dependency] private FSBunkeredDownSystem _bunkeredDown = default!;

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

        var fromZombie = args.Origin != null && HasComp<FSZombieVisualsComponent>(args.Origin.Value);
        var holdingMelee = _hands.TryGetActiveItem(uid, out var activeHeld) && activeHeld.HasValue
            && HasComp<MeleeWeaponComponent>(activeHeld.Value) && !HasComp<GunComponent>(activeHeld.Value);

        args.Damage *= GetIncomingMultiplier(mindId, augs, fromZombie, holdingMelee);
    }

    public float GetIncomingMultiplier(EntityUid mindId, FSPerkLevelsComponent augs, bool fromZombie,
        bool holdingMelee, List<(string PerkId, float Resist)>? sources = null)
    {
        var points = 0f;

        void Resist(string perkId, float value)
        {
            points += value;
            sources?.Add((perkId, value));
        }

        var juggLevel = augs.GetSlottedLevel("Juggernaught");
        if (juggLevel > 0 && fromZombie)
            Resist("Juggernaught", juggLevel * FSPerkBonusConstants.JuggernaughtPerLevel);

        var snsLevel = augs.GetSlottedLevel("SwordAndShield");
        if (snsLevel > 0 && holdingMelee)
            Resist("SwordAndShield", snsLevel * FSPerkBonusConstants.SwordAndShieldResistPerLevel);

        var pacifistLevel = augs.GetSlottedLevel("Pacifist");
        if (pacifistLevel > 0)
            Resist("Pacifist", pacifistLevel * FSPerkBonusConstants.PacifistResistPerLevel);

        var rampLevel = augs.GetSlottedLevel("Rampage");
        if (rampLevel > 0 && TryComp<FSRampageComponent>(mindId, out var ramp) && ramp.Stacks > 0)
            Resist("Rampage", MathF.Min(1f, ramp.Stacks * rampLevel * FSPerkBonusConstants.RampageResistPerLevel));

        var medievalLevel = augs.GetSlottedLevel("Medievalist");
        if (medievalLevel > 0)
            Resist("Medievalist", medievalLevel * FSPerkBonusConstants.MedievalistResistPerLevel);

        var bunker = _bunkeredDown.GetResist(mindId, augs);
        if (bunker > 0f)
            Resist("BunkeredDown", bunker);

        var taken = 1f / (points + 1f);

        if (augs.GetSlottedLevel("GlassCannon") > 0)
        {
            taken += FSPerkBonusConstants.GlassCannonIncomingMultiplier - 1f;
            sources?.Add(("GlassCannon", 1f - FSPerkBonusConstants.GlassCannonIncomingMultiplier));
        }

        return taken;
    }
}
