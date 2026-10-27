using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Ailments;

public sealed partial class FSSkippedBeatSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISharedPlayerManager _player = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSSkippedBeatComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<FSSkippedBeatComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextBeat = _timing.CurTime + ent.Comp.Interval;
        Dirty(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSkippedBeatComponent, StatusEffectComponent>();
        while (query.MoveNext(out var uid, out var beat, out var status))
        {
            if (now < beat.NextBeat
                || status.AppliedTo is not { } patient
                || _net.IsClient && patient != _player.LocalEntity)
            {
                continue;
            }

            beat.NextBeat = now + beat.Interval;
            Dirty(uid, beat);

            if (_mobState.IsDead(patient))
                continue;

            _movementMod.TryUpdateMovementSpeedModDuration(patient, beat.Slowdown, beat.Duration, beat.SpeedScale);
            _audio.PlayLocal(beat.Sound, patient, patient);
            if (_net.IsServer && beat.Spark is { } spark)
                Spawn(spark, Transform(patient).Coordinates);
        }
    }
}
