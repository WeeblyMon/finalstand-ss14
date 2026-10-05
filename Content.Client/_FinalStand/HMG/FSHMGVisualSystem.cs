// The barrel glows orange, red, then white as the HMG heats up; it throws casings while firing and vents steam once it cools.
using Content.Client._FinalStand.Particles;
using Content.Shared._FinalStand.HMG;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.HMG;

public sealed class FSHMGVisualSystem : EntitySystem
{
    [Dependency] private FSHMGSystem _hmg = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private FSParticleSystem _particles = default!;

    private const string Layer = "overheat";
    private const float ShowAbove = 0.25f;
    private static readonly Color Warm = Color.FromHex("#ff9a3c");
    private static readonly Color Hot = Color.FromHex("#ff3b2a");
    private static readonly Color White = Color.FromHex("#fff4e0");

    private readonly Dictionary<EntityUid, (float Heat, bool Overheated)> _last = new();

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new FSHMGLinkOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSHMGLinkOverlay>();
    }

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<FSHMGComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var hmg, out var sprite))
        {
            if (!_sprite.LayerMapTryGet((uid, sprite), Layer, out var layer, false))
                continue;

            var overheated = _hmg.IsOverheated(hmg);
            var raw = _hmg.CurrentHeat(hmg);
            var heat = overheated ? 1f : raw;
            var visible = heat > ShowAbove;
            _sprite.LayerSetVisible((uid, sprite), layer, visible);
            if (visible)
                _sprite.LayerSetColor((uid, sprite), layer, Stage(heat).WithAlpha((heat - ShowAbove) / (1f - ShowAbove)));

            if (_last.TryGetValue(uid, out var last))
            {
                if (raw > last.Heat + 0.001f)
                    _particles.Burst("FSCasing", uid);
                if (last.Overheated && !overheated)
                    _particles.Burst("FSSteamVent", uid);
            }

            _last[uid] = (raw, overheated);
        }
    }

    private static Color Stage(float heat)
    {
        return heat switch
        {
            < 0.6f => Warm,
            < 0.9f => Color.InterpolateBetween(Warm, Hot, (heat - 0.6f) / 0.3f),
            _ => Color.InterpolateBetween(Hot, White, (heat - 0.9f) / 0.1f),
        };
    }
}
