using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Melee;

public sealed class FSObjectiveVignetteOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    private readonly IEntityManager _entities;
    private readonly IPlayerManager _player;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public float Level;
    public float Strength;
    public Color Color;

    public FSObjectiveVignetteOverlay(IEntityManager entities, IPlayerManager player, IPrototypeManager prototypes)
    {
        _entities = entities;
        _player = player;
        _shader = prototypes.Index(CircleMaskShader).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return Level > 0.001f
               && _entities.TryGetComponent(_player.LocalEntity, out EyeComponent? eye)
               && args.Viewport.Eye == eye.Eye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var distance = args.ViewportBounds.Width;
        _shader.SetParameter("time", 0f);
        _shader.SetParameter("color", new Vector3(Color.R, Color.G, Color.B));
        _shader.SetParameter("darknessAlphaOuter", Strength * Level);
        _shader.SetParameter("outerCircleRadius", 0.9f * distance);
        _shader.SetParameter("outerCircleMaxRadius", 0.9f * distance);
        _shader.SetParameter("innerCircleRadius", 0.45f * distance);
        _shader.SetParameter("innerCircleMaxRadius", 0.45f * distance);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
