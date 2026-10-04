using Content.Client._FinalStand.RCD;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.Materials;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Fortifications;

public sealed partial class FSUpgradeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IResourceCache _cache = default!;

    private FSUpgradeRing? _ring;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<FSUpgradeableComponent>(Owner, out var comp))
            return;

        var sprite = EntMan.System<SpriteSystem>();
        var rules = EntMan.System<FSUpgradeSystem>();
        var materials = EntMan.System<FSCarriedMaterialSystem>();
        var user = _player.LocalEntity;

        var entries = new List<FSUpgradeEntry>();
        foreach (var id in comp.Options)
        {
            var upgrade = _proto.Index(id);
            var affordable = true;
            foreach (var (stack, need) in upgrade.Cost)
            {
                if (user is not { } u || materials.Count(u, stack) < need)
                    affordable = false;
            }

            entries.Add(new FSUpgradeEntry(upgrade, sprite.Frame0(upgrade.Icon), rules.Check(comp, upgrade), comp.Pending == id, !affordable));
        }

        _ring = new FSUpgradeRing(new FSRCDLook(_cache, _proto, sprite), EntMan, user, comp, entries, _input.MouseScreenPosition.Position);
        _ring.OnPick += entry =>
        {
            SendMessage(new FSUpgradeSelectMessage(entry.Upgrade.ID));
            Close();
        };
        _ring.OnDismiss += Close;
        _ui.PopupRoot.AddChild(_ring);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _ring?.Orphan();
        _ring = null;
    }
}
