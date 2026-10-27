using Content.Shared._FinalStand.Ailments;
using Content.Shared.Movement.Systems;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Ailments;

public sealed partial class FSSkippedBeatSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSSkippedBeatComponent, StatusEffectComponent>();
        while (query.MoveNext(out var beat, out var status))
        {
            if (beat.NextBeat == TimeSpan.Zero)
            {
                beat.NextBeat = now + beat.Interval;
                continue;
            }

            if (now < beat.NextBeat || status.AppliedTo is not { } patient)
                continue;

            beat.NextBeat = now + beat.Interval;
            _movementMod.TryUpdateMovementSpeedModDuration(patient, beat.Slowdown, beat.Duration, beat.SpeedScale);
            _audio.PlayEntity(beat.Sound, patient, patient);
            if (beat.Spark is { } spark)
                Spawn(spark, Transform(patient).Coordinates);
        }
    }
}
