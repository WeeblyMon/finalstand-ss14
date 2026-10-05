// Keeps the pings and tasks the server sent, drops expired ones, and plays a short sound per category.
using Content.Shared._FinalStand.Departments;
using Content.Shared._FinalStand.Pings;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Pings;

public sealed record FSActivePing(FSPingEvent Data, TimeSpan Start, TimeSpan End);

public sealed partial class FSPingClientSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private const int PerSender = 3;
    private static readonly TimeSpan BossFlash = TimeSpan.FromSeconds(0.8);
    private static readonly SoundSpecifier InfoSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");
    private static readonly SoundSpecifier EnemySound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    private static readonly SoundSpecifier RequestSound = new SoundPathSpecifier("/Audio/Machines/sonar-ping.ogg");
    private static readonly SoundSpecifier BossSound = new SoundPathSpecifier("/Audio/Effects/alert.ogg");

    public readonly List<FSActivePing> Pings = new();
    public List<FSTaskEntry> Tasks { get; private set; } = new();
    public TimeSpan BossFlashUntil { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSPingEvent>(OnPing);
        SubscribeNetworkEvent<FSTaskBoardEvent>(ev => Tasks = ev.Tasks);
        _overlays.AddOverlay(new FSPingOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSPingOverlay>();
    }

    public bool ForMe(FSPingAudience audience)
    {
        if (audience == FSPingAudience.Everyone)
            return true;

        if (!TryComp<FSDepartmentAccessComponent>(_player.LocalEntity, out var dept))
            return false;

        return audience switch
        {
            FSPingAudience.Engineering => dept.Engineering,
            FSPingAudience.Medical => dept.Medical,
            FSPingAudience.Science => dept.Science,
            _ => false,
        };
    }

    private void OnPing(FSPingEvent ev)
    {
        Pings.RemoveAll(p => p.Data.Sender == ev.Sender
            && (ev.Target != null ? p.Data.Target == ev.Target : p.Data.Type == ev.Type && p.Data.Target == null));

        var mine = 0;
        for (var i = Pings.Count - 1; i >= 0; i--)
        {
            if (Pings[i].Data.Sender != ev.Sender || ++mine < PerSender)
                continue;

            Pings.RemoveAt(i);
        }

        var now = _timing.RealTime;
        Pings.Add(new FSActivePing(ev, now, now + TimeSpan.FromSeconds(ev.Lifetime)));

        var forMe = ForMe(ev.Audience);
        var sound = ev.Type switch
        {
            FSPingType.Boss => BossSound,
            FSPingType.Enemy or FSPingType.Special or FSPingType.Danger => EnemySound,
            _ when FSPingLook.IsRequest(ev.Type) => RequestSound,
            _ => InfoSound,
        };

        _audio.PlayGlobal(sound, Filter.Local(), false, AudioParams.Default.WithVolume(forMe ? -2f : -9f));
        if (ev.Type == FSPingType.Boss)
            BossFlashUntil = now + BossFlash;
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.RealTime;
        Pings.RemoveAll(p => now >= p.End);
    }
}
