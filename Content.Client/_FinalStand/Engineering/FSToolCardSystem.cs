// A card above the hands for the engineer's tools, in the mech HUD's style: the RCD's piece, how many you can build
// and its cap (Ring layout only; the Belt already shows this), the rivet gun's steel and repair cap, and the hammer's
// grade materials plus the grade and pour progress of the barricade under the cursor.
using System.Numerics;
using Content.Client._FinalStand.Interface;
using Content.Client._FinalStand.RCD;
using Content.Client._FinalStand.UI;
using Content.Client.Gameplay;
using Content.Client.Hands.Systems;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.Materials;
using Content.Shared._FinalStand.RCD;
using Content.Shared._FinalStand.Repair;
using Content.Shared.CCVar;
using Content.Shared.RCD.Components;
using Content.Shared.Stacks;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Engineering;

public sealed partial class FSToolCardSystem : EntitySystem
{
    [Dependency] private IOverlayManager _overlays = default!;
    [Dependency] private IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new FSToolCardOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<FSToolCardOverlay>();
    }

    // Whether the card is showing this tool, so the weapon panel can leave out the same numbers.
    public bool Covers(EntityUid held)
    {
        if (!_cfg.GetCVar(CCVars.FSEngToolCard))
            return false;

        if (HasComp<FSEngineerOnlyRCDComponent>(held))
            return !_cfg.GetCVar(CCVars.FSRcdBeltLayout);

        return HasComp<FSRivetGunComponent>(held) || HasComp<FSEngineerHammerComponent>(held);
    }
}

public sealed partial class FSToolCardOverlay : Overlay
{
    [Dependency] private IEntityManager _entMan = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private IUserInterfaceManager _ui = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IStateManager _state = default!;

    private static readonly ResPath LabelPath = new("/Fonts/NotoSans/NotoSans-Bold.ttf");
    private static readonly ResPath NumberPath = new("/Fonts/NotoSansDisplay/NotoSansDisplay-BoldItalic.ttf");
    private static readonly Color Back = FSPalette.PanelDeep.WithAlpha(0.9f);
    private static readonly Color Shadow = Color.Black.WithAlpha(0.35f);
    private static readonly ProtoId<StackPrototype>[] HammerMaterials = { "Steel", "Plasteel", "Uranium" };

    private const float Width = 236f;
    private const float Height = 46f;
    private const float Slant = 10f;
    private const float AboveBottom = 118f;
    private const float HoverRange = 3f;

    private FSToolCardSystem? _cards;
    private HandsSystem? _hands;
    private SpriteSystem? _sprite;
    private FSCarriedMaterialSystem? _materials;
    private SharedTransformSystem? _xform;
    private FSRCDLook? _look;
    private Font? _label;
    private Font? _number;
    private Font? _small;
    private float _fontScale;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public FSToolCardOverlay()
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _cards ??= _entMan.System<FSToolCardSystem>();
        _hands ??= _entMan.System<HandsSystem>();
        if (_player.LocalEntity is not { } player
            || !_hands.TryGetActiveItem(player, out var active)
            || !_cards.Covers(active.Value))
            return;

        var held = active.Value;
        _sprite ??= _entMan.System<SpriteSystem>();
        _materials ??= _entMan.System<FSCarriedMaterialSystem>();
        _xform ??= _entMan.System<SharedTransformSystem>();
        _look ??= new FSRCDLook(_cache, _proto, _sprite);
        var s = _ui.DefaultUIScale;
        LoadFonts(s);

        var handle = args.ScreenHandle;
        var bounds = args.ViewportBounds;
        var width = MathF.Round(Width * s);
        var height = MathF.Round(Height * s);
        var slant = MathF.Round(Slant * s);
        var topLeft = new Vector2(MathF.Round(bounds.Center.X - width / 2f), MathF.Round(bounds.Bottom - AboveBottom * s - height));

        FSShapes.Slanted(handle, topLeft + new Vector2(2f, 3f) * s, width, height, slant, Shadow);
        FSShapes.Slanted(handle, topLeft, width, height, slant, Back);
        FSShapes.Slanted(handle, topLeft, width, height, slant, FSPalette.ChipEdge, border: MathF.Max(1f, s));
        FSShapes.Slanted(handle, topLeft + new Vector2(2f * s, 0f), slant + 3f * s, height, slant, FSPalette.Money);

        var inner = new Vector2(topLeft.X + slant + 10f * s, topLeft.Y + 6f * s);
        var right = topLeft.X + width - slant / 2f - 10f * s;

        if (_entMan.TryGetComponent<FSEngineerOnlyRCDComponent>(held, out var fs) && _entMan.TryGetComponent<RCDComponent>(held, out var rcd))
            DrawRcd(handle, player, fs, rcd, inner, right, s);
        else if (_entMan.TryGetComponent<FSRivetGunComponent>(held, out var rivet))
            DrawRivet(handle, player, rivet, inner, right, s);
        else if (_entMan.TryGetComponent<FSEngineerHammerComponent>(held, out var hammer))
            DrawHammer(handle, player, hammer, inner, right, s);
    }

    private void DrawRcd(DrawingHandleScreen handle, EntityUid player, FSEngineerOnlyRCDComponent fs, RCDComponent rcd, Vector2 at,
        float right, float s)
    {
        var piece = _proto.Index(rcd.ProtoId);
        var carried = _materials!.CountAll(player);
        Caption(handle, at, Loc.GetString("fs-tool-card-rcd"));
        handle.DrawString(_number!, at + new Vector2(0f, 9f * s), Fit(handle, _number!, FSRCDText.Name(_proto, piece), right - at.X - 60f * s),
            FSPalette.TextBright);

        var count = FSRCDPalette.Affordable(_proto, carried, fs, piece);
        if (count >= 0)
        {
            var text = count > 0 ? $"×{count}" : Loc.GetString("fs-tool-card-short");
            var size = handle.GetDimensions(_number!, text, 1f);
            handle.DrawString(_number!, new Vector2(right - size.X, at.Y + 9f * s), text, count > 0 ? FSPalette.Money : FSPalette.DangerSoft);
        }

        if (_proto.TryIndex<FSBuildRecipePrototype>(piece.ID, out var recipe)
            && recipe.CapGroup is { } group
            && fs.CapsMax.TryGetValue(group, out var max))
        {
            var used = fs.CapsUsed.GetValueOrDefault(group);
            var capText = $"{used}/{max}";
            var capSize = handle.GetDimensions(_small!, capText, 1f);
            var railY = at.Y + 31f * s;
            FSWorldChip.Rail(handle, new Vector2(at.X, railY), right - at.X - capSize.X - 6f * s, MathF.Max(2f, MathF.Round(3f * s)),
                max > 0 ? Math.Clamp((float) used / max, 0f, 1f) : 1f, used >= max ? FSPalette.Danger : FSPalette.Money);
            handle.DrawString(_small!, new Vector2(right - capSize.X, railY - capSize.Y / 2f + 1f * s), capText, FSPalette.TextMuted);
        }
    }

    private void DrawRivet(DrawingHandleScreen handle, EntityUid player, FSRivetGunComponent gun, Vector2 at, float right, float s)
    {
        var steel = _materials!.Count(player, gun.Material);
        Caption(handle, at, Loc.GetString("fs-tool-card-rivet"));
        var value = Loc.GetString("fs-tool-card-rivet-steel", ("count", steel));
        handle.DrawString(_number!, at + new Vector2(0f, 9f * s), value, steel > 0 ? FSPalette.TextBright : FSPalette.DangerSoft);

        var rivets = Loc.GetString("fs-tool-card-rivet-shots", ("count", steel * gun.RivetsPerSheet));
        var rivetSize = handle.GetDimensions(_small!, rivets, 1f);
        handle.DrawString(_small!, new Vector2(right - rivetSize.X, at.Y + 14f * s), rivets, FSPalette.TextMuted);

        var cap = Loc.GetString("fs-tool-card-rivet-cap", ("percent", (int) MathF.Round(gun.RepairCap * 100f)));
        var capSize = handle.GetDimensions(_small!, cap, 1f);
        var railY = at.Y + 31f * s;
        FSWorldChip.Rail(handle, new Vector2(at.X, railY), right - at.X - capSize.X - 6f * s, MathF.Max(2f, MathF.Round(3f * s)),
            gun.RepairCap, FSPalette.Ok);
        handle.DrawString(_small!, new Vector2(right - capSize.X, railY - capSize.Y / 2f + 1f * s), cap, FSPalette.Danger);
    }

    private void DrawHammer(DrawingHandleScreen handle, EntityUid player, FSEngineerHammerComponent hammer, Vector2 at, float right,
        float s)
    {
        Caption(handle, at, Loc.GetString("fs-tool-card-hammer"));

        var chipX = right;
        var carried = _materials!.CountAll(player);
        for (var i = HammerMaterials.Length - 1; i >= 0; i--)
        {
            var material = HammerMaterials[i];
            var have = carried.GetValueOrDefault(material) + hammer.SiloStock.GetValueOrDefault(material);
            var text = have.ToString();
            var size = handle.GetDimensions(_small!, text, 1f);
            chipX -= size.X;
            handle.DrawString(_small!, new Vector2(chipX, at.Y), text, have > 0 ? FSPalette.TextBright : FSPalette.TextDim);
            var icon = MathF.Round(12f * s);
            chipX -= icon + 2f * s;
            if (_look!.MaterialIcon(material) is { } texture)
                handle.DrawTextureRect(texture, UIBox2.FromDimensions(new Vector2(chipX, at.Y - 1f * s), new Vector2(icon, icon)));
            chipX -= 8f * s;
        }

        if (Hovered(player) is not { } target || !_entMan.TryGetComponent<FSUpgradeableComponent>(target, out var upgradeable))
        {
            handle.DrawString(_small!, at + new Vector2(0f, 16f * s), Loc.GetString("fs-tool-card-hammer-idle"), FSPalette.TextMuted);
            return;
        }

        var grade = _proto.Index(upgradeable.Grade);
        var gradeName = Loc.GetString(grade.Name).ToUpperInvariant();
        handle.DrawString(_number!, at + new Vector2(0f, 9f * s), gradeName, grade.HudColor ?? FSPalette.TextBright);

        if (upgradeable.Pending is not { } pendingId || !_proto.TryIndex(pendingId, out var pending))
            return;

        var railY = at.Y + 31f * s;
        var label = Loc.GetString("fs-tool-card-hammer-pour", ("name", Loc.GetString(pending.Name)), ("hits", upgradeable.Poured),
            ("total", pending.Hits));
        var labelSize = handle.GetDimensions(_small!, label, 1f);
        FSWorldChip.Rail(handle, new Vector2(at.X, railY), right - at.X - labelSize.X - 6f * s, MathF.Max(2f, MathF.Round(3f * s)),
            pending.Hits > 0 ? (float) upgradeable.Poured / pending.Hits : 0f, pending.HudColor ?? FSPalette.Money);
        handle.DrawString(_small!, new Vector2(right - labelSize.X, railY - labelSize.Y / 2f + 1f * s), label, FSPalette.TextMuted);
    }

    private EntityUid? Hovered(EntityUid player)
    {
        if (_state.CurrentState is not GameplayStateBase screen || _ui.CurrentlyHovered is not Robust.Client.UserInterface.CustomControls.IViewportControl)
            return null;

        var coords = _eye.PixelToMap(_input.MouseScreenPosition);
        if (screen.GetClickedEntity(coords) is not { } target)
            return null;

        var me = _xform!.GetMapCoordinates(player);
        return me.MapId == coords.MapId && (coords.Position - me.Position).Length() <= HoverRange ? target : null;
    }

    private void Caption(DrawingHandleScreen handle, Vector2 at, string text)
    {
        handle.DrawString(_label!, at, text, FSPalette.TextMuted);
    }

    private static string Fit(DrawingHandleScreen handle, Font font, string text, float max)
    {
        if (handle.GetDimensions(font, text, 1f).X <= max)
            return text;

        for (var len = text.Length - 1; len > 0; len--)
        {
            var cut = text.Substring(0, len) + "…";
            if (handle.GetDimensions(font, cut, 1f).X <= max)
                return cut;
        }

        return text;
    }

    private void LoadFonts(float s)
    {
        if (_label != null && Math.Abs(_fontScale - s) < 0.01f)
            return;

        _fontScale = s;
        var label = _cache.GetResource<FontResource>(LabelPath);
        _label = new VectorFont(label, (int) MathF.Round(7 * s));
        _small = new VectorFont(label, (int) MathF.Round(7 * s));
        _number = new VectorFont(_cache.GetResource<FontResource>(NumberPath), (int) MathF.Round(12 * s));
    }
}
