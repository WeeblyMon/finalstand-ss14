using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.Melee;

// A few frames of stark black and white on the hits that deserve it.
public sealed class FSImpactFrameOverlay : Overlay
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly ProtoId<ShaderPrototype> Shader = "FSImpactFrame";
    private const float Duration = 0.05f;

    // Slow clients would otherwise skip the flash entirely.
    private const int MinFrames = 2;

    private readonly ShaderInstance _shader;
    private float _until;
    private int _frames;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public FSImpactFrameOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypes.Index(Shader).InstanceUnique();
    }

    public void Flash(float now)
    {
        _until = now + Duration;
        _frames = MinFrames;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _frames > 0 || (float) _timing.RealTime.TotalSeconds < _until;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _frames--;
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
