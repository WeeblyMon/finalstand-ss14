using Content.Shared.Input;
using Content.Shared._FinalStand.Grenades;
using Robust.Shared.Containers;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudSystem
{
    private readonly List<(GrenadeType Type, int Stock)> _carriedPacks = new();
    private readonly Dictionary<GrenadeType, string> _throwableNames = new();

    private void UpdateThrowables(WaveHudOverlay overlay)
    {
        overlay.ThrowableName = null;
        overlay.ThrowableStock = 0;
        overlay.ThrowableHasChoice = false;
        overlay.QuickGrenadeKey = KeyText(ContentKeyFunctions.QuickGrenade, "G");

        if (_player.LocalEntity is not { } player)
            return;

        CollectPacks(player);
        if (_carriedPacks.Count == 0)
            return;

        var active = CompOrNull<FSActiveGrenadeComponent>(player)?.ActiveType ?? _carriedPacks[0].Type;

        var index = 0;
        for (var i = 0; i < _carriedPacks.Count; i++)
        {
            if (_carriedPacks[i].Type != active)
                continue;

            index = i;
            break;
        }

        var (type, stock) = _carriedPacks[index];
        if (!_throwableNames.TryGetValue(type, out var name))
            _throwableNames[type] = name = Loc.GetString($"fs-throwable-{type.ToString().ToLowerInvariant()}");

        overlay.ThrowableName = name;
        overlay.ThrowableStock = stock;
        overlay.ThrowableHasChoice = _carriedPacks.Count > 1;
    }

    private void OnThrowableCycle(int delta)
    {
        if (_player.LocalEntity is not { } player || _carriedPacks.Count <= 1)
            return;

        var active = CompOrNull<FSActiveGrenadeComponent>(player)?.ActiveType ?? _carriedPacks[0].Type;
        var index = _carriedPacks.FindIndex(p => p.Type == active);
        if (index < 0)
            index = 0;

        var next = (index + delta + _carriedPacks.Count) % _carriedPacks.Count;
        RaiseNetworkEvent(new FSSelectGrenadeMessage { Type = _carriedPacks[next].Type });
    }

    private void CollectPacks(EntityUid player)
    {
        _carriedPacks.Clear();
        Walk(player, 0);
        _carriedPacks.Sort((a, b) => a.Type.CompareTo(b.Type));

        void Walk(EntityUid root, int depth)
        {
            if (!TryComp<ContainerManagerComponent>(root, out var mgr))
                return;

            foreach (var container in mgr.Containers.Values)
            {
                foreach (var item in container.ContainedEntities)
                {
                    if (TryComp<FSGrenadePackComponent>(item, out var pack)
                        && !_carriedPacks.Exists(p => p.Type == pack.PackType))
                    {
                        _carriedPacks.Add((pack.PackType, pack.Stock));
                    }

                    if (depth < 1)
                        Walk(item, depth + 1);
                }
            }
        }
    }
}
