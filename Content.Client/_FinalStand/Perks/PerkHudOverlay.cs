using System.Numerics;
using Content.Shared._FinalStand.Perks;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Enums;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Perks;

public sealed class PerkHudOverlay : Overlay
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    public List<PerkType> ActivePerks = [];

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    private Font? _font;

    public PerkHudOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ActivePerks.Count == 0)
            return;

        // Font size matches the enemy counter in WaveHudOverlay (small, readable)
        _font ??= new VectorFont(
            _resourceCache.GetResource<FontResource>(new ResPath("/Fonts/NotoSans/NotoSans-Bold.ttf")), 9);

        var screen = args.ScreenHandle;
        var screenSize = _clyde.ScreenSize;

        // Mirror WaveHudOverlay constants so perks sit just above the enemy counter.
        // Wave digits: bottom margin 24, height 80 → top at screenSize.Y - 104
        // Enemy counter (~22px) + 6px gap → enemy counter top at ≈ screenSize.Y - 132
        // Perks sit directly above that with a 4px gap.
        const float margin = 24f;
        const float digitHeight = 80f;
        const float enemyCounterHeight = 22f;
        const float enemyCounterGap = 6f;
        const float lineHeight = 13f;

        var blockBottom = screenSize.Y - margin - digitHeight - enemyCounterHeight - enemyCounterGap - 4f;
        var blockTop    = blockBottom - lineHeight * ActivePerks.Count;
        var y = blockTop;

        foreach (var perk in ActivePerks)
        {
            var (label, color) = PerkDisplay(perk);
            var textW = screen.GetDimensions(_font, label, 1f).X;
            var x = screenSize.X - margin - textW;

            var bgRect = new UIBox2(x - 4f, y - 1f, x + textW + 4f, y + lineHeight - 1f);
            screen.DrawRect(bgRect, new Color(0f, 0f, 0f, 0.6f));
            screen.DrawString(_font, new Vector2(x, y), label, color);
            y += lineHeight;
        }
    }

    private static (string label, Color color) PerkDisplay(PerkType perk) => perk switch
    {
        PerkType.Juggernog => ("■ JUGGERNOG",  Color.FromHex("#FF8A65")),
        PerkType.SpeedCola => ("■ SPEED COLA", Color.FromHex("#4FC3F7")),
        PerkType.DoubleTap => ("■ DOUBLE TAP", Color.FromHex("#FF5252")),
        PerkType.StaminUp  => ("■ STAMIN-UP",  Color.FromHex("#69F0AE")),
        _                  => ("■ PERK",        Color.White),
    };
}
