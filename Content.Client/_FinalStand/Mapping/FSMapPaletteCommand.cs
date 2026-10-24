using Content.Client.Administration.Managers;
using Robust.Shared.Console;

namespace Content.Client._FinalStand.Mapping;

public sealed partial class FSMapPaletteCommand : LocalizedCommands
{
    [Dependency] private IClientAdminManager _admin = default!;

    private FSMapPaletteWindow? _window;

    public override string Command => "fspalette";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_admin.IsActive())
        {
            shell.WriteError(Loc.GetString("cmd-fspalette-denied"));
            return;
        }

        if (_window != null)
        {
            _window.Close();
            _window = null;
            return;
        }

        _window = new FSMapPaletteWindow();
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }
}
