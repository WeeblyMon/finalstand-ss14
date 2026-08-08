using Content.Shared._FinalStand.Mobs;
using Content.Shared.Mobs;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Mobs;

public sealed class FSDeathLootSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSDeathLootComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<FSDeathLootComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        var coords = Transform(ent).Coordinates;
        foreach (var drop in ent.Comp.Drops)
        {
            if (_random.Prob(drop.Chance))
                Spawn(drop.Proto, coords);
        }
    }
}
