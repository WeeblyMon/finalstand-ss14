using System.Linq;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Perks;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSPerkLockSystem : EntitySystem
{
    public static readonly string[] LockingPerks = ["Medievalist"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
        SubscribeLocalEvent<WavePrepStartedEvent>(OnPrepStarted);
    }

    private void OnCombatStarted(WaveCombatStartedEvent args)
    {
        var query = EntityQueryEnumerator<FSPerkLevelsComponent>();
        while (query.MoveNext(out var mindId, out var perks))
            LockSlotted(mindId, perks);
    }

    private void OnPrepStarted(WavePrepStartedEvent args)
    {
        var query = EntityQueryEnumerator<FSPerkLockComponent>();
        while (query.MoveNext(out _, out var locks))
            locks.Locked.Clear();
    }

    public void LockSlotted(EntityUid mindId, FSPerkLevelsComponent perks)
    {
        foreach (var id in LockingPerks)
        {
            if (perks.GetSlottedLevel(id) > 0)
                EnsureComp<FSPerkLockComponent>(mindId).Locked.Add(id);
        }
    }

    public bool IsLocked(EntityUid mindId, string perkId)
        => TryComp<FSPerkLockComponent>(mindId, out var locks) && locks.Locked.Contains(perkId);

    public bool AnyLocked(EntityUid mindId)
        => TryComp<FSPerkLockComponent>(mindId, out var locks) && locks.Locked.Count > 0;

    public bool KeepsLocked(EntityUid mindId, IReadOnlyCollection<string> slots)
    {
        if (!TryComp<FSPerkLockComponent>(mindId, out var locks))
            return true;

        foreach (var id in locks.Locked)
        {
            if (!slots.Contains(id))
                return false;
        }

        return true;
    }
}

[RegisterComponent]
public sealed partial class FSPerkLockComponent : Component
{
    public HashSet<string> Locked = new();
}
