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

    public readonly List<DamageNumber> Numbers = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    internal const float Lifetime = 1.6f;
    private const float RiseSpeed = 1.1f;   // world units per second

    // Normal hits: white + black outline; crit hits: red + deep-red outline; armor: grey + dark outline; level up: gold
    private static readonly Color NormalFg       = new(1f,    1f,    1f,    1f);
    private static readonly Color NormalOutline   = new(0f,    0f,    0f,    0.9f);
    private static readonly Color CritFg          = new(1f,    0.15f, 0.15f, 1f);
    private static readonly Color CritOutline     = new(0.4f,  0f,    0f,    0.9f);
    private static readonly Color ArmorFg         = new(0.65f, 0.65f, 0.65f, 1f);
    private static readonly Color ArmorOutline    = new(0.15f, 0.15f, 0.15f, 0.9f);
    private static readonly Color LevelUpFg       = new(1f,    0.84f, 0f,    1f);
    private static readonly Color LevelUpOutline  = new(0.45f, 0.28f, 0f,    0.9f);

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

        var handle = args.ScreenHandle;
        var matrix = args.ViewportControl.GetWorldToScreenMatrix();

        foreach (var num in Numbers)
        {
            if (num.MapId != args.MapId)
                continue;

            var worldPos = num.OriginWorldPos + new Vector2(0f, num.Age * RiseSpeed);
            var screenPos = Vector2.Transform(worldPos, matrix);

            var lifetime = num.Lifetime > 0f ? num.Lifetime : Lifetime;
            var fadeStart = lifetime * 0.5f;
            var alpha = num.Age < fadeStart
                ? 1f
                : 1f - (num.Age - fadeStart) / (lifetime - fadeStart);
            alpha = Math.Clamp(alpha, 0f, 1f);

            Color fg, outline;
            if (num.IsLevelUp)
            {
                fg      = LevelUpFg.WithAlpha(alpha);
                outline = LevelUpOutline.WithAlpha(alpha * 0.9f);
            }
            else if (num.IsArmor)
            {
                fg      = ArmorFg.WithAlpha(alpha);
                outline = ArmorOutline.WithAlpha(alpha * 0.9f);
            }
            else if (num.IsCrit)
            {
                fg      = CritFg.WithAlpha(alpha);
                outline = CritOutline.WithAlpha(alpha * 0.9f);
            }
            else
            {
                fg      = NormalFg.WithAlpha(alpha);
                outline = NormalOutline.WithAlpha(alpha * 0.9f);
            }

            var font = _fontNormal;
            var text = num.IsLevelUp
                ? $"LEVEL UP +{num.LevelUpAp}AP"
                : ((int)MathF.Round(num.Amount)).ToString();
            var dims = handle.GetDimensions(font, text, 1f);
            var origin = screenPos - dims / 2f;

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
        public bool IsArmor;
        public bool IsLevelUp;
        public int LevelUpAp;
        public float Age;
        public float Lifetime;
    }
}
