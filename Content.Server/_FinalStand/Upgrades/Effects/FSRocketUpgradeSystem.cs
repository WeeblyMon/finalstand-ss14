using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.Armor;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Explosion.Components;
using Content.Shared.Projectiles;
using Content.Shared.Tag;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Components.Effects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Upgrades.Effects;

public sealed class FSRocketUpgradeSystem : EntitySystem
{
    private const float IntensityPerRadiusUnit = 25f;
    private const float IntensityPerShapedChargeLevel = 20f;
    private const float BaseArmorStripRadius = 8f;

    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly FSResearchBuffSystem _researchBuff = default!;

    private static readonly ProtoId<TagPrototype> ExplosiveTag = "WeaponExplosive";

    private EntityQuery<ProjectileComponent> _projQuery;

    public override void Initialize()
    {
        base.Initialize();
        _projQuery = GetEntityQuery<ProjectileComponent>();
        SubscribeLocalEvent<ExplodeOnTriggerComponent, AttemptTriggerEvent>(OnAttemptTrigger);
    }

    private void OnAttemptTrigger(EntityUid uid, ExplodeOnTriggerComponent _, ref AttemptTriggerEvent args)
    {
        // Independent of the per-weapon shop-upgrade state below, so hand-thrown grenades
        // (no ProjectileComponent.Weapon) benefit too.
        if (_tags.HasTag(uid, ExplosiveTag) && TryComp<ExplosiveComponent>(uid, out var researchExplosive))
        {
            var mul = _researchBuff.GetDamageMultiplier(false, false, true, false, false, false, false, false, false);
            if (mul != 1f)
            {
#pragma warning disable RA0002
                researchExplosive.TotalIntensity *= mul;
#pragma warning restore RA0002
            }
        }

        if (!_projQuery.TryGetComponent(uid, out var proj) || proj.Weapon == null)
            return;
        if (!TryComp<FSWeaponUpgradeStateComponent>(proj.Weapon, out var state))
            return;
        TryComp<FSBarrageComponent>(proj.Weapon, out var barrage);
        var hasBarrage = barrage != null && barrage.Spool > 0f;
        if (state.BlastRadiusBonus <= 0 && state.ShapedChargeLevel <= 0 && state.ArmorShredMagnitude <= 0f && !hasBarrage)
            return;

        if (TryComp<ExplosiveComponent>(uid, out var explosive))
        {
#pragma warning disable RA0002
            if (state.BlastRadiusBonus > 0)
                explosive.TotalIntensity += state.BlastRadiusBonus * IntensityPerRadiusUnit;
            if (state.ShapedChargeLevel > 0)
                explosive.MaxIntensity += state.ShapedChargeLevel * IntensityPerShapedChargeLevel;
            if (hasBarrage)
                explosive.TotalIntensity *= 1f + barrage!.Spool * barrage.Level * FSBarrageComponent.ExplosionBonusPerLevel;
#pragma warning restore RA0002
        }

        if (state.ArmorShredMagnitude > 0f)
        {
            var mapCoords = _transform.GetMapCoordinates(uid);
            if (mapCoords.MapId == MapId.Nullspace)
                return;

            var radius = BaseArmorStripRadius + state.BlastRadiusBonus;
            var targets = new HashSet<Entity<FSArmorComponent>>();
            _lookup.GetEntitiesInRange<FSArmorComponent>(mapCoords, radius, targets);
            foreach (var (_, armor) in targets)
                armor.CurrentArmor = MathF.Max(0f, armor.CurrentArmor - armor.MaxArmor * state.ArmorShredMagnitude);
        }
    }
}
