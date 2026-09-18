using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._FinalStand.Weapons;

public sealed class FSWeaponStatSystem : EntitySystem
{
    [Dependency] private FSPerkBuffSystem _perks = default!;
    [Dependency] private FSResearchBuffSystem _research = default!;
    [Dependency] private FSMedicalBonusSystem _medical = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnRefresh);
    }

    private void OnRefresh(EntityUid uid, GunComponent gun, ref GunRefreshModifiersEvent args)
    {
        _research.ApplyGunModifiers(uid, ref args);

        var holder = Transform(uid).ParentUid;
        if (!holder.IsValid())
            return;

        _perks.ApplyGunModifiers(holder, ref args);
        _medical.ApplyGunModifiers(holder, ref args);
    }
}
