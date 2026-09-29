using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FinalStand.MedicalOps;

// Screen-space ring distortion for the Über deploy. At most four run at once; extras are dropped.
public sealed class FSShockwaveOverlay : Overlay
{
    private readonly IGameTiming _timing;
    private readonly ShaderInstance _shader;

    private static readonly ProtoId<ShaderPrototype> ShockwaveShader = "FSShockwave";

    public const int MaxWaves = 4;
    public const float Duration = 0.55f;
    public const float MaxRadius = 5f;

    private readonly List<(MapCoordinates At, TimeSpan Start)> _waves = new();
    private readonly Vector2[] _centres = new Vector2[MaxWaves];
    private readonly float[] _radii = new float[MaxWaves];
    private readonly float[] _strengths = new float[MaxWaves];
    private int _count;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public FSShockwaveOverlay(IGameTiming timing, IPrototypeManager prototypes)
    {
        _timing = timing;
        _shader = prototypes.Index(ShockwaveShader).InstanceUnique();
    }

    public void Add(MapCoordinates at)
    {
        if (_waves.Count < MaxWaves)
            _waves.Add((at, _timing.RealTime));
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        var now = _timing.RealTime;
        _waves.RemoveAll(w => (now - w.Start).TotalSeconds > Duration);
        _count = 0;

        foreach (var (at, start) in _waves)
        {
            if (at.MapId != args.MapId)
                continue;

            var t = (float) (now - start).TotalSeconds / Duration;
            var eased = 1f - (1f - t) * (1f - t);
            var centre = args.Viewport.WorldToLocal(at.Position);
            var edge = args.Viewport.WorldToLocal(at.Position + new Vector2(MaxRadius * eased, 0f));

            _centres[_count] = new Vector2(centre.X, args.Viewport.Size.Y - centre.Y);
            _radii[_count] = MathF.Abs(edge.X - centre.X);
            _strengths[_count] = 1f - t;
            _count++;
        }

        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("count", _count);
        _shader.SetParameter("centres", _centres);
        _shader.SetParameter("radii", _radii);
        _shader.SetParameter("strengths", _strengths);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
