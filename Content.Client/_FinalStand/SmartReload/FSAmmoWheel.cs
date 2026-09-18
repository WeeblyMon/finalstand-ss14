using Content.Shared._FinalStand.Utility;
using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.SmartReload;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Whitelist;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.SmartReload;

public sealed class FSAmmoWheel : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private ItemSlotsSystem _slots = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedContainerSystem _containers = default!;

    private SimpleRadialMenu? _menu;

    public bool IsOpen => _menu is { Disposed: false, Visible: true };

    public void Close()
    {
        _menu?.Close();
        _menu = null;
    }

    public bool TryOpen(EntityUid gun)
    {
        Close();

        if (_player.LocalSession?.AttachedEntity is not { } user)
            return false;

        if (!FSItemSlots.TryGetSlot(EntityManager, _slots, gun, SharedGunSystem.MagazineSlot, out var magSlot))
            return false;

        var loaded = magSlot.Item;
        var found = new List<EntityUid>();
        Collect(user, magSlot.Whitelist, loaded, found, depth: 0);

        if (found.Count == 0)
            return false;

        var groups = new List<(EntityUid Best, int BestCount, int Total)>();
        var index = new Dictionary<string, int>();

        foreach (var mag in found)
        {
            var key = MetaData(mag).EntityPrototype?.ID ?? Name(mag);
            var count = AmmoCount(mag);

            if (!index.TryGetValue(key, out var at))
            {
                index[key] = groups.Count;
                groups.Add((mag, count, 1));
                continue;
            }

            var (best, bestCount, total) = groups[at];
            groups[at] = count > bestCount ? (mag, count, total + 1) : (best, bestCount, total + 1);
        }

        var options = new List<RadialMenuOptionBase>(groups.Count);
        foreach (var (mag, _, total) in groups)
        {
            options.Add(new RadialMenuActionOption<EntityUid>(Select, mag)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(mag),
                ToolTip = Name(mag) + AmmoSuffix(mag) + (total > 1 ? $" x{total}" : string.Empty),
            });
        }

        _menu = _ui.CreateWindow<SimpleRadialMenu>();
        _menu.Track(gun);
        _menu.SetButtons(options);
        _menu.OpenOverMouseScreenPosition();
        return true;

        void Select(EntityUid chosen)
        {
            RaiseNetworkEvent(new FSLoadMagazineMessage
            {
                Gun = GetNetEntity(gun),
                Magazine = GetNetEntity(chosen),
            });
            Close();
        }
    }

    private void Collect(EntityUid root, EntityWhitelist? whitelist, EntityUid? loaded,
        List<EntityUid> into, int depth)
    {
        if (!TryComp<ContainerManagerComponent>(root, out var mgr))
            return;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (item != loaded
                    && !_whitelist.IsWhitelistFail(whitelist, item)
                    && !into.Contains(item))
                {
                    into.Add(item);
                }

                if (depth < 1)
                    Collect(item, whitelist, loaded, into, depth + 1);
            }
        }
    }

    private (int Count, int Capacity) Ammo(EntityUid mag)
    {
        var ev = new GetAmmoCountEvent();
        RaiseLocalEvent(mag, ref ev);
        return (ev.Count, ev.Capacity);
    }

    private int AmmoCount(EntityUid mag) => Ammo(mag).Count;

    private string AmmoSuffix(EntityUid mag)
    {
        var (count, capacity) = Ammo(mag);
        return capacity > 0 ? $" ({count}/{capacity})" : string.Empty;
    }
}
