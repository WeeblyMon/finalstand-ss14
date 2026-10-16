using Content.Client._FinalStand.Ammo;
using Content.Client._FinalStand.Armor;
using Content.Client._FinalStand.Bags;
using Content.Client._FinalStand.CCC;
using Content.Client._FinalStand.Deployables;
using Content.Client._FinalStand.Engineering;
using Content.Client._FinalStand.Loot;
using Content.Client._FinalStand.Mech;
using Content.Client._FinalStand.MedicalOps;
using Content.Client._FinalStand.Perks;
using Content.Client._FinalStand.RCD;
using Content.Client._FinalStand.Repair;
using Content.Client._FinalStand.Teleportation;
using Content.Client._FinalStand.WaveHud;
using Content.Client._FinalStand.Weapons;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.Interface;

public sealed class FSClassicHudSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;

    private static readonly Type[] Hidden =
    {
        typeof(WaveHudOverlay),
        typeof(FSWaveReportOverlay),
        typeof(FSMechHudOverlay),
        typeof(FSUberOverlay),
        typeof(FSChargeMeterOverlay),
        typeof(FSToolCardOverlay),
        typeof(FSDeployableToastOverlay),
        typeof(FSDeployableChipOverlay),
        typeof(FSDeployableLifetimeOverlay),
        typeof(FSAmmoBoxLabelOverlay),
        typeof(FSSentryTurretLabelOverlay),
        typeof(FSStructureHealthOverlay),
        typeof(FSCCCHealthBarOverlay),
        typeof(FSPortalTimerOverlay),
        typeof(FSRCDGhostHintOverlay),
        typeof(FSPerkShopIndicatorOverlay),
        typeof(FSArmorShopIndicatorOverlay),
        typeof(FSBagShopIndicatorOverlay),
        typeof(FSGachaLabelOverlay),
        typeof(FSSyringeFillerIndicatorOverlay),
        typeof(WaveAmmoBoxIndicatorOverlay),
    };

    public override void FrameUpdate(float frameTime)
    {
        if (!FSClassicHud.Enabled)
            return;

        foreach (var type in Hidden)
            _overlays.RemoveOverlay(type);
    }
}
