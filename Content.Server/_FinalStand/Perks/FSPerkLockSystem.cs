using System.Linq;
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Perks;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSPerkLockSystem : EntitySystem
{
    public static readonly string[] LockingPerks = ["Medievalist"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WaveCombatStartedEvent>(OnCombatStarted);
    }

    private void OnCombatStarted(ref WaveCombatStartedEvent args)
    {
        var query = EntityQueryEnumerator<FSPerkLevelsComponent>();
        while (query.MoveNext(out var mindId, out var perks))
            LockSlotted(mindId, perks);
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
