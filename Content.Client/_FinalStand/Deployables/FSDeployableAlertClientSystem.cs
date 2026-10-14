// Your own sentries: a low one pulses its chip and chirps once; an empty or destroyed one posts a short toast with
// how far away it was and which way.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Content.Shared._FinalStand.Deployables;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Deployables;

public sealed partial class FSDeployableAlertClientSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    private static readonly SoundSpecifier LowSound = new SoundPathSpecifier("/Audio/_FinalStand/Engineering/sentry_low.ogg");
    private static readonly SoundSpecifier LostSound = new SoundPathSpecifier("/Audio/_FinalStand/Engineering/sentry_lost.ogg");

    private static readonly string[] Compass = { "E", "NE", "N", "NW", "W", "SW", "S", "SE" };

    public readonly HashSet<EntityUid> Low = new();
    public readonly List<(string Text, Color Color, TimeSpan At)> Toasts = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSDeployableAlertEvent>(OnAlert);
        _overlays.AddOverlay(new FSDeployableToastOverlay(this));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSDeployableToastOverlay>();
    }

    public float Pulse(EntityUid uid)
    {
        if (!Low.Contains(uid))
            return 0f;

        return 0.5f + 0.5f * MathF.Sin((float) _timing.RealTime.TotalSeconds * 6f);
    }

    public override void FrameUpdate(float frameTime)
    {
        Low.RemoveWhere(uid => !Exists(uid));
        Toasts.RemoveAll(t => _timing.RealTime - t.At > TimeSpan.FromSeconds(FSDeployableToastOverlay.Life));
    }

    private void OnAlert(FSDeployableAlertEvent ev)
    {
        if (!_cfg.GetCVar(CCVars.FSEngOwnerAlerts))
            return;

        if (ev.Alert == FSDeployableAlert.LowAmmo)
        {
            if (TryGetEntity(ev.Target, out var target))
                Low.Add(target.Value);
            _audio.PlayGlobal(LowSound, Filter.Local(), false);
            return;
        }

        var key = ev.Alert == FSDeployableAlert.Lost ? "fs-sentry-alert-lost" : "fs-sentry-alert-spent";
        Toasts.Add((Loc.GetString(key, ("name", ev.Name.ToUpperInvariant()), ("where", Where(ev))),
            ev.Alert == FSDeployableAlert.Lost ? FSPalette.Danger : FSPalette.Warn, _timing.RealTime));
        if (Toasts.Count > 3)
            Toasts.RemoveAt(0);
        _audio.PlayGlobal(LostSound, Filter.Local(), false);
    }

    private string Where(FSDeployableAlertEvent ev)
    {
        if (_player.LocalEntity is not { } local)
            return string.Empty;

        var at = _xform.ToMapCoordinates(GetCoordinates(ev.At));
        var me = _xform.GetMapCoordinates(local);
        if (at.MapId != me.MapId)
            return string.Empty;

        var offset = at.Position - me.Position;
        var octant = (int) MathF.Round(MathF.Atan2(offset.Y, offset.X) / (MathF.PI / 4f));
        return $"{offset.Length():0} m {Compass[(octant + 8) % 8]}";
    }
}

public sealed partial class FSDeployableToastOverlay : Overlay
{
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;

    public const double Life = 3.5;

    private readonly FSDeployableAlertClientSystem _alerts;
    private Font? _font;
    private float _fontScale;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSDeployableToastOverlay(FSDeployableAlertClientSystem alerts)
    {
        IoCManager.InjectDependencies(this);
        _alerts = alerts;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_alerts.Toasts.Count == 0)
            return;

        var s = _ui.DefaultUIScale;
        if (_font == null || Math.Abs(_fontScale - s) > 0.01f)
        {
            _fontScale = s;
            _font = new VectorFont(_cache.GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"), (int) MathF.Round(8 * s));
        }

        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var y = bounds.Top + bounds.Height * 0.27f;
        foreach (var (text, color, at) in _alerts.Toasts)
        {
            var age = (_timing.RealTime - at).TotalSeconds;
            var alpha = (float) Math.Clamp(Math.Min(age / 0.15, (Life - age) / 0.4), 0d, 1d);
            var size = handle.GetDimensions(_font, text, 1f);
            var width = size.X + 30f * s;
            var height = size.Y + 8f * s;
            var topLeft = new Vector2(MathF.Round(bounds.Center.X - width / 2f), MathF.Round(y));
            FSShapes.Slanted(handle, topLeft, width, height, MathF.Round(6f * s), FSPalette.PanelDeep.WithAlpha(0.9f * alpha));
            FSShapes.Slanted(handle, topLeft, width, height, MathF.Round(6f * s), color.WithAlpha(alpha), border: MathF.Max(1f, 1.5f * s));
            handle.DrawString(_font, topLeft + new Vector2(15f * s, 4f * s), text, color.WithAlpha(alpha));
            y += height + 4f * s;
        }
    }
}
