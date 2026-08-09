using Content.Shared._FinalStand.SmartReload;
using Content.Shared.DoAfter;
using Content.Shared.Weapons.Ranged.Components;

namespace Content.Server._FinalStand.SmartReload;

public sealed partial class FSSmartReloadSystem : EntitySystem
{
    private void ReloadRevolver(EntityUid gun, EntityUid user, bool isChainReload = false)
    {
        if (!TryComp<RevolverAmmoProviderComponent>(gun, out var comp))
            return;

        var empty = CountEmptyChambers(comp);     // null chambers TryRevolverInsert can fill
        var spent = CountSpentChambers(comp);     // false chambers (fired cases)

        if (empty == 0 && spent == 0)
        {
            if (!isChainReload)
                _popup.PopupEntity("Cylinder is full.", gun, user);
            return;
        }

        if (HasMixedRevolverAmmo(comp))
        {
            if (!isChainReload)
                _popup.PopupEntity("Mixed ammo in cylinder — reload manually.", gun, user);
            return;
        }

        var source = FindBestAmmo(user, comp.Whitelist, gun);
        if (source == null)
        {
            if (!isChainReload)
                _popup.PopupEntity("No compatible ammo found.", gun, user);
            return;
        }

        if (_activeChamberFills.ContainsKey(gun))
            return;

        // TryRevolverInsert only accepts null chambers — eject spent cases first.
        if (empty == 0 && spent > 0)
            _gunSystem.EmptyRevolver((gun, comp), user);

        StartChamberFill(gun, user, source.Value, isChainReload);
    }

    private void StartChamberFill(EntityUid gun, EntityUid user, EntityUid round, bool isChainReload = false)
    {
        var doAfterArgs = new DoAfterArgs(EntityManager, user, ChamberFillTime * GetReloadMultiplier(user, gun),
            new FSChamberFillDoAfterEvent { IsChainReload = isChainReload }, eventTarget: gun, used: round)
        {
            NeedHand           = true,
            BreakOnMove        = false,
            BreakOnDamage      = false,
            BlockDuplicate     = true,
            DuplicateCondition = DuplicateConditions.SameEvent | DuplicateConditions.SameTarget,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs, out var id))
        {
            _activeChamberFills[gun] = id.Value;
            SetReloading(gun, true);
        }
        else
        {
            _activeChamberFills.Remove(gun);
            SetReloading(gun, false);
        }
    }

    private void OnChamberFillComplete(EntityUid gun, RevolverAmmoProviderComponent comp, FSChamberFillDoAfterEvent args)
    {
        if (args.Cancelled || args.Used == null || !args.User.IsValid())
        {
            _activeChamberFills.Remove(gun);
            SetReloading(gun, false);
            return;
        }

        if (!TryResolveRound(args.Used.Value, out var toInsert))
        {
            _activeChamberFills.Remove(gun);
            SetReloading(gun, false);
            return;
        }

        var prevNull = CountNullChambers(comp);
        _gunSystem.TryRevolverInsert((gun, comp), toInsert, args.User);

        if (CountNullChambers(comp) == prevNull)
        {
            _activeChamberFills.Remove(gun);
            SetReloading(gun, false);
            return;
        }

        var reloaded = new FSGunReloadedEvent(gun, args.User);
        RaiseLocalEvent(ref reloaded);

        if (CountNullChambers(comp) == 0)
        {
            _activeChamberFills.Remove(gun);
            SetReloading(gun, false);
            return;
        }

        var nextSource = NextChainSource(args.Used.Value, args.User, comp.Whitelist, gun);
        if (nextSource.IsValid())
            StartChamberFill(gun, args.User, nextSource, args.IsChainReload);
        else
        {
            _activeChamberFills.Remove(gun);
            SetReloading(gun, false);
        }
    }
}
