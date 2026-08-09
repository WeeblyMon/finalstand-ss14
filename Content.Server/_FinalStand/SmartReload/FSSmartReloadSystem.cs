using System.Linq;
using Content.Server.Popups;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.Components;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared._FinalStand.Shop;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.SmartReload;

public sealed partial class FSSmartReloadSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedGunSystem _gunSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    // Tracks active shell-insert do-afters per gun (for cleanup bookkeeping).
    private readonly Dictionary<EntityUid, DoAfterId> _activeShellInserts = new();

    // Guns that fired a shot while a shell-insert chain was running.
    // The chain aborts at the next OnShellInsertComplete rather than via Cancel()
    // (Cancel leaves a stale BlockDuplicate entry that breaks subsequent R presses).
    private readonly HashSet<EntityUid> _reloadAborted = new();

    // Revolvers had no equivalent of _activeShellInserts, so a chamber-fill chain could neither
    // be detected nor aborted.
    private readonly Dictionary<EntityUid, DoAfterId> _activeChamberFills = new();

    private static readonly TimeSpan MagEjectTime    = TimeSpan.FromSeconds(0.25);
    private static readonly TimeSpan MagInsertTime   = TimeSpan.FromSeconds(0.55);
    private static readonly TimeSpan ShellInsertTime = TimeSpan.FromSeconds(0.55);
    private static readonly TimeSpan ChamberFillTime = TimeSpan.FromSeconds(0.45);

    private enum GunArchetype { Magazine, TubeFed, Revolver, Battery, None }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<FSSmartReloadMessage>(OnSmartReload);
        SubscribeNetworkEvent<FSEjectMessage>(OnEject);
        SubscribeLocalEvent<MagazineAmmoProviderComponent, FSMagReloadDoAfterEvent>(OnMagReloadComplete);
        SubscribeLocalEvent<ChamberMagazineAmmoProviderComponent, FSMagReloadDoAfterEvent>(OnMagReloadComplete);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, FSShellInsertDoAfterEvent>(OnShellInsertComplete);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, AmmoShotEvent>(OnBallisticGunFired);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, ComponentRemove>(OnBallisticRemoved);
        SubscribeLocalEvent<RevolverAmmoProviderComponent, FSChamberFillDoAfterEvent>(OnChamberFillComplete);
        SubscribeLocalEvent<RevolverAmmoProviderComponent, ComponentRemove>(OnRevolverRemoved);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _activeShellInserts.Clear();
        _activeChamberFills.Clear();
        _reloadAborted.Clear();

        var query = EntityQueryEnumerator<FSReloadingComponent>();
        while (query.MoveNext(out var gun, out _))
            RemCompDeferred<FSReloadingComponent>(gun);
    }

    private void OnRevolverRemoved(EntityUid gun, RevolverAmmoProviderComponent _, ComponentRemove args)
    {
        _activeChamberFills.Remove(gun);
    }

    private void SetReloading(EntityUid gun, bool reloading)
    {
        if (reloading)
            EnsureComp<FSReloadingComponent>(gun);
        else
            RemComp<FSReloadingComponent>(gun);
    }

    private bool TryGetValidGun(NetEntity netGun, ICommonSession? session,
        out EntityUid gun, out EntityUid user)
    {
        gun  = GetEntity(netGun);
        user = session?.AttachedEntity ?? EntityUid.Invalid;

        return user.IsValid()
               && !_mobState.IsIncapacitated(user)
               && HasComp<GunComponent>(gun)
               && _hands.IsHolding(user, gun);
    }
    private GunArchetype Detect(EntityUid gun)
    {
        if (HasComp<RevolverAmmoProviderComponent>(gun))                                          return GunArchetype.Revolver;
        if (HasComp<MagazineAmmoProviderComponent>(gun) ||
            HasComp<ChamberMagazineAmmoProviderComponent>(gun))                                   return GunArchetype.Magazine;
        if (HasComp<BallisticAmmoProviderComponent>(gun))                                         return GunArchetype.TubeFed;
        if (HasComp<BatteryAmmoProviderComponent>(gun))                                           return GunArchetype.Battery;
        return GunArchetype.None;
    }

    // R Tap

    private void OnSmartReload(FSSmartReloadMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetValidGun(msg.Gun, args.SenderSession, out var gun, out var user))
            return;

        switch (Detect(gun))
        {
            case GunArchetype.Magazine:
                ReloadMagazine(gun, user);
                break;
            case GunArchetype.TubeFed:
                ReloadTubeFed(gun, user);
                break;
            case GunArchetype.Revolver:
                ReloadRevolver(gun, user);
                break;
            case GunArchetype.Battery:
                ReloadBattery(gun, user);
                break;
            default:
                _popup.PopupEntity("No reload action available.", gun, user);
                break;
        }
    }

    // Hold R to eject (magazine, tube shells, revolver rounds, or battery cell depending on gun type)

    private void OnEject(FSEjectMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetValidGun(msg.Gun, args.SenderSession, out var gun, out var user))
            return;

        switch (Detect(gun))
        {
            case GunArchetype.Magazine:
                _slots.TryEject(gun, SharedGunSystem.MagazineSlot, user, out _);
                break;

            case GunArchetype.TubeFed:
                if (TryComp<BallisticAmmoProviderComponent>(gun, out var bal))
                    DumpAllTubeShells(gun, bal);
                break;

            case GunArchetype.Revolver:
                if (TryComp<RevolverAmmoProviderComponent>(gun, out var rev))
                    _gunSystem.EmptyRevolver((gun, rev), user);
                break;

            case GunArchetype.Battery:
                // Only eject if the gun has a swappable cell slot.
                if (_slots.TryGetSlot(gun, "gun_cell", out _))
                    _slots.TryEject(gun, "gun_cell", user, out _);
                break;
        }
    }

    private float GetReloadMultiplier(EntityUid user, EntityUid gun)
    {
        var mult = 1.0f;
        if (TryComp<FSWeaponUpgradeStateComponent>(gun, out var state))
            mult *= state.ReloadSpeedMultiplier * state.ResearchReloadMultiplier;
        return mult;
    }
}
