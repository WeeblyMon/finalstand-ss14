using System.Linq;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Weapons;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.DoAfter;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server._FinalStand.SmartReload;

public sealed partial class FSSmartReloadSystem : EntitySystem
{
    private void ReloadTubeFed(EntityUid gun, EntityUid user, bool isChainReload = false)
    {
        if (!TryComp<BallisticAmmoProviderComponent>(gun, out var comp))
            return;

        if (HasComp<FSMinigunComponent>(gun))
        {
            if (!isChainReload)
                _popup.PopupEntity(Loc.GetString("fs-smart-reload-minigun"), gun, user);
            return;
        }

        if (comp.Count >= comp.Capacity)
        {
            if (!isChainReload)
                _popup.PopupEntity(Loc.GetString("fs-smart-reload-already-full"), gun, user);
            return;
        }

        if (HasMixedAmmoBallistic(comp))
        {
            if (!isChainReload)
                _popup.PopupEntity(Loc.GetString("fs-smart-reload-tube-mixed"), gun, user);
            return;
        }

        var shell = FindBestAmmo(user, comp.Whitelist, gun);
        if (shell == null)
        {
            if (!isChainReload)
                _popup.PopupEntity(Loc.GetString("fs-smart-reload-no-ammo"), gun, user);
            return;
        }

        if (_activeShellInserts.ContainsKey(gun))
            return;

        _reloadAborted.Remove(gun);

        if (HasComp<FSBulkLoaderComponent>(shell.Value))
        {
            StartBulkShellInsert(gun, user, shell.Value);
            return;
        }

        StartShellInsert(gun, user, shell.Value, isChainReload);
    }

    private void StartBulkShellInsert(EntityUid gun, EntityUid user, EntityUid box)
    {
        var insertTime = TryComp<FSWeaponUpgradeStateComponent>(gun, out var upg) && upg.SpeedLoaderEnabled
            ? TimeSpan.FromSeconds(0.05)
            : (TryComp<BallisticAmmoProviderComponent>(box, out var boxComp) ? boxComp.FillDelay : ShellInsertTime)
              * GetReloadMultiplier(user, gun);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, insertTime,
            new FSBulkShellInsertDoAfterEvent(), eventTarget: gun, used: box)
        {
            NeedHand      = true,
            BreakOnMove   = false,
            BreakOnDamage = false,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs, out var id))
        {
            _activeShellInserts[gun] = id.Value;
            SetReloading(gun, true);
        }
        else
        {
            EndShellInsert(gun);
        }
    }

    private void OnBulkShellInsertComplete(EntityUid gun, BallisticAmmoProviderComponent comp, FSBulkShellInsertDoAfterEvent args)
    {
        EndShellInsert(gun);

        if (args.Cancelled || args.Used == null || !args.User.IsValid())
            return;

        if (_reloadAborted.Remove(gun))
            return;

        var prevCount = comp.Count;
        _bulkLoader.BulkTransfer(args.Used.Value, gun, args.User);

        if (comp.Count > prevCount)
        {
            var reloaded = new FSGunReloadedEvent(gun, args.User);
            RaiseLocalEvent(ref reloaded);
        }
    }

    private void StartShellInsert(EntityUid gun, EntityUid user, EntityUid shell, bool isChainReload = false)
    {
        var insertTime = TryComp<FSWeaponUpgradeStateComponent>(gun, out var upg) && upg.SpeedLoaderEnabled
            ? TimeSpan.FromSeconds(0.05)
            : ShellInsertTime * GetReloadMultiplier(user, gun);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, insertTime,
            new FSShellInsertDoAfterEvent { IsChainReload = isChainReload }, eventTarget: gun, used: shell)
        {
            NeedHand      = true,
            BreakOnMove   = false,
            BreakOnDamage = false,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs, out var id))
        {
            _activeShellInserts[gun] = id.Value;
            SetReloading(gun, true);
        }
        else
        {
            EndShellInsert(gun);
        }
    }

    private void OnBallisticGunFired(EntityUid gun, BallisticAmmoProviderComponent _, AmmoShotEvent args)
    {
        if (_activeShellInserts.ContainsKey(gun))
            _reloadAborted.Add(gun);
    }

    private void OnBallisticRemoved(EntityUid gun, BallisticAmmoProviderComponent _, ComponentRemove args)
    {
        _activeShellInserts.Remove(gun);
        _reloadAborted.Remove(gun);
    }

    private void OnShellInsertComplete(EntityUid gun, BallisticAmmoProviderComponent comp, FSShellInsertDoAfterEvent args)
    {
        // Every way out but starting the next shell has to clear the insert, or the gun stays locked mid-reload.
        if (args.Cancelled
            || args.Used == null
            || !args.User.IsValid()
            || _reloadAborted.Remove(gun)
            || !TryResolveRound(args.Used.Value, out var toInsert))
        {
            EndShellInsert(gun);
            return;
        }

        var prevCount = comp.Count;
        _gunSystem.TryBallisticInsert((gun, comp), toInsert, args.User);

        if (comp.Count == prevCount)
        {
            EndShellInsert(gun);
            return;
        }

        var reloaded = new FSGunReloadedEvent(gun, args.User);
        RaiseLocalEvent(ref reloaded);

        if (comp.Count >= comp.Capacity)
        {
            EndShellInsert(gun);
            return;
        }

        var nextSource = NextChainSource(args.Used.Value, args.User, comp.Whitelist, gun);
        if (!nextSource.IsValid())
            EndShellInsert(gun);
        else if (HasComp<FSBulkLoaderComponent>(nextSource))
            StartBulkShellInsert(gun, args.User, nextSource);
        else
            StartShellInsert(gun, args.User, nextSource, args.IsChainReload);
    }

    private void EndShellInsert(EntityUid gun)
    {
        _activeShellInserts.Remove(gun);
        SetReloading(gun, false);
    }
}
