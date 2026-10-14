using Content.Shared.Administration;
using Content.Shared.CCVar;
using JetBrains.Annotations;
using Robust.Client.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Client._FinalStand.Weapons.Prediction;

[UsedImplicitly, AnyCommand]
public sealed partial class FSNetLagCommand : LocalizedCommands
{
    [Dependency] private IClientGameTiming _timing = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntitySystemManager _systems = default!;

    public override string Command => "fsnetlag";

    public override string Help => LocalizationManager.GetString($"cmd-{Command}-help");

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var ticks = _timing.CurTick.Value - _timing.LastRealTick.Value + 0.5;
        var ms = ticks * _timing.TickPeriod.TotalMilliseconds;
        var prediction = _cfg.GetCVar(CCVars.FSShotPrediction) ? "cmd-fsnetlag-on" : "cmd-fsnetlag-off";

        shell.WriteLine(LocalizationManager.GetString($"cmd-{Command}-result",
            ("ticks", ticks.ToString("0.0")),
            ("ms", ((int) ms).ToString()),
            ("ping", (_net.ServerChannel?.Ping ?? 0).ToString()),
            ("prediction", LocalizationManager.GetString(prediction))));

        if (_systems.GetEntitySystem<FSShotPredictionSystem>().LastServerBulletDelay is { } delay)
            shell.WriteLine(LocalizationManager.GetString($"cmd-{Command}-shot", ("ms", ((int) delay.TotalMilliseconds).ToString())));
    }
}
