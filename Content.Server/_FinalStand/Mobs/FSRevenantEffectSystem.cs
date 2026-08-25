using System.Numerics;
using Robust.Shared.Audio.Systems;
using Content.Shared.GameTicking;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mobs;

public enum FSRevenantEffect : byte
{
    Death,
    Kill,
    Mark,
    Grab,
    Bind,
    SliceVertical,
    SliceDiagonal,
    Hit,
    Scythe,
}

public sealed partial class FSRevenantEffectSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IGameTiming _timing = default!;

    private const string ExecuteSound = "/Audio/_FinalStand/Mobs/Revenant/execute.ogg";
    private const string GrabSound = "/Audio/_FinalStand/Mobs/Revenant/grab.ogg";
    private const string SliceSound = "/Audio/_FinalStand/Mobs/Revenant/slice.ogg";
    private const string KillLaughSound = "/Audio/_FinalStand/Mobs/Revenant/deathlaugh.ogg";
    private const string BlastSound = "/Audio/_FinalStand/Mobs/Revenant/blast.ogg";
    private const string BindSound = "/Audio/_FinalStand/Mobs/Revenant/bind.ogg";
    private const string MarkSound = "/Audio/_FinalStand/Mobs/Revenant/mark.ogg";
    private const string VanishSound = "/Audio/_FinalStand/Mobs/Revenant/vanish.ogg";

    private readonly List<PendingEffect> _pending = new();

    private readonly record struct PendingEffect(
        TimeSpan At,
        FSRevenantEffect Effect,
        EntityUid? Parent,
        MapCoordinates Where,
        Vector2? Facing);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    public void SpawnEffect(FSRevenantEffect effect, MapCoordinates at, Vector2? facing = null, float delay = 0f)
    {
        if (at.MapId == MapId.Nullspace)
            return;

        if (delay > 0f)
        {
            _pending.Add(new PendingEffect(_timing.CurTime + TimeSpan.FromSeconds(delay), effect, null, at, facing));
            return;
        }

        var ent = Spawn(PrototypeFor(effect), at);

        if (!IsDirectional(effect) || facing is not { } dir || dir.LengthSquared() <= 0.001f)
            return;

        _transform.SetWorldRotation(ent, new Angle(MathF.Atan2(dir.Y, dir.X)));
    }

    public void SpawnAttached(FSRevenantEffect effect, EntityUid parent, Vector2 localOffset = default, float delay = 0f)
    {
        if (TerminatingOrDeleted(parent))
            return;

        if (delay > 0f)
        {
            _pending.Add(new PendingEffect(_timing.CurTime + TimeSpan.FromSeconds(delay), effect, parent, default, null));
            return;
        }

        var ent = Spawn(PrototypeFor(effect), new EntityCoordinates(parent, Vector2.Zero));
        _transform.SetLocalPosition(ent, localOffset);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pending.Count == 0)
            return;

        var now = _timing.CurTime;
        for (var i = _pending.Count - 1; i >= 0; i--)
        {
            if (now < _pending[i].At)
                continue;

            var pending = _pending[i];
            _pending.RemoveAt(i);

            if (pending.Parent is { } parent)
                SpawnAttached(pending.Effect, parent);
            else
                SpawnEffect(pending.Effect, pending.Where, pending.Facing);
        }
    }

    private void OnRoundRestart(RoundRestartCleanupEvent args)
    {
        _pending.Clear();
    }

    public void PlayExecuteWindup(EntityUid source)
    {
        _audio.PlayPvs(ExecuteSound, source);
    }

    public void PlayGrab(EntityUid source)
    {
        _audio.PlayPvs(GrabSound, source);
    }

    public void PlaySlice(EntityUid source)
    {
        _audio.PlayPvs(SliceSound, source);
    }

    public void PlayKillLaugh(EntityUid source)
    {
        _audio.PlayPvs(KillLaughSound, source);
    }

    public void PlayBlast(EntityUid source)
    {
        _audio.PlayPvs(BlastSound, source);
    }

    public void PlayBind(EntityUid source)
    {
        _audio.PlayPvs(BindSound, source);
    }

    public void PlayMark(EntityUid source)
    {
        _audio.PlayPvs(MarkSound, source);
    }

    public void PlayVanish(EntityUid source)
    {
        _audio.PlayPvs(VanishSound, source);
    }

    private static bool IsDirectional(FSRevenantEffect effect)
        => effect is FSRevenantEffect.Grab or FSRevenantEffect.Scythe;

    private static string PrototypeFor(FSRevenantEffect effect) => effect switch
    {
        FSRevenantEffect.Death => "FSRevenantDeathEffect",
        FSRevenantEffect.Kill => "FSRevenantKillEffect",
        FSRevenantEffect.Mark => "FSRevenantMarkEffect",
        FSRevenantEffect.Grab => "FSRevenantGrabEffect",
        FSRevenantEffect.Bind => "FSRevenantBindEffect",
        FSRevenantEffect.SliceVertical => "FSRevenantSliceVEffect",
        FSRevenantEffect.SliceDiagonal => "FSRevenantSliceDEffect",
        FSRevenantEffect.Hit => "FSRevenantHitEffect",
        FSRevenantEffect.Scythe => "FSRevenantScytheEffect",
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, null),
    };
}
