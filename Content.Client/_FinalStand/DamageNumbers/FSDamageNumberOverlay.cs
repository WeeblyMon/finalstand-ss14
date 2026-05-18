using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.DamageNumbers;

public sealed class FSDamageNumberOverlay : Overlay
{
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private Font? _fontNormal;
    private Font? _fontCrit;

    public readonly List<DamageNumber> Numbers = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    internal const float Lifetime = 1.6f;
    private const float RiseSpeed = 1.1f;   // world units per second

    // Normal hits: white + black outline; crit hits: red + deep-red outline
    private static readonly Color NormalFg      = new(1f, 1f,    1f,    1f);
    private static readonly Color NormalOutline  = new(0f, 0f,    0f,    0.9f);
    private static readonly Color CritFg         = new(1f, 0.15f, 0.15f, 1f);
    private static readonly Color CritOutline    = new(0.4f, 0f,  0f,    0.9f);

    public FSDamageNumberOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.ViewportControl == null || Numbers.Count == 0)
            return;

        _fontNormal ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(
                new ResPath("/Fonts/AnimeAce/animeace2_reg.ttf")), 11);

        _fontCrit ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(
                new ResPath("/Fonts/AnimeAce/animeace2_bld.ttf")), 14);

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();

        foreach (var num in Numbers)
        {
            if (num.MapId != args.MapId)
                continue;

            // Rise upward in world space (+ Y = up, flipped to – screen Y by the matrix)
            var worldPos = num.OriginWorldPos + new Vector2(0f, num.Age * RiseSpeed);
            var screenPos = Vector2.Transform(worldPos, matrix);

            // Fade starts at 50 % of lifetime
            var fadeStart = Lifetime * 0.5f;
            var alpha = num.Age < fadeStart
                ? 1f
                : 1f - (num.Age - fadeStart) / (Lifetime - fadeStart);
            alpha = Math.Clamp(alpha, 0f, 1f);

            var font = num.IsCrit ? _fontCrit : _fontNormal;
            var fg      = (num.IsCrit ? CritFg      : NormalFg).WithAlpha(alpha);
            var outline = (num.IsCrit ? CritOutline : NormalOutline).WithAlpha(alpha * 0.9f);

            var text = ((int)MathF.Round(num.Amount)).ToString();
            var dims = handle.GetDimensions(font, text, 1f);
            var origin = screenPos - dims / 2f;

            // 4-direction outline at 1.5 px
            const float o = 1.5f;
            handle.DrawString(font, origin + new Vector2(-o, -o), text, 1f, outline);
            handle.DrawString(font, origin + new Vector2( o, -o), text, 1f, outline);
            handle.DrawString(font, origin + new Vector2(-o,  o), text, 1f, outline);
            handle.DrawString(font, origin + new Vector2( o,  o), text, 1f, outline);
            handle.DrawString(font, origin, text, 1f, fg);
        }
    }

    public struct DamageNumber
    {
        public Vector2 OriginWorldPos;
        public MapId MapId;
        public float Amount;
        public bool IsCrit;
        public float Age;
    }
}
