// The barrel glows as the HMG heats up and stays lit while it's locked out overheated.
using Content.Shared._FinalStand.HMG;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.HMG;

public sealed class FSHMGVisualSystem : EntitySystem
{
    [Dependency] private FSHMGSystem _hmg = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IOverlayManager _overlays = default!;

    private const string Layer = "overheat";
    private const float ShowAbove = 0.25f;

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

            var heat = _hmg.IsOverheated(hmg) ? 1f : _hmg.CurrentHeat(hmg);
            var visible = heat > ShowAbove;
            _sprite.LayerSetVisible((uid, sprite), layer, visible);
            if (visible)
                _sprite.LayerSetColor((uid, sprite), layer, Color.White.WithAlpha((heat - ShowAbove) / (1f - ShowAbove)));
        }
    }
}
