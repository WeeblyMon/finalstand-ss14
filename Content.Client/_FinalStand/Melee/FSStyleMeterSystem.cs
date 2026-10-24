using Content.Shared._FinalStand.Melee;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Melee;

// Tracks the local player's style rank for the meter and plays its stingers.
public sealed partial class FSStyleMeterSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier RankUpSound = new SoundCollectionSpecifier("FSMeleeSwishHeavy");
    private static readonly SoundSpecifier CrowdSound = new SoundPathSpecifier("/Audio/_FinalStand/Melee/crowd_cheer.ogg");
    private const int CrowdRank = 5;
    private const float CrowdCooldown = 8f;

    private FSStyleMeterOverlay? _overlay;
    private float _lastCrowd = float.MinValue;

    public int Rank { get; private set; }
    public float Score { get; private set; }
    public int Hits { get; private set; }

    // Real time of the last rank change, and whether it went up, for the meter's pop or shake.
    public float ChangedAt { get; private set; } = float.MinValue;
    public bool Rose { get; private set; }
    public float HitsChangedAt { get; private set; } = float.MinValue;

    public float RealTime => (float) _timing.RealTime.TotalSeconds;

    public override void Initialize()
    {
        base.Initialize();
        _overlay = new FSStyleMeterOverlay(this);
        _overlays.AddOverlay(_overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        if (_overlay != null)
            _overlays.RemoveOverlay(_overlay);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } local || !TryComp<FSStyleComponent>(local, out var style))
        {
            Rank = 0;
            Score = 0f;
            Hits = 0;
            return;
        }

        var now = _timing.CurTime;
        Score = style.ScoreAt(now);
        var hits = style.HitsAt(now);
        if (hits != Hits)
            HitsChangedAt = RealTime;
        Hits = hits;

        var rank = FSStyleComponent.RankOf(Score);
        if (rank == Rank)
            return;

        Rose = rank > Rank;
        Rank = rank;
        ChangedAt = RealTime;
        if (!Rose || !_cfg.GetCVar(CCVars.FSStyleMeter))
            return;

        _audio.PlayGlobal(RankUpSound, Filter.Local(), false, AudioParams.Default.WithVolume(-4f).WithPitchScale(0.9f + 0.08f * rank));
        if (rank >= CrowdRank && RealTime - _lastCrowd > CrowdCooldown)
        {
            _lastCrowd = RealTime;
            _audio.PlayGlobal(CrowdSound, Filter.Local(), false, AudioParams.Default.WithVolume(-8f + 2f * (rank - CrowdRank)));
        }
    }
}
