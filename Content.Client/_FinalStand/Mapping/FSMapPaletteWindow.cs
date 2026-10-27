using System.Linq;
using System.Numerics;
using Content.Client.Mapping;
using Content.Shared._FinalStand.Mapping;
using Content.Shared.Decals;
using Content.Shared.Maps;
using Robust.Client.Graphics;
using Robust.Client.Placement;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.GameObjects;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using static Robust.Client.UserInterface.Controls.BaseButton;
using static Robust.Client.UserInterface.Controls.LineEdit;

namespace Content.Client._FinalStand.Mapping;

public sealed class FSMapPaletteWindow : DefaultWindow
{
    private readonly IPrototypeManager _protos;
    private readonly IPlacementManager _placement;
    private readonly IResourceCache _resources;
    private readonly SpriteSystem _sprite;
    private readonly Content.Client.Decals.DecalPlacementSystem _decal;

    private readonly MappingPrototypeList _list;
    private readonly List<MappingPrototype> _leaves = new();

    private Button _eraseEntity = default!;
    private Button _eraseTile = default!;
    private OptionButton _placementMode = default!;

    public FSMapPaletteWindow()
    {
        _protos = IoCManager.Resolve<IPrototypeManager>();
        _placement = IoCManager.Resolve<IPlacementManager>();
        _resources = IoCManager.Resolve<IResourceCache>();

        var entities = IoCManager.Resolve<IEntityManager>();
        _sprite = entities.System<SpriteSystem>();
        _decal = entities.System<Content.Client.Decals.DecalPlacementSystem>();

        Title = Loc.GetString("fs-map-palette-title");
        MinSize = new Vector2(420, 560);
        SetSize = new Vector2(420, 720);

        _list = new MappingPrototypeList { VerticalExpand = true };

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        root.AddChild(_list);
        BuildToolStrip(root);
        Contents.AddChild(root);

        _list.GetPrototypeData += OnGetData;
        _list.SelectionChanged += OnSelectionChanged;
        _list.CollapseToggled += OnCollapseToggled;
        _list.SearchBar.OnTextChanged += OnSearch;
        _list.CollapseAllButton.OnPressed += OnCollapseAll;
        _list.ClearSearchButton.OnPressed += OnClearSearch;

        _protos.PrototypesReloaded += OnPrototypesReloaded;

        Reload();
    }

    private void BuildToolStrip(BoxContainer root)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

        _eraseEntity = new Button
        {
            Text = Loc.GetString("fs-map-palette-erase-entity"),
            ToggleMode = true,
            HorizontalExpand = true,
            StyleClasses = { "ButtonSquare" },
        };
        _eraseTile = new Button
        {
            Text = Loc.GetString("fs-map-palette-erase-tile"),
            ToggleMode = true,
            HorizontalExpand = true,
            StyleClasses = { "ButtonSquare" },
        };

        _eraseEntity.OnToggled += OnEraseEntityToggled;
        _eraseTile.OnToggled += OnEraseTileToggled;

        row.AddChild(_eraseEntity);
        row.AddChild(_eraseTile);
        root.AddChild(row);

        _placementMode = new OptionButton
        {
            HorizontalExpand = true,
            StyleClasses = { "ButtonSquare" },
            ToolTip = Loc.GetString("entity-spawn-window-override-menu-tooltip"),
            TooltipDelay = 0,
        };

        var modes = _placement.AllModeNames.ToList();
        for (var i = 0; i < modes.Count; i++)
        {
            _placementMode.AddItem(modes[i], i);
        }

        _placementMode.SelectId(0);
        _placementMode.OnItemSelected += OnPlacementModeSelected;
        root.AddChild(_placementMode);
    }

    private void OnPlacementModeSelected(OptionButton.ItemSelectedEventArgs args)
    {
        _placementMode.SelectId(args.Id);

        if (_placement.CurrentPermission is not { } permission)
            return;

        _placement.BeginPlacing(new PlacementInformation
        {
            PlacementOption = _placement.AllModeNames[args.Id],
            EntityType = permission.EntityType,
            TileType = permission.TileType,
            Range = permission.Range,
            IsTile = permission.IsTile,
        });
    }

    private void OnEraseEntityToggled(ButtonToggledEventArgs args)
    {
        if (args.Pressed == _placement.Eraser)
            return;

        _eraseTile.Pressed = false;
        _decal.SetActive(false);
        Deselect();

        _placement.Clear();
        _placement.ToggleEraser();
    }

    private void OnEraseTileToggled(ButtonToggledEventArgs args)
    {
        if (_placement.Eraser)
        {
            _eraseEntity.Pressed = false;
            _placement.ToggleEraser();
        }

        _decal.SetActive(false);
        Deselect();
        _placement.Clear();

        if (!args.Pressed)
            return;

        _placement.BeginPlacing(new PlacementInformation
        {
            PlacementOption = "AlignTileAny",
            TileType = 0,
            IsTile = true,
        });
    }

    private void Deselect()
    {
        if (_list.Selected is not { } selected)
            return;

        selected.Button.Pressed = false;
        _list.Selected = null;
    }

    private void ClearErasers()
    {
        if (_placement.Eraser)
            _placement.ToggleEraser();

        _eraseEntity.Pressed = false;
        _eraseTile.Pressed = false;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (!args.WasModified<FSMapPalettePrototype>() &&
            !args.WasModified<ContentTileDefinition>() &&
            !args.WasModified<DecalPrototype>() &&
            !args.WasModified<EntityPrototype>())
        {
            return;
        }

        Reload();
    }

    private void Reload()
    {
        _leaves.Clear();

        var nodes = new Dictionary<string, MappingPrototype>();
        foreach (var category in _protos.EnumeratePrototypes<FSMapPalettePrototype>())
        {
            nodes[category.ID] = new MappingPrototype(null, NameOf(category));
        }

        var roots = new List<(int Order, MappingPrototype Node)>();

        foreach (var category in _protos.EnumeratePrototypes<FSMapPalettePrototype>())
        {
            var node = nodes[category.ID];

            foreach (var childId in category.Children)
            {
                if (!nodes.TryGetValue(childId, out var child))
                    continue;

                Link(node, child);
            }

            foreach (var leaf in Leaves(category))
            {
                Link(node, leaf);
                _leaves.Add(leaf);
            }

            if (category.Root)
                roots.Add((category.Order, node));
        }

        foreach (var category in _protos.EnumeratePrototypes<FSMapPalettePrototype>())
        {
            var node = nodes[category.ID];
            var order = category.Children
                .Select((id, i) => (id.Id, i))
                .ToDictionary(x => x.Id, x => x.i);

            node.Children?.Sort((a, b) =>
            {
                var aIsCategory = a.Prototype == null;
                var bIsCategory = b.Prototype == null;
                if (aIsCategory != bIsCategory)
                    return aIsCategory ? -1 : 1;

                if (aIsCategory)
                {
                    var ai = Index(order, nodes, a);
                    var bi = Index(order, nodes, b);
                    if (ai != bi)
                        return ai.CompareTo(bi);
                }

                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        roots.Sort((a, b) => a.Order != b.Order
            ? a.Order.CompareTo(b.Order)
            : string.Compare(a.Node.Name, b.Node.Name, StringComparison.OrdinalIgnoreCase));

        _list.UpdateVisible(roots.Select(r => r.Node).ToList());
    }

    private static int Index(Dictionary<string, int> order, Dictionary<string, MappingPrototype> nodes, MappingPrototype node)
    {
        foreach (var (id, index) in order)
        {
            if (nodes.TryGetValue(id, out var candidate) && candidate == node)
                return index;
        }

        return int.MaxValue;
    }

    private static void Link(MappingPrototype parent, MappingPrototype child)
    {
        parent.Children ??= new List<MappingPrototype>();
        parent.Children.Add(child);
        child.Parents ??= new List<MappingPrototype>();
        child.Parents.Add(parent);
    }

    private string NameOf(FSMapPalettePrototype category)
    {
        return category.Name is { } name ? Loc.GetString(name) : category.ID;
    }

    private IEnumerable<MappingPrototype> Leaves(FSMapPalettePrototype category)
    {
        foreach (var id in category.Tiles)
        {
            if (_protos.TryIndex<ContentTileDefinition>(id, out var tile))
                yield return new MappingPrototype(tile, Loc.GetString(tile.Name));
        }

        foreach (var id in category.Entities)
        {
            if (_protos.TryIndex(id, out var entity))
                yield return new MappingPrototype(entity, entity.Name);
        }

        foreach (var id in category.Decals)
        {
            if (_protos.TryIndex<DecalPrototype>(id, out var decal))
                yield return new MappingPrototype(decal, decal.ID);
        }
    }

    private void OnGetData(IPrototype prototype, List<Texture> textures)
    {
        switch (prototype)
        {
            case EntityPrototype entity:
                textures.AddRange(_sprite.GetPrototypeTextures(entity).Select(t => t.Default));
                break;
            case DecalPrototype decal:
                textures.Add(_sprite.Frame0(decal.Sprite));
                break;
            case ContentTileDefinition tile:
                if (tile.Sprite?.ToString() is { } sprite)
                    textures.Add(_resources.GetResource<TextureResource>(sprite).Texture);
                break;
        }
    }

    private void OnSelectionChanged(MappingSpawnButton button, IPrototype? prototype)
    {
        if (_list.Selected is { } old && old != button)
        {
            old.Button.Pressed = false;
            _list.Selected = null;
        }

        if (!button.Button.Pressed)
        {
            _placement.Clear();
            _decal.SetActive(false);
            return;
        }

        ClearErasers();

        switch (prototype)
        {
            case EntityPrototype entity:
                _decal.SetActive(false);
                _placement.BeginPlacing(new PlacementInformation
                {
                    PlacementOption = _placementMode.SelectedId > 0
                        ? _placement.AllModeNames[_placementMode.SelectedId]
                        : entity.PlacementMode,
                    EntityType = entity.ID,
                    IsTile = false,
                });
                break;
            case ContentTileDefinition tile:
                _decal.SetActive(false);
                _placement.BeginPlacing(new PlacementInformation
                {
                    PlacementOption = "AlignTileAny",
                    TileType = tile.TileId,
                    IsTile = true,
                });
                break;
            case DecalPrototype decal:
                _placement.Clear();
                _decal.SetActive(true);
                _decal.UpdateDecalInfo(decal.ID, Color.White, 0, true, 0, false);
                break;
            default:
                return;
        }

        _list.Selected = button;
    }

    private void OnCollapseToggled(MappingSpawnButton button, ButtonToggledEventArgs args)
    {
        SetCollapsed(button, args.Pressed);
    }

    private void SetCollapsed(MappingSpawnButton button, bool expanded)
    {
        if (expanded)
        {
            if (button.Prototype?.Children != null)
            {
                foreach (var child in button.Prototype.Children)
                {
                    _list.Insert(button.ChildrenPrototypes, child, true);
                }
            }

            button.CollapseButton.Label.Text = "▼";
        }
        else
        {
            button.ChildrenPrototypes.RemoveAllChildren();
            button.CollapseButton.Label.Text = "▶";
        }
    }

    private void OnSearch(LineEditEventArgs args)
    {
        if (string.IsNullOrEmpty(args.Text))
        {
            _list.PrototypeList.Visible = true;
            _list.SearchList.Visible = false;
            return;
        }

        var matches = _leaves
            .Where(p => p.Name.Contains(args.Text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _list.PrototypeList.Visible = false;
        _list.SearchList.Visible = true;
        _list.Search(matches);
    }

    private void OnCollapseAll(ButtonEventArgs args)
    {
        foreach (var child in _list.PrototypeList.Children)
        {
            if (child is not MappingSpawnButton button || !button.CollapseButton.Pressed)
                continue;

            button.CollapseButton.Pressed = false;
            SetCollapsed(button, false);
        }

        _list.ScrollContainer.SetScrollValue(Vector2.Zero);
    }

    private void OnClearSearch(ButtonEventArgs args)
    {
        _list.SearchBar.Text = string.Empty;
        OnSearch(new LineEditEventArgs(_list.SearchBar, string.Empty));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        _protos.PrototypesReloaded -= OnPrototypesReloaded;
        _placement.Clear();
        _decal.SetActive(false);
    }
}
