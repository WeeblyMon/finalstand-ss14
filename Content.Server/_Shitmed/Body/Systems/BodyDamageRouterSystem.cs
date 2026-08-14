using System.Linq;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared.Body;
using Content.Shared._FinalStand.Medical;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Robust.Shared.Random;

namespace Content.Server._Shitmed.Body.Systems;

/// <summary>
/// FINALSTAND: Routes damage applied to a mob entity (BodyComponent) to one of
/// its body parts (WoundableComponent), so the Shitmed wound/trauma system
/// receives DamageChangedEvent and can populate wound severity, bleeding,
/// trauma, and vital-damage state for the HealthAnalyzer body doll.
/// </summary>
/// <remarks>
/// Goob-Station's full Shitmed port rewrites <c>DamageableSystem.ChangeDamage</c>
/// to apply damage directly to body parts (replacing mob-level damage entirely).
/// That rewrite is ~200 lines and intersects armor, resistance, and combat-mode
/// code. This system is a much smaller stand-in: when DamageChangedEvent fires
/// on a mob, induce equivalent wounds on a randomly-picked WoundableComponent
/// body part. The mob's own DamageableComponent damage still accumulates (so
/// mobstate transitions work), and the wound system now sees the same delta so
/// the analyzer can display wounds, traumas, vital damage, and the colored body
/// doll. Healing routes symmetrically through TryHealWoundsOnWoundable.
/// </remarks>
public sealed partial class BodyDamageRouterSystem : EntitySystem
{
    [Dependency] private OrganLookupSystem _lookup = default!;
    [Dependency] private SharedBodyAppearanceSystem _body = default!;
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BodyComponent, DamageChangedEvent>(OnBodyDamageChanged);
    }

    private void OnBodyDamageChanged(EntityUid uid, BodyComponent body, ref DamageChangedEvent args)
    {
        // Need a damage delta to act on.
        if (args.DamageDelta is null || args.DamageDelta.Empty)
            return;

        // Collect woundable body parts. The mob itself doesn't have WoundableComponent —
        // its anatomical sub-entities do.
        var parts = new List<EntityUid>();
        foreach (var (partId, _) in _lookup.GetBodyOrgans((uid, body)))
        {
            if (HasComp<WoundableComponent>(partId))
                parts.Add(partId);
        }

        if (parts.Count == 0)
            return;

        // For damage: pick one body part (TargetingComponent target if set, otherwise random).
        // For healing: distribute across all body parts so a Brutepack actually heals.
        EntityUid? chosen = null;
        if (args.Origin is { } origin && TryComp<TargetingComponent>(origin, out var attackerTargeting))
            chosen = ResolveTargetPart(parts, attackerTargeting.Target);

        if (chosen is null && TryComp<TargetingComponent>(uid, out var targeting))
            chosen = ResolveTargetPart(parts, targeting.Target);

        chosen ??= _random.Pick(parts);

        foreach (var (type, value) in args.DamageDelta.DamageDict)
        {
            if (value == FixedPoint2.Zero)
                continue;

            if (value > 0)
            {
                _wound.TryInduceWound(chosen.Value, type, value, out _);
            }
            else
            {
                var wounded = parts.Where(part => HasWoundOfType(part, type)).ToList();
                if (wounded.Count == 0)
                    continue;

                var perPart = -value / wounded.Count;
                foreach (var part in wounded)
                    _wound.TryHealWoundsOnWoundable(part, perPart, type, out _);
            }
        }
    }

    private bool HasWoundOfType(EntityUid woundable, string damageType)
    {
        foreach (var wound in _wound.GetWoundableWounds(woundable))
        {
            if (wound.Comp.DamageType == damageType)
                return true;
        }

        return false;
    }

    private EntityUid? ResolveTargetPart(List<EntityUid> candidates, TargetBodyPart target)
    {
        foreach (var part in candidates)
        {
            if (_lookup.GetTarget(part) == target)
                return part;
        }

        return null;
    }
}
