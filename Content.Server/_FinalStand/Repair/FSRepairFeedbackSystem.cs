// Tells a repairer how much they fixed, summed per structure, and when a rivet bounced off the repair cap.
using Content.Shared._FinalStand.Repair;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Repair;

public sealed partial class FSRepairFeedbackSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private static readonly TimeSpan Window = TimeSpan.FromSeconds(0.5);
    private static readonly TimeSpan CapCooldown = TimeSpan.FromSeconds(1.5);

    private readonly Dictionary<(EntityUid Target, EntityUid User), (float Amount, TimeSpan Flush)> _pending = new();
    private readonly Dictionary<EntityUid, TimeSpan> _capSentAt = new();
    private readonly List<(EntityUid Target, EntityUid User)> _due = new();

    public void Repaired(EntityUid target, EntityUid? user, float amount)
    {
        if (user is not { } who || amount <= 0f)
            return;

        var key = (target, who);
        _pending[key] = _pending.TryGetValue(key, out var open)
            ? (open.Amount + amount, open.Flush)
            : (amount, _timing.CurTime + Window);
    }

    public void CapReached(EntityUid target, EntityUid? user)
    {
        if (user is not { } who)
            return;

        if (_capSentAt.TryGetValue(who, out var last) && _timing.CurTime - last < CapCooldown)
            return;

        _capSentAt[who] = _timing.CurTime;
        RaiseNetworkEvent(new FSRivetCapEvent(GetNetEntity(target)), Filter.Entities(who));
    }

    public override void Update(float frameTime)
    {
        if (_pending.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (key, open) in _pending)
        {
            if (now >= open.Flush)
                _due.Add(key);
        }

        foreach (var key in _due)
        {
            var amount = _pending[key].Amount;
            _pending.Remove(key);
            if (!TerminatingOrDeleted(key.Target) && !TerminatingOrDeleted(key.User))
                RaiseNetworkEvent(new FSRepairNumberEvent(GetNetEntity(key.Target), amount), Filter.Entities(key.User));
        }

        _due.Clear();
    }
}
