using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Deployables;

namespace Content.Server._FinalStand.Deployables;

// Mirrors FSGrenadeRegenSystem - Stock regenerates at the start of each wave's prep phase.
public sealed partial class FSDeployableRegenSystem : EntitySystem
{
    [Dependency] private Perks.FSTechnicianSystem _technician = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnWavePrepStarted);
    }

    private void OnWavePrepStarted(ref WavePrepStartedEvent _)
    {
        var query = EntityQueryEnumerator<FSDeployableItemComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var bonus = _technician.GetBonusForItem(uid, comp);
            if (bonus > 0)
            {
                var max = comp.MaxStock + bonus;
                if (comp.Stock < max)
                {
                    comp.Stock = max;
                    Dirty(uid, comp);
                }
                continue;
            }

            if (comp.Stock >= comp.MaxStock)
                continue;

            if (++comp.WavesSinceRegen < comp.WavesPerRegen)
                continue;

            comp.WavesSinceRegen = 0;
            comp.Stock = Math.Min(comp.MaxStock, comp.Stock + comp.RegenPerWave);
            Dirty(uid, comp);
        }
    }
}
