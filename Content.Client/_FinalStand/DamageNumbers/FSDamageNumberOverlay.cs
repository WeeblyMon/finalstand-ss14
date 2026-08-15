using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.DamageNumbers;

public sealed partial class FSDamageNumberOverlay : Overlay
{
    [Dependency] private IResourceCache _resourceCache = default!;

    private Font? _fontNormal;

    public readonly List<DamageNumber> Numbers = new();

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    internal const float Lifetime = 1.6f;
    private const float RiseSpeed = 1.1f;   // world units per second
    private const float CullMargin = 64f;
    private const int MaxNumbers = 80;

    // Oldest is overwritten at the cap; shifting the whole list on every spawn was the alternative.
    public void Add(in DamageNumber number)
    {
        if (Numbers.Count < MaxNumbers)
        {
            Numbers.Add(number);
            return;
        }

        var oldest = 0;
        for (var i = 1; i < Numbers.Count; i++)
        {
            if (Numbers[i].Age > Numbers[oldest].Age)
                oldest = i;
        }

        Numbers[oldest] = number;
    }

    public void Age(float frameTime)
    {
        for (var i = Numbers.Count - 1; i >= 0; i--)
        {
            var n = Numbers[i];
            n.Age += frameTime;

            var lifetime = n.Lifetime > 0f ? n.Lifetime : Lifetime;
            if (n.Age >= lifetime)
            {
                Numbers.RemoveAt(i);
                continue;
            }

            Numbers[i] = n;
        }
    }

    // Normal hits: white + black outline; crit hits: red + deep-red outline; armor: grey + dark outline; level up: gold
    private static readonly Color NormalFg       = new(1f,    1f,    1f,    1f);
    private static readonly Color NormalOutline   = new(0f,    0f,    0f,    0.9f);
    private static readonly Color CritFg          = new(1f,    0.15f, 0.15f, 1f);
    private static readonly Color CritOutline     = new(0.4f,  0f,    0f,    0.9f);
    private static readonly Color ArmorFg         = new(0.65f, 0.65f, 0.65f, 1f);
    private static readonly Color ArmorOutline    = new(0.15f, 0.15f, 0.15f, 0.9f);
    private static readonly Color LevelUpFg       = new(1f,    0.84f, 0f,    1f);
    private static readonly Color LevelUpOutline  = new(0.45f, 0.28f, 0f,    0.9f);
    private static readonly Color HealFg          = new(0.1f,  1f,    0.1f,  1f);
    private static readonly Color HealOutline     = new(0f,    0.35f, 0f,    0.9f);

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
        var bounds = args.ViewportBounds;

        for (var i = 0; i < Numbers.Count; i++)
        {
            var num = Numbers[i];
            if (num.MapId != args.MapId)
                continue;

            var worldPos = num.OriginWorldPos + new Vector2(0f, num.Age * RiseSpeed);
            var screenPos = Vector2.Transform(worldPos, matrix);

            if (screenPos.X < bounds.Left - CullMargin || screenPos.X > bounds.Right + CullMargin ||
                screenPos.Y < bounds.Top - CullMargin || screenPos.Y > bounds.Bottom + CullMargin)
                continue;

            var lifetime = num.Lifetime > 0f ? num.Lifetime : Lifetime;
            var fadeStart = lifetime * 0.5f;
            var alpha = num.Age < fadeStart
                ? 1f
                : 1f - (num.Age - fadeStart) / (lifetime - fadeStart);
            alpha = Math.Clamp(alpha, 0f, 1f);

            Color fg, outline;
            if (num.IsHeal)
            {
                fg      = HealFg.WithAlpha(alpha);
                outline = HealOutline.WithAlpha(alpha * 0.9f);
            }
            else if (num.IsLevelUp)
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
            var text = num.Text;

            if (num.Size == Vector2.Zero)
            {
                num.Size = handle.GetDimensions(font, text, 1f);
                Numbers[i] = num;
            }

            var origin = screenPos - num.Size / 2f;

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
        public bool IsHeal;
        public int LevelUpAp;
        public float Age;
        public float Lifetime;

        // Fixed for the number's whole life; built at spawn, measured on first draw.
        public string Text;
        public Vector2 Size;
    }
}
