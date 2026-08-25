using System.Numerics;
using System.Text;
using Content.Server.Administration;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Administration;
using Content.Shared.Mobs.Components;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server._FinalStand.Commands;

[AdminCommand(AdminFlags.Debug)]
public sealed partial class FSDebugRevenantCommand : LocalizedEntityCommands
{
    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private Mobs.FSRevenantSystem _revenant = default!;

    private const string RevenantProto = "FSZombieRevenant";

    public override string Command => "fsrevenant";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "spawn":
                Spawn(shell, args);
                break;
            case "lock":
                Lock(shell, args);
                break;
            case "unlock":
                Unlock(shell);
                break;
            case "info":
                Info(shell);
                break;
            case "clear":
                Clear(shell);
                break;
            case "stats":
                shell.WriteLine(_revenant.GetTelemetry());
                break;
            case "resetstats":
                _revenant.ResetTelemetry();
                shell.WriteLine("Telemetry counters reset.");
                break;
            default:
                shell.WriteLine(Help);
                break;
        }
    }

    private void Spawn(IConsoleShell shell, string[] args)
    {
        if (!TryGetPlayerCoords(shell, out var coords, out var mapPos))
            return;

        var distance = 5f;
        if (args.Length > 1 && float.TryParse(args[1], out var parsed))
            distance = parsed;

        var spawnPos = new MapCoordinates(mapPos.Position + new Vector2(distance, 0f), mapPos.MapId);
        var uid = _ent.SpawnEntity(RevenantProto, spawnPos);
        shell.WriteLine($"Spawned {_ent.ToPrettyString(uid)} {distance} tiles east.");
    }

    private void Lock(IConsoleShell shell, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError($"Usage: fsrevenant lock <{string.Join('|', Enum.GetNames<FSRevenantAbility>())}>");
            return;
        }

        if (!Enum.TryParse<FSRevenantAbility>(args[1], true, out var ability))
        {
            shell.WriteError($"Unknown ability '{args[1]}'. Valid: {string.Join(", ", Enum.GetNames<FSRevenantAbility>())}");
            return;
        }

        var count = _revenant.SetDebugLock(ability);
        shell.WriteLine($"Locked {count} existing Revenant(s) to {ability}, and every one spawned from now on. " +
                        "They will use nothing else. Fired/refused transitions are logged to the server console.");
    }

    private void Unlock(IConsoleShell shell)
    {
        var count = _revenant.SetDebugLock(null);
        shell.WriteLine($"Unlocked {count} Revenant(s). New spawns are no longer locked either.");
    }

    private void Info(IConsoleShell shell)
    {
        var sb = new StringBuilder();
        var count = 0;

        var query = _ent.EntityQueryEnumerator<FSRevenantComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out _))
        {
            count++;
            var channel = comp.IsExecuting ? "EXECUTING"
                : comp.IsGrabPaused ? "GRAB-PAUSE"
                : "idle";

            var lockState = comp.LockedAbility is { } locked ? $" LOCKED:{locked}" : "";
            sb.AppendLine($"{_ent.ToPrettyString(uid)} [{channel}]{lockState} gcd={comp.GcdAccum:F2}/{comp.GlobalCooldown:F2}");
            sb.AppendLine($"  phase={comp.Phase} ({comp.PhaseAccum:F1}s)");
            sb.AppendLine($"  grab={Ready(comp.GrabAccum, comp.GrabCooldown)} bind={Ready(comp.BindAccum, comp.BindCooldown)} " +
                          $"slice={Ready(comp.SliceAccum, comp.SliceCooldown)} bolt={Ready(comp.BoltAccum, comp.BoltCooldown)}");
            sb.AppendLine($"  marked={(comp.MarkedTarget is { } m ? _ent.ToPrettyString(m) : "none")} " +
                          $"nextSlice={(comp.UseVerticalNext ? "vertical" : "diagonal")}");
        }

        if (count == 0)
            sb.AppendLine("No Revenants exist.");

        sb.Append(GetTelemetryLine());
        shell.WriteLine(sb.ToString().TrimEnd());
    }

    private void Clear(IConsoleShell shell)
    {
        var removed = 0;
        var query = _ent.EntityQueryEnumerator<FSRevenantComponent>();
        var doomed = new List<EntityUid>();
        while (query.MoveNext(out var uid, out _))
            doomed.Add(uid);

        foreach (var uid in doomed)
        {
            _ent.QueueDeleteEntity(uid);
            removed++;
        }

        shell.WriteLine($"Deleted {removed} Revenant(s).");
    }

    private string GetTelemetryLine() => _revenant.GetTelemetry();

    private static string Ready(float accum, float cooldown)
        => accum >= cooldown ? "READY" : $"{cooldown - accum:F1}s";

    private bool TryGetPlayerCoords(IConsoleShell shell, out EntityCoordinates coords, out MapCoordinates mapPos)
    {
        coords = default;
        mapPos = default;

        if (shell.Player?.AttachedEntity is not { } player)
        {
            shell.WriteError("This command requires an attached entity.");
            return false;
        }

        coords = _ent.GetComponent<TransformComponent>(player).Coordinates;
        mapPos = _transform.GetMapCoordinates(player);
        return mapPos.MapId != MapId.Nullspace;
    }
}
