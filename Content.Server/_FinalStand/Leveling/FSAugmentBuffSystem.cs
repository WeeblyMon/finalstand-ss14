using Content.Server._FinalStand.Augments;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Mind;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Leveling;

public sealed class FSAugmentBuffSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> LauncherTag = "WeaponGunLauncher";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnStoppingPower);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnBulletStorm);
    }

    private void OnStoppingPower(FSProjectileHitEffectEvent ev)
    {
        if (ev.Shooter == null) return;
        if (ev.Weapon.HasValue && _tags.HasTag(ev.Weapon.Value, LauncherTag)) return;
        if (!_mind.TryGetMind(ev.Shooter.Value, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("StoppingPower");
        if (level <= 0) return;

        ev.AdditionalMultiplier *= 1f + level * 0.04f;
    }

    private void OnBulletStorm(EntityUid uid, GunComponent gunComp, ref GunRefreshModifiersEvent args)
    {
        var holder = Transform(uid).ParentUid;
        if (!holder.IsValid()) return;
        if (!_mind.TryGetMind(holder, out var mindId, out _)) return;
        if (!TryComp<FSAugmentLevelsComponent>(mindId, out var augs)) return;

        var level = augs.GetSlottedLevel("BulletStorm");
        if (level <= 0) return;

        args.FireRate *= 1f + level * 0.08f;
    }
}
