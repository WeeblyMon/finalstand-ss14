// The RCD picker. Ring layout: the Build Ring opens with your current piece under the cursor.
// Belt layout: Z steps to the next belt slot instead of opening anything.
using Content.Shared._FinalStand.RCD;
using Content.Shared.CCVar;
using Content.Shared.RCD;
using Content.Shared.RCD.Components;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.RCD;

public sealed class FSRCDMenuBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceCache _cache = default!;

    private static readonly SpriteSpecifier StationIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Radial/RCD/walls_and_flooring.png"));

    private FSBuildRing? _ring;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<RCDComponent>(Owner, out var rcd)
            || !EntMan.TryGetComponent<FSEngineerOnlyRCDComponent>(Owner, out var fs))
            return;

        var palette = new FSRCDPalette(_proto, rcd, fs);
        if (_cfg.GetCVar(CCVars.FSRcdBeltLayout))
        {
            EntMan.System<FSRCDBeltSystem>().Step(Owner, palette, 1);
            // Closing inside Open mutates the open-UI list the engine is iterating, so wait a frame.
            Timer.Spawn(0, Close);
            return;
        }

        var sprite = EntMan.System<SpriteSystem>();
        var look = new FSRCDLook(_cache, _proto, sprite);
        var user = _player.LocalEntity;
        var current = rcd.ProtoId.Id;

        var top = FSRCDLook.Entries(EntMan, _proto, sprite, user, fs, current, palette.Pieces);
        var stationSelected = palette.Station.Exists(p => p.ID == current);
        top.Add(new FSRCDEntry(FSRCDEntryKind.Station, null, null, -1, false, null, stationSelected));
        if (palette.Deconstruct != null)
            top.AddRange(FSRCDLook.Entries(EntMan, _proto, sprite, user, fs, current, [palette.Deconstruct]));

        var station = FSRCDLook.Entries(EntMan, _proto, sprite, user, fs, current, palette.Station);
        station.Add(new FSRCDEntry(FSRCDEntryKind.Back, null, null, -1, false, null, false));

        _ring = new FSBuildRing(look, EntMan, user, fs, top, station, sprite.Frame0(StationIcon),
            _input.MouseScreenPosition.Position);
        _ring.OnPick += entry =>
        {
            if (entry.Piece != null)
                SendMessage(new RCDSystemMessage(entry.Piece.ID));
            Close();
        };
        _ring.OnDismiss += Close;
        _ring.OnToggleLayout += () =>
        {
            EntMan.System<FSRCDBeltSystem>().SuppressSteps();
            _cfg.SetCVar(CCVars.FSRcdBeltLayout, true);
            _cfg.SaveToFile();
            Close();
        };
        _ui.PopupRoot.AddChild(_ring);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _ring?.Orphan();
        _ring = null;
    }
}
