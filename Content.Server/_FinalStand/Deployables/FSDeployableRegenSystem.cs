using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.GameTicking;

namespace Content.Server._FinalStand.Deployables;

// Mirrors FSGrenadeRegenSystem - Stock regenerates at the start of each wave's prep phase.
public sealed class FSDeployableRegenSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WavePrepStartedEvent>(OnWavePrepStarted);
    }

    private void OnWavePrepStarted(WavePrepStartedEvent _)
    {
        var query = EntityQueryEnumerator<FSDeployableItemComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Stock >= comp.MaxStock)
                continue;

            comp.Stock = Math.Min(comp.MaxStock, comp.Stock + comp.RegenPerWave);
            Dirty(uid, comp);
        }
    }
}
