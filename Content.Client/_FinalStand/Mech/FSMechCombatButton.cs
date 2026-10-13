// The mech HUD's combat toggle: 1/2/3 pick weapons while piloting, so combat mode needs its own button.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.UI;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Input;

namespace Content.Client._FinalStand.Mech;

public sealed class FSMechCombatButton : Control
{
    private static readonly Color Back = Color.FromHex("#080a0c");
    private static readonly Color Power = Color.FromHex("#52d6ef");

    public bool InCombat;
    public event Action? OnToggle;

    private Font? _font;
    private bool _hovered;
    private float _fontScale;

    public FSMechCombatButton()
    {
        MouseFilter = MouseFilterMode.Stop;
    }

    protected override void KeyBindDown(GUIBoundKeyEventArgs args)
    {
        base.KeyBindDown(args);
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        OnToggle?.Invoke();
        args.Handle();
    }

    protected override void MouseEntered()
    {
        base.MouseEntered();
        _hovered = true;
    }

    protected override void MouseExited()
    {
        base.MouseExited();
        _hovered = false;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        var s = UIScale;
        if (_font == null || Math.Abs(_fontScale - s) > 0.01f)
        {
            _fontScale = s;
            _font = new VectorFont(IoCManager.Resolve<IResourceCache>().GetResource<FontResource>("/Fonts/NotoSans/NotoSans-Bold.ttf"),
                (int) MathF.Round(7 * s));
        }

        var size = PixelSize;
        var slant = MathF.Round(7f * s);
        var accent = InCombat ? FSPalette.Danger : Color.White.WithAlpha(_hovered ? 0.5f : 0.25f);
        FSShapes.Slanted(handle, Vector2.Zero, size.X, size.Y, slant, Back.WithAlpha(InCombat ? 0.75f : 0.5f));
        if (InCombat)
            FSShapes.Slanted(handle, Vector2.Zero, size.X, size.Y, slant, FSPalette.Danger.WithAlpha(0.25f));
        FSShapes.Slanted(handle, Vector2.Zero, size.X, size.Y, slant, accent, border: MathF.Max(1f, 1.5f * s));

        var label = Loc.GetString(InCombat ? "fs-mech-hud-combat-on" : "fs-mech-hud-combat-off");
        var text = handle.GetDimensions(_font, label, 1f);
        FSShapes.Diamond(handle, new Vector2(slant + 10f * s, size.Y / 2f), 8f * s, InCombat ? FSPalette.Danger : Power.WithAlpha(0.6f));
        handle.DrawString(_font, new Vector2((size.X - text.X) / 2f + 6f * s, (size.Y - text.Y) / 2f), label,
            InCombat ? FSPalette.TextBright : FSPalette.TextMuted);
    }
}
