using Content.Server._FinalStand.Perks;
using Content.Shared._FinalStand.Utility;
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
using Content.Shared._FinalStand.Weapons;
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
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedGunSystem _gunSystem = default!;
    [Dependency] private FSBulkLoaderSystem _bulkLoader = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private FSBloodloadSystem _bloodload = default!;

    private readonly Dictionary<EntityUid, DoAfterId> _activeShellInserts = new();

    private readonly HashSet<EntityUid> _reloadAborted = new();

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
        SubscribeNetworkEvent<FSLoadMagazineMessage>(OnLoadMagazine);
        SubscribeLocalEvent<MagazineAmmoProviderComponent, FSMagReloadDoAfterEvent>(OnMagReloadComplete);
        SubscribeLocalEvent<ChamberMagazineAmmoProviderComponent, FSMagReloadDoAfterEvent>(OnMagReloadComplete);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, FSShellInsertDoAfterEvent>(OnShellInsertComplete);
        SubscribeLocalEvent<BallisticAmmoProviderComponent, FSBulkShellInsertDoAfterEvent>(OnBulkShellInsertComplete);
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

    private void OnLoadMagazine(FSLoadMagazineMessage msg, EntitySessionEventArgs args)
    {
        if (!TryGetValidGun(msg.Gun, args.SenderSession, out var gun, out var user))
            return;

        if (Detect(gun) != GunArchetype.Magazine)
            return;

        var chosen = GetEntity(msg.Magazine);
        if (!chosen.IsValid() || TerminatingOrDeleted(chosen))
            return;

        if (!FSItemSlots.TryGetSlot(EntityManager, _slots, gun, SharedGunSystem.MagazineSlot, out var magSlot)
            || _whitelist.IsWhitelistFail(magSlot.Whitelist, chosen))
        {
            return;
        }

        ReloadMagazine(gun, user, chosen: chosen);
    }

    private float GetReloadMultiplier(EntityUid user, EntityUid gun)
    {
        var mult = _bloodload.GetReloadTimeMultiplier(user);
        if (TryComp<FSWeaponUpgradeStateComponent>(gun, out var state))
            mult *= state.ReloadSpeedMultiplier * state.ResearchReloadMultiplier;
        return mult;
    }
}
