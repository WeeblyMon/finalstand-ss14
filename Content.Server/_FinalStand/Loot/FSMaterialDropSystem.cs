using Content.Server._FinalStand.GameTicking.Rules;
using Content.Shared._FinalStand.Loot;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Content.Shared.Stacks;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._FinalStand.Loot;

public sealed partial class FSMaterialDropSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private WaveGameRuleSystem _waves = default!;
    [Dependency] private Engineering.FSEngineeringTelemetrySystem _telemetry = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    private const int WavesUntilCleanup = 2;

    // Ceiling on drops loose on the floor at once; banked (picked-up) ones don't count.
    private const int MaxLooseDrops = 300;

    private int _wavesEnded;
    private readonly Queue<EntityUid> _looseDropOrder = new();
    private readonly HashSet<Entity<StackComponent>> _nearbyStacks = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMaterialDropComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<WaveEndedEvent>(OnWaveEnded);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _wavesEnded = 0;
        _looseDropOrder.Clear();
    }

    private void OnMobStateChanged(Entity<FSMaterialDropComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || ent.Comp.Materials.Count == 0)
            return;

        var chance = MathF.Min(ent.Comp.DropChance + ent.Comp.ChancePerWave * _waves.GetWaveNumber(),
            MathF.Max(ent.Comp.MaxChance, ent.Comp.DropChance));
        var multiplier = GetAmountMultiplier(_waves.GetScalingPlayers(), _waves.GetWaveNumber());
        var coords = _transform.GetMapCoordinates(ent);
        for (var i = 0; i < ent.Comp.Rolls; i++)
        {
            if (!_random.Prob(chance))
                continue;

            var proto = _random.Pick(ent.Comp.Materials);
            var amount = (int) multiplier + (_random.Prob(multiplier % 1f) ? 1 : 0);
            _telemetry.RecordDrop(proto, amount);
            DropMaterial(proto, amount, coords);
        }

        TrimLooseDrops();
    }

    public float GetAmountMultiplier(int players, int wave)
    {
        var playerBonus = Math.Max(0, players - _cfg.GetCVar(CCVars.FSMatDropBasePlayers)) * _cfg.GetCVar(CCVars.FSMatDropPerPlayer);
        var waveBonus = Math.Max(0, wave - _cfg.GetCVar(CCVars.FSMatDropBaseWave)) * _cfg.GetCVar(CCVars.FSMatDropPerWave);
        return 1f + MathF.Min(playerBonus + waveBonus, _cfg.GetCVar(CCVars.FSMatDropBonusCap));
    }

    private void DropMaterial(EntProtoId proto, int amount, MapCoordinates coords)
    {
        var drop = Spawn(proto, coords);
        if (!TryComp<StackComponent>(drop, out var stack))
        {
            Track(drop);
            for (var i = 1; i < amount; i++)
                Track(Spawn(proto, coords));
            return;
        }

        _stack.SetCount((drop, stack), amount);

        _nearbyStacks.Clear();
        _lookup.GetEntitiesInRange(coords, _cfg.GetCVar(CCVars.FSMatDropMergeRange), _nearbyStacks, LookupFlags.Dynamic | LookupFlags.Sundries);
        foreach (var other in _nearbyStacks)
        {
            if (other.Owner == drop
                || other.Comp.StackTypeId != stack.StackTypeId
                || !TryComp<FSWaveLootComponent>(other, out var loot)
                || _container.IsEntityInContainer(other))
                continue;

            if (_stack.TryMergeStacks((drop, stack), other.AsNullable(), out _))
                loot.DroppedOnWave = _wavesEnded;

            if (TerminatingOrDeleted(drop) || stack.Count <= 0)
                return;
        }

        Track(drop);
    }

    private void Track(EntityUid drop)
    {
        EnsureComp<FSWaveLootComponent>(drop).DroppedOnWave = _wavesEnded;
        _looseDropOrder.Enqueue(drop);
    }

    private void TrimLooseDrops()
    {
        while (_looseDropOrder.Count > MaxLooseDrops)
        {
            var oldest = _looseDropOrder.Dequeue();

            // Already picked up or gone - it's not floor litter anymore, leave it alone.
            if (!Exists(oldest) || _container.IsEntityInContainer(oldest))
                continue;

            QueueDel(oldest);
        }
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
}
