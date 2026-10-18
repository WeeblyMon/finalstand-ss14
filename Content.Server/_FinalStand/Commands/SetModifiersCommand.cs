using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Administration;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Round)]
public sealed partial class SetModifiersCommand : LocalizedEntityCommands
{
    [Dependency] private WaveGameRuleSystem _waveRule = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override string Command => "setmodifiers";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var ids = args.Select(a => new ProtoId<FSRoundModifierPrototype>(a)).ToList();
        if (ids.Any(id => !_proto.HasIndex(id)))
        {
            shell.WriteError(Loc.GetString("cmd-setmodifiers-unknown"));
            return;
        }

        if (!_waveRule.TrySetModifiers(ids))
            shell.WriteError(Loc.GetString("cmd-setmodifiers-refused"));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        => CompletionResult.FromOptions(CompletionHelper.PrototypeIDs<FSRoundModifierPrototype>());
}
