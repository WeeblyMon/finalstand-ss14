using System.Linq;
using Content.Shared.Mind;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

// Records who administered medicine, so chem healing can be credited once it metabolises.
public sealed partial class FSTreatmentAttributionSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private IGameTiming _timing = default!;

    public static readonly TimeSpan AttributionWindow = TimeSpan.FromSeconds(30);

    public void RecordTreatment(EntityUid patient, EntityUid user)
    {
        if (_net.IsClient)
            return;

        if (!_mind.TryGetMind(user, out var userMind, out var mind) || mind.UserId == null)
            return;

        if (!HasComp<FSMedicalPatientComponent>(patient))
            return;

        var comp = EnsureComp<FSTreatmentAttributionComponent>(patient);
        var now = _timing.CurTime;

        var expired = comp.RecentTreaters
            .Where(kv => kv.Value <= now)
            .Select(kv => kv.Key)
            .ToList();
        foreach (var treater in expired)
            comp.RecentTreaters.Remove(treater);

        comp.RecentTreaters[userMind] = now + AttributionWindow;
    }

    public bool TryGetAttributedMedic(EntityUid patient, out EntityUid medicMind)
    {
        medicMind = default;

        if (!TryComp<FSTreatmentAttributionComponent>(patient, out var comp))
            return false;

        var now = _timing.CurTime;
        var latest = TimeSpan.MinValue;
        var found = false;

        foreach (var (treater, expiry) in comp.RecentTreaters)
        {
            if (expiry <= now || expiry <= latest)
                continue;

            latest = expiry;
            medicMind = treater;
            found = true;
        }

        return found;
    }
}
