using Content.Shared._FinalStand.Perks;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Perks;

// Flags entities whose fire is healing them so the client can recolour it.
public sealed partial class FSHealingFireSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private static readonly TimeSpan Linger = TimeSpan.FromSeconds(1.5);
    private readonly Dictionary<EntityUid, TimeSpan> _healing = new();
    private readonly List<EntityUid> _expired = new();

    public void MarkHealing(EntityUid uid)
    {
        if (!_healing.ContainsKey(uid))
            _appearance.SetData(uid, FSHealingFireVisuals.Healing, true);
        _healing[uid] = _timing.CurTime + Linger;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        if (_healing.Count == 0)
            return;

        var now = _timing.CurTime;
        foreach (var (uid, until) in _healing)
        {
            if (until <= now)
                _expired.Add(uid);
        }

        foreach (var uid in _expired)
        {
            _healing.Remove(uid);
            if (!TerminatingOrDeleted(uid))
                _appearance.SetData(uid, FSHealingFireVisuals.Healing, false);
        }
        _expired.Clear();
    }
}
