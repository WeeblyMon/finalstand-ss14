// Draws the active-item module: one panel for what is in the active hand, replacing vanilla's pair.
using System.Numerics;
using Robust.Client.Graphics;

namespace Content.Client._FinalStand.WaveHud;

public sealed partial class WaveHudOverlay
{
    public string? WeaponName;
    public int? WeaponLoaded;
    public int? WeaponCapacity;
    public int WeaponReserve;

    private const float WeaponPanelW = 232f;
    private const float WeaponPanelPad = 8f;

    private static readonly Color WeaponBack = new(0.08f, 0.09f, 0.11f, 0.78f);
    private static readonly Color WeaponEdge = new(0.23f, 0.26f, 0.32f, 0.85f);
    private static readonly Color WeaponText = Color.FromHex("#D8E0E8");
    private static readonly Color WeaponMuted = Color.FromHex("#8FA1B3");
    private static readonly Color AmmoFull = Color.FromHex("#D8E0E8");
    private static readonly Color AmmoLow = Color.FromHex("#E85055");

    private void DrawWeaponModule(DrawingHandleScreen screen, float margin, float bandLift)
    {
        if (WeaponName is not { } name)
            return;

        var labelH = _cachedLabelH;
        var valueH = _cachedValueH;
        var hasAmmo = WeaponLoaded is not null && WeaponCapacity is > 0;

        var contentH = labelH + (hasAmmo ? 4f + valueH : 0f);
        var panelH = contentH + WeaponPanelPad * 2f;

        // Far bottom right. The wave panel moved to the top, so this corner is the module's own.
        var x = _clyde.ScreenSize.X - margin - WeaponPanelW;
        var bottom = _clyde.ScreenSize.Y - bandLift;
        var y = bottom - panelH;

        screen.DrawRect(new UIBox2(x, y, x + WeaponPanelW, y + panelH), WeaponBack);
        screen.DrawRect(new UIBox2(x, y, x + WeaponPanelW, y + panelH), WeaponEdge, filled: false);

        var textX = x + WeaponPanelPad;
        screen.DrawString(_labelFont!, new Vector2(textX, y + WeaponPanelPad), name.ToUpperInvariant(), WeaponText);

        if (!hasAmmo)
            return;

        var loaded = WeaponLoaded!.Value;
        var capacity = WeaponCapacity!.Value;
        var ammoColor = loaded == 0 || loaded <= capacity * 0.25f ? AmmoLow : AmmoFull;

        var ammo = $"{loaded}/{capacity}";
        var ammoY = y + WeaponPanelPad + labelH + 4f;
        screen.DrawString(_valueFont!, new Vector2(textX, ammoY), ammo, ammoColor);

        var reserve = $"x{WeaponReserve}";
        var reserveW = screen.GetDimensions(_labelFont!, reserve, 1f).X;
        screen.DrawString(_labelFont!,
            new Vector2(x + WeaponPanelW - WeaponPanelPad - reserveW, ammoY + valueH - labelH),
            reserve, WeaponMuted);
    }
}
