// Keeps the pings and tasks the server sent, drops expired ones and ones whose mob has died, plays a short sound from the ping and logs your role's pings in chat.
using Content.Client.UserInterface.Systems.Chat;
using Content.Shared._FinalStand.Departments;
using Content.Shared._FinalStand.Pings;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Mobs.Systems;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Pings;

public sealed record FSActivePing(FSPingEvent Data, TimeSpan Start, TimeSpan End);

public sealed partial class FSPingClientSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private MobStateSystem _mobState = default!;

    private const int PerSender = 3;
    private const float PingHearing = 18f;
    private static readonly TimeSpan BossFlash = TimeSpan.FromSeconds(0.8);
    private static readonly SoundSpecifier InfoSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg");
    private static readonly SoundSpecifier EnemySound = new SoundPathSpecifier("/Audio/Machines/twobeep.ogg");
    private static readonly SoundSpecifier RequestSound = new SoundPathSpecifier("/Audio/Machines/sonar-ping.ogg");
    private static readonly SoundSpecifier BossSound = new SoundPathSpecifier("/Audio/Effects/alert.ogg");

    public readonly List<FSActivePing> Pings = new();
    public List<FSTaskEntry> Tasks { get; private set; } = new();
    public TimeSpan BossFlashUntil { get; private set; }
    public TimeSpan MutedUntil { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSPingEvent>(OnPing);
        SubscribeNetworkEvent<FSTaskBoardEvent>(ev => Tasks = ev.Tasks);
        SubscribeNetworkEvent<FSPingRefusedEvent>(ev => MutedUntil = _timing.RealTime + TimeSpan.FromSeconds(ev.Seconds));
        _overlays.AddOverlay(new FSPingOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSPingOverlay>();
    }

    public bool ForMe(FSPingAudience audience)
    {
        return FSPingAudiences.Includes(audience, CompOrNull<FSDepartmentAccessComponent>(_player.LocalEntity));
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
        var sound = FSPingLook.CategoryOf(ev.Type) switch
        {
            FSPingCategory.Boss => BossSound,
            FSPingCategory.Enemy => EnemySound,
            FSPingCategory.Request => RequestSound,
            _ => InfoSound,
        };

        var volume = AudioParams.Default.WithVolume(forMe ? -2f : -9f);
        if (ev.Type == FSPingType.Boss)
            _audio.PlayGlobal(sound, Filter.Local(), false, volume);
        else
            _audio.PlayStatic(sound, Filter.Local(), GetCoordinates(ev.Coords), false, volume.WithMaxDistance(PingHearing));
        if (ev.Type == FSPingType.Boss)
            BossFlashUntil = now + BossFlash;

        if (forMe && _cfg.GetCVar(CCVars.FSPingChat))
            LogToChat(ev);
    }

    private void LogToChat(FSPingEvent ev)
    {
        var where = string.Empty;
        if (_player.LocalEntity is { } local)
        {
            var me = _transform.GetMapCoordinates(local);
            var at = _transform.ToMapCoordinates(GetCoordinates(ev.Coords));
            var offset = at.Position - me.Position;
            if (at.MapId == me.MapId && offset.Length() >= 1f)
                where = Loc.GetString("fs-ping-chat-where", ("distance", (int) offset.Length()),
                    ("dir", Loc.GetString($"fs-ping-dir-{offset.ToWorldAngle().GetDir().ToString().ToLowerInvariant()}")));
        }

        var text = Loc.GetString("fs-ping-chat", ("name", ev.SenderName), ("label", ev.Label), ("where", where));
        var wrapped = $"[color=#9ca3af]{FormattedMessage.EscapeText(text)}[/color]";
        _ui.GetUIController<ChatUIController>().ProcessChatMessage(
            new ChatMessage(ChatChannel.Notifications, text, wrapped, NetEntity.Invalid, null), speechBubble: false);
    }

    public override void FrameUpdate(float frameTime)
    {
        var now = _timing.RealTime;
        Pings.RemoveAll(p => now >= p.End || TargetGone(p.Data));
    }

    private bool TargetGone(FSPingEvent ping)
    {
        return ping.Target is { } net && TryGetEntity(net, out var target) && Exists(target) && _mobState.IsDead(target.Value);
    }
}
