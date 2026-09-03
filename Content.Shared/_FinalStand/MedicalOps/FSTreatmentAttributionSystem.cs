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

    public void RecordTreatment(EntityUid patient, EntityUid user, EntityUid? used = null)
    {
        if (_net.IsClient)
            return;

        if (!_mind.TryGetMind(user, out var userMind, out var mind) || mind.UserId == null)
            return;

        if (!HasComp<FSMedicalPatientComponent>(patient))
            return;

        var comp = EnsureComp<FSTreatmentAttributionComponent>(patient);
        var now = _timing.CurTime;

        PruneExpired(comp.RecentTreaters, now);
        comp.RecentTreaters[userMind] = now + AttributionWindow;

        if (used is not { } item
            || !TryComp<FSProducedByComponent>(item, out var produced)
            || !produced.ProducerMind.IsValid())
            return;

        PruneExpired(comp.RecentSuppliers, now);
        comp.RecentSuppliers[produced.ProducerMind] = now + AttributionWindow;
    }

    public void TagProducer(EntityUid item, EntityUid producer)
    {
        if (_net.IsClient)
            return;

        if (!_mind.TryGetMind(producer, out var producerMind, out var mind) || mind.UserId == null)
            return;

        EnsureComp<FSProducedByComponent>(item).ProducerMind = producerMind;
    }

    // A syringe drawn from a tagged bottle carries the chemist's claim with the solution.
    public void PropagateProducer(EntityUid from, EntityUid to)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<FSProducedByComponent>(from, out var source) || !source.ProducerMind.IsValid())
            return;

        EnsureComp<FSProducedByComponent>(to).ProducerMind = source.ProducerMind;
    }

    public void ClearAttribution(EntityUid patient)
    {
        if (!TryComp<FSTreatmentAttributionComponent>(patient, out var comp))
            return;

        comp.RecentTreaters.Clear();
        comp.RecentSuppliers.Clear();
    }

    public bool TryGetAttributedMedic(EntityUid patient, out EntityUid medicMind)
    {
        medicMind = default;
        return TryComp<FSTreatmentAttributionComponent>(patient, out var comp)
               && TryGetLatest(comp.RecentTreaters, out medicMind);
    }

    public bool TryGetAttributedSupplier(EntityUid patient, out EntityUid supplierMind)
    {
        supplierMind = default;
        return TryComp<FSTreatmentAttributionComponent>(patient, out var comp)
               && TryGetLatest(comp.RecentSuppliers, out supplierMind);
    }

    private bool TryGetLatest(Dictionary<EntityUid, TimeSpan> claims, out EntityUid mindId)
    {
        mindId = default;

        var now = _timing.CurTime;
        var latest = TimeSpan.MinValue;
        var found = false;

        foreach (var (claimant, expiry) in claims)
        {
            if (expiry <= now || expiry <= latest)
                continue;

            latest = expiry;
            mindId = claimant;
            found = true;
        }

        return found;
    }

    private static void PruneExpired(Dictionary<EntityUid, TimeSpan> claims, TimeSpan now)
    {
        var expired = claims.Where(kv => kv.Value <= now).Select(kv => kv.Key).ToList();
        foreach (var claimant in expired)
            claims.Remove(claimant);
    }
}
