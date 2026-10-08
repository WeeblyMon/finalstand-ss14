// Keeps a job audible while it runs: plays a short cycle of tool sounds at the work site until it finishes or is cancelled.
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Effects;

public sealed class FSWorkSoundSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(0.9);

    private readonly Dictionary<EntityUid, Job> _jobs = new();
    private readonly List<EntityUid> _done = new();

    private sealed class Job
    {
        public EntityCoordinates At;
        public SoundSpecifier[] Sounds = default!;
        public TimeSpan Until;
        public TimeSpan Next;
        public int Index;
    }

    public void Start(EntityUid key, EntityCoordinates at, SoundSpecifier[] sounds, float seconds)
    {
        if (sounds.Length == 0)
            return;

        var now = _timing.CurTime;
        _jobs[key] = new Job { At = at, Sounds = sounds, Until = now + TimeSpan.FromSeconds(seconds), Next = now };
    }

    public void Stop(EntityUid key)
    {
        _jobs.Remove(key);
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        foreach (var (key, job) in _jobs)
        {
            if (now >= job.Until || TerminatingOrDeleted(key))
            {
                _done.Add(key);
                continue;
            }

            if (now < job.Next)
                continue;

            job.Next = now + Interval;
            _audio.PlayPvs(job.Sounds[job.Index++ % job.Sounds.Length], job.At, AudioParams.Default.WithVariation(0.08f).WithVolume(-3f));
        }

        foreach (var key in _done)
            _jobs.Remove(key);
        _done.Clear();
    }
}
