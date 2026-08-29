using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server._FinalStand.Science;
using Content.Shared._FinalStand.Loot;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Loot;

public sealed class FSMaterialDropSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private FSScienceOnlySystem _science = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private const int WavesUntilCleanup = 2;

    private int _wavesEnded;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMaterialDropComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _) => _wavesEnded = 0;

    private void OnMobStateChanged(Entity<FSMaterialDropComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Materials.Count == 0)
            return;

        var chance = MathF.Min(ent.Comp.MaxChance,
            ent.Comp.BaseChance + ent.Comp.ChancePerScientist * CountScientists());

        if (!_random.Prob(chance))
            return;

        var drop = Spawn(_random.Pick(ent.Comp.Materials), Transform(ent).Coordinates);
        EnsureComp<FSWaveLootComponent>(drop).DroppedOnWave = _wavesEnded;
    }

    private void OnWaveEnded(ref WaveEndedEvent args)
    {
        _wavesEnded++;

        var query = EntityQueryEnumerator<FSWaveLootComponent>();
        while (query.MoveNext(out var uid, out var loot))
        {
            if (_wavesEnded - loot.DroppedOnWave < WavesUntilCleanup)
                continue;

            // Anything a player has banked is theirs; only litter left on the floor is swept up.
            if (_container.IsEntityInContainer(uid))
                continue;

            QueueDel(uid);
        }
    }

    private int CountScientists()
    {
        var count = 0;
        foreach (var session in _player.Sessions)
        {
            if (session.AttachedEntity is { } mob && _science.IsScience(mob))
                count++;
        }

        return count;
    }
}
