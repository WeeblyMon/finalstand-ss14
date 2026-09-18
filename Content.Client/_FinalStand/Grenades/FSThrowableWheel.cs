using Content.Client.UserInterface.Controls;
using Content.Shared._FinalStand.Grenades;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Containers;

namespace Content.Client._FinalStand.Grenades;

public sealed class FSThrowableWheel : EntitySystem
{
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    private SimpleRadialMenu? _menu;

    public void Close()
    {
        _menu?.Close();
        _menu = null;
    }

    public bool TryOpen()
    {
        Close();

        if (_player.LocalSession?.AttachedEntity is not { } user)
            return false;

        var active = CompOrNull<FSActiveGrenadeComponent>(user)?.ActiveType;

        var packs = new Dictionary<GrenadeType, (EntityUid Uid, int Stock)>();
        Collect(user, packs, depth: 0);

        if (packs.Count <= 1)
            return false;

        var options = new List<RadialMenuOptionBase>(packs.Count);
        foreach (var (type, pack) in packs)
        {
            var chosen = type;
            options.Add(new RadialMenuActionOption<GrenadeType>(Select, chosen)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(pack.Uid),
                ToolTip = $"{Name(pack.Uid)} ({pack.Stock})" + (type == active ? " - equipped" : string.Empty),
            });
        }

        _menu = _ui.CreateWindow<SimpleRadialMenu>();
        _menu.Track(user);
        _menu.SetButtons(options);
        _menu.OpenOverMouseScreenPosition();
        return true;

        void Select(GrenadeType type)
        {
            RaiseNetworkEvent(new FSSelectGrenadeMessage { Type = type });
            Close();
        }
    }

    private void Collect(EntityUid root, Dictionary<GrenadeType, (EntityUid, int)> into, int depth)
    {
        if (!TryComp<ContainerManagerComponent>(root, out var mgr))
            return;

        foreach (var container in mgr.Containers.Values)
        {
            foreach (var item in container.ContainedEntities)
            {
                if (TryComp<FSGrenadePackComponent>(item, out var pack)
                    && pack.Stock > 0
                    && !into.ContainsKey(pack.PackType))
                {
                    into[pack.PackType] = (item, pack.Stock);
                }

                if (depth < 1)
                    Collect(item, into, depth + 1);
            }
        }
    }
}
