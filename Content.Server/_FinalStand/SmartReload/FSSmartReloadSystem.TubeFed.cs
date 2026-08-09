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
                _popup.PopupEntity("Minigun can only be reloaded from ammo resupply.", gun, user);
            return;
        }

        if (comp.Count >= comp.Capacity)
        {
            if (!isChainReload)
                _popup.PopupEntity("Already full.", gun, user);
            return;
        }

        if (HasMixedAmmoBallistic(comp))
        {
            if (!isChainReload)
                _popup.PopupEntity("Mixed ammo loaded — reload manually.", gun, user);
            return;
        }

        var shell = FindBestAmmo(user, comp.Whitelist, gun);
        if (shell == null)
        {
            if (!isChainReload)
                _popup.PopupEntity("No compatible ammo found.", gun, user);
            return;
        }

        if (_activeShellInserts.ContainsKey(gun))
            return;

        _reloadAborted.Remove(gun);
        StartShellInsert(gun, user, shell.Value, isChainReload);
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
            _activeShellInserts.Remove(gun);
            SetReloading(gun, false);
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
        if (args.Cancelled || args.Used == null || !args.User.IsValid())
        {
            _activeShellInserts.Remove(gun);
            SetReloading(gun, false);
            return;
        }

        if (_reloadAborted.Remove(gun))
        {
            _activeShellInserts.Remove(gun);
            SetReloading(gun, false);
            return;
        }

        if (!TryResolveRound(args.Used.Value, out var toInsert))
            return;

        var prevCount = comp.Count;
        _gunSystem.TryBallisticInsert((gun, comp), toInsert, args.User);

        if (comp.Count > prevCount)
        {
            var reloaded = new FSGunReloadedEvent(gun, args.User);
            RaiseLocalEvent(ref reloaded);
        }

        if (comp.Count == prevCount)
        {
            _activeShellInserts.Remove(gun);
            SetReloading(gun, false);
            return; // insert failed — stop chain
        }

        if (comp.Count >= comp.Capacity)
        {
            _activeShellInserts.Remove(gun);
            SetReloading(gun, false);
            return;
        }

        var nextSource = NextChainSource(args.Used.Value, args.User, comp.Whitelist, gun);
        if (nextSource.IsValid())
            StartShellInsert(gun, args.User, nextSource, args.IsChainReload);
        else
        {
            _activeShellInserts.Remove(gun);
            SetReloading(gun, false);
        }
    }

    private void DumpAllTubeShells(EntityUid gun, BallisticAmmoProviderComponent comp)
    {
        var coords = Transform(gun).Coordinates;

#pragma warning disable RA0002
        foreach (var entity in comp.Entities.ToList())
            _containers.Remove((entity, null, null), comp.Container, force: true);

        comp.Entities.Clear();

        if (comp.Proto != null)
        {
            for (var i = 0; i < comp.UnspawnedCount; i++)
                Spawn(comp.Proto.Value, coords);
        }

        comp.UnspawnedCount = 0;
#pragma warning restore RA0002
        Dirty(gun, comp);
    }
}
