using System.Linq;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Titles;

[AnyCommand]
public sealed partial class FSTitleCommand : LocalizedEntityCommands
{
    [Dependency] private FSTitleSystem _titles = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override string Command => "title";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("cmd-title-no-player"));
            return;
        }

        if (args.Length == 0)
        {
            var owned = _titles.GetOwned(player.UserId);
            if (owned.Count == 0)
            {
                shell.WriteLine(Loc.GetString("cmd-title-none-owned"));
                return;
            }

            var equipped = _titles.GetEquipped(player.UserId);
            shell.WriteLine(Loc.GetString("cmd-title-list-header"));
            foreach (var id in owned)
            {
                var proto = _proto.Index(id);
                shell.WriteLine(Loc.GetString(id == equipped ? "cmd-title-list-entry-equipped" : "cmd-title-list-entry",
                    ("title", Loc.GetString(proto.Name)),
                    ("description", Loc.GetString(proto.Description))));
            }
            return;
        }

        if (string.Equals(argStr.Trim(), "none", StringComparison.OrdinalIgnoreCase))
        {
            _titles.Equip(player.UserId, null);
            shell.WriteLine(Loc.GetString("cmd-title-cleared"));
            return;
        }

        if (!_titles.TryResolve(player.UserId, argStr.Trim(), out var titleId))
        {
            shell.WriteError(Loc.GetString("cmd-title-not-owned", ("query", argStr.Trim())));
            return;
        }

        _titles.Equip(player.UserId, titleId);
        shell.WriteLine(Loc.GetString("cmd-title-equipped", ("title", Loc.GetString(_proto.Index(titleId).Name))));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1 || shell.Player is not { } player)
            return CompletionResult.Empty;

        var options = _titles.GetOwned(player.UserId)
            .Select(id => new CompletionOption(Loc.GetString(_proto.Index(id).Name)))
            .Append(new CompletionOption("none"));
        return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-title-hint"));
    }
}
