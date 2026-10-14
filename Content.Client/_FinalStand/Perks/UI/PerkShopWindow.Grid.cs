using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.Stylesheets;
using Content.Shared._FinalStand.Perks;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ISImage = SixLabors.ImageSharp.Image;

namespace Content.Client._FinalStand.Perks.UI;

public sealed partial class PerkShopWindow
{
    private const float TileWidth = 104f;
    private const float TileHeight = 118f;
    private const int SlotSize = 52;

    private readonly List<GridContainer> _grids = new();

    private Texture? GetPerkIcon(string perkId)
    {
        if (!FSPerkDef.All.TryGetValue(perkId, out var def))
            return null;

        var file = def.IconFile ?? def.Id.ToLowerInvariant();
        var path = $"/Textures/_FinalStand/Interface/Perks/Icons/{file}.png";

        if (_iconCache.TryGetValue(path, out var cached))
            return cached;

        if (_res.TryContentFileRead(path, out var stream))
        {
            using (stream)
            using (var img = ISImage.Load<Rgba32>(stream))
            using (var resized = img.Clone(x => x.Resize(CellSize, CellSize)))
            {
                var tex = _clyde.LoadTextureFromImage(resized, path);
                _iconCache[path] = tex;
                return tex;
            }
        }

        Logger.Warning($"[PerkShop] No icon found for perk '{perkId}' (looked for {file}.png).");
        return null;
    }

    private void RebuildSlots()
    {
        SlotsRow.RemoveAllChildren();
        if (_state == null)
            return;

        for (var i = 0; i < FSPerkDef.SlotCount; i++)
        {
            var idx = i;
            var slotId = _state.Slots[i];
            FSPerkDef.All.TryGetValue(slotId ?? string.Empty, out var def);
            var selected = def != null && def.Id == _selectedId;

            var box = def == null
                ? Box(FSUiPalette.BgRecess, FSUiPalette.BorderNeutral, new Thickness(1))
                : Box(FSPerkPalette.Background[def.Category],
                    selected ? FSUiPalette.AccentBrand : FSPerkPalette.Edge[def.Category],
                    new Thickness(selected ? 2 : 1));

            var btn = new ContainerButton
            {
                SetSize = new Vector2(SlotSize, SlotSize),
                StyleBoxOverride = box,
                ToolTip = def == null ? $"Slot {i + 1}: empty" : $"Slot {i + 1}: {def.Name} (level {Level(def.Id)})",
            };

            var layers = new LayoutContainer();
            if (def != null && GetPerkIcon(def.Id) is { } icon)
            {
                var tex = new TextureRect { Texture = icon, Stretch = TextureRect.StretchMode.Scale, SetSize = new Vector2(36, 36) };
                LayoutContainer.SetPosition(tex, new Vector2((SlotSize - 36) / 2f, (SlotSize - 36) / 2f));
                layers.AddChild(tex);
            }

            var number = new Label
            {
                Text = (i + 1).ToString(),
                FontOverride = _fontSmallBold,
                FontColorOverride = def == null ? FSUiPalette.BorderSubtle : FSUiPalette.TextPrimary,
            };
            LayoutContainer.SetPosition(number, def == null ? new Vector2(SlotSize / 2f - 4, SlotSize / 2f - 8) : new Vector2(4, 1));
            layers.AddChild(number);
            btn.AddChild(layers);

            btn.OnPressed += _ =>
            {
                if (def != null)
                {
                    Select(def.Id);
                    return;
                }

                // An empty slot equips the selected perk into that exact slot.
                if (_selectedId != null && _state != null && Level(_selectedId) > 0 && !_state.Slots.Contains(_selectedId))
                    OnEquipPerk?.Invoke(new FSEquipPerkMessage { PerkId = _selectedId, SlotIndex = idx });
            };

            SlotsRow.AddChild(btn);
        }
    }

    private void RebuildGrid()
    {
        PerkGrid.RemoveAllChildren();
        _grids.Clear();
        if (_state == null)
            return;

        var search = _search.Trim().ToLowerInvariant();
        foreach (var cat in CatOrder)
        {
            if (_filterCategory.HasValue && cat != _filterCategory.Value)
                continue;

            var inCat = FSPerkDef.All.Values.Where(d => d.Category == cat).ToList();
            var shown = inCat
                .Where(d => search.Length == 0 ||
                            d.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                            d.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (shown.Count == 0)
                continue;

            var section = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 10 };

            var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            header.AddChild(new PanelContainer
            {
                SetSize = new Vector2(8, 8),
                VerticalAlignment = VAlignment.Center,
                PanelOverride = new StyleBoxFlat { BackgroundColor = FSPerkPalette.Accent[cat] },
            });
            header.AddChild(new Label { Text = CatLabel[cat], FontOverride = _fontSmallBold, FontColorOverride = FSPerkPalette.Accent[cat] });
            header.AddChild(new Label
            {
                Text = $"{inCat.Count(d => Level(d.Id) > 0)} of {inCat.Count} owned",
                FontColorOverride = FSUiPalette.TextMuted,
            });
            section.AddChild(header);

            var grid = new GridContainer
            {
                MaxGridWidth = MathF.Max(_gridWidth, TileWidth),
                HSeparationOverride = 8,
                VSeparationOverride = 8,
            };
            foreach (var def in shown)
                grid.AddChild(BuildTile(def));
            _grids.Add(grid);

            section.AddChild(grid);
            PerkGrid.AddChild(section);
        }

        if (_grids.Count == 0)
        {
            PerkGrid.AddChild(new Label
            {
                Text = $"No perks match \"{_search}\".",
                FontColorOverride = FSUiPalette.TextMuted,
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
        }
    }

    private Control BuildTile(FSPerkDef def)
    {
        var level = Level(def.Id);
        var locked = level == 0;
        var maxed = level == FSPerkDef.MaxLevel;
        var selected = _selectedId == def.Id;
        var slot = _state == null ? -1 : Array.IndexOf(_state.Slots, def.Id);

        var tile = new ContainerButton
        {
            SetSize = new Vector2(TileWidth, TileHeight),
            ToolTip = def.Name + (locked ? " — not owned" : $" — level {level} of {FSPerkDef.MaxLevel}"),
        };

        // Unowned tiles keep a muted wash of their category so the catalogue still reads by colour.
        var categoryFill = FSPerkPalette.Background[def.Category];
        var categoryEdge = FSPerkPalette.Edge[def.Category];
        var fill = locked ? Color.InterpolateBetween(FSUiPalette.BgRecess, categoryFill, 0.55f) : categoryFill;
        var restEdge = locked ? Color.InterpolateBetween(FSUiPalette.BgTrack, categoryEdge, 0.6f) : categoryEdge;

        void Paint(bool hover)
        {
            var edge = selected ? FSUiPalette.AccentBrand : hover ? FSPerkPalette.Accent[def.Category] : restEdge;
            tile.StyleBoxOverride = Box(fill, edge, new Thickness(selected ? 2 : 1));
        }
        Paint(false);
        tile.OnMouseEntered += _ => Paint(true);
        tile.OnMouseExited += _ => Paint(false);
        tile.OnPressed += _ => Select(def.Id);

        var layers = new LayoutContainer();

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SetSize = new Vector2(TileWidth, TileHeight - 10),
        };
        LayoutContainer.SetPosition(content, new Vector2(0, 12));

        if (GetPerkIcon(def.Id) is { } icon)
        {
            content.AddChild(new TextureRect
            {
                Texture = icon,
                Stretch = TextureRect.StretchMode.Scale,
                SetSize = new Vector2(44, 44),
                HorizontalAlignment = HAlignment.Center,
                Modulate = locked ? FSPerkPalette.Accent[def.Category].WithAlpha(0.6f) : Color.White,
            });
        }

        var nameBox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            VerticalExpand = true,
            Margin = new Thickness(4, 6, 4, 0),
        };
        foreach (var line in SplitName(def.Name))
        {
            nameBox.AddChild(new Label
            {
                Text = line,
                FontOverride = _fontSmallBold,
                FontColorOverride = locked ? FSUiPalette.TextMuted : FSUiPalette.TextPrimary,
                HorizontalAlignment = HAlignment.Center,
            });
        }
        content.AddChild(nameBox);

        var pips = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 3,
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10),
        };
        for (var i = 0; i < FSPerkDef.MaxLevel; i++)
        {
            var on = i < level;
            pips.AddChild(new PanelContainer
            {
                SetSize = new Vector2(14, 4),
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = on ? (maxed ? FSUiPalette.Currency : FSPerkPalette.Accent[def.Category]) : FSUiPalette.BgTrack,
                },
            });
        }
        content.AddChild(pips);
        layers.AddChild(content);

        if (slot >= 0)
        {
            var badge = new PanelContainer
            {
                SetSize = new Vector2(16, 16),
                PanelOverride = new StyleBoxFlat { BackgroundColor = FSUiPalette.TextPrimary },
            };
            badge.AddChild(new Label
            {
                Text = (slot + 1).ToString(),
                FontOverride = _fontSmallBold,
                FontColorOverride = FSUiPalette.BgDeep,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
            });
            LayoutContainer.SetPosition(badge, new Vector2(5, 5));
            layers.AddChild(badge);
        }
        else if (locked)
        {
            var cost = new Label
            {
                Text = $"{FSPerkDef.CostForUpgrade(0)} PP",
                FontOverride = _fontSmallBold,
                FontColorOverride = FSUiPalette.Currency,
            };
            LayoutContainer.SetAnchorPreset(cost, LayoutContainer.LayoutPreset.TopRight);
            LayoutContainer.SetGrowHorizontal(cost, LayoutContainer.GrowDirection.Begin);
            LayoutContainer.SetMarginRight(cost, -6);
            LayoutContainer.SetMarginTop(cost, 3);
            layers.AddChild(cost);
        }

        tile.AddChild(layers);
        return tile;
    }

    // Long names break at the space nearest the middle so they read on two lines instead of clipping.
    private static IEnumerable<string> SplitName(string name)
    {
        if (name.Length <= 12 || !name.Contains(' '))
            return [name];

        var mid = name.Length / 2;
        var best = -1;
        for (var i = 0; i < name.Length; i++)
        {
            if (name[i] == ' ' && (best < 0 || Math.Abs(i - mid) < Math.Abs(best - mid)))
                best = i;
        }
        return [name[..best], name[(best + 1)..]];
    }

    private void Select(string id)
    {
        _selectedId = id;
        RebuildSlots();
        RebuildGrid();
        RefreshInfo();
    }
}
