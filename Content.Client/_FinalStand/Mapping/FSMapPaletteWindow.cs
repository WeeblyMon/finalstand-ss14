using System.Linq;
using System.Numerics;
using Content.Client.Mapping;
using Content.Shared._FinalStand.Mapping;
using Content.Client.Decals.UI;
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

    private BoxContainer _decalControls = default!;
    private ColorSelectorSliders _decalColorPicker = default!;
    private CheckBox _decalAutoBox = default!;
    private CheckBox _decalSnapBox = default!;
    private CheckBox _decalCleanableBox = default!;
    private PaletteColorPicker? _picker;

    private ProtoId<DecalPrototype>? _decalId;
    private Color _decalColor = Color.White;
    private float _decalRotation;
    private bool _decalSnap = true;
    private int _decalZIndex;
    private bool _decalCleanable;
    private bool _decalAuto;

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

        BuildDecalControls(root);
    }

    private void BuildDecalControls(BoxContainer root)
    {
        _decalControls = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            Visible = false,
        };

        var left = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        _decalColorPicker = new ColorSelectorSliders { IsAlphaVisible = true };
        _decalColorPicker.OnColorChanged += OnDecalColorPicked;

        var palette = new Button
        {
            Text = Loc.GetString("decal-placer-window-palette"),
            StyleClasses = { "ButtonSquare" },
        };
        palette.OnPressed += OnDecalPalettePressed;

        left.AddChild(_decalColorPicker);
        left.AddChild(palette);

        var right = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        _decalAutoBox = new CheckBox { Text = Loc.GetString("decal-placer-window-enable-auto") };
        _decalSnapBox = new CheckBox { Text = Loc.GetString("decal-placer-window-enable-snap"), Pressed = true };
        _decalCleanableBox = new CheckBox { Text = Loc.GetString("decal-placer-window-enable-cleanable") };

        _decalAutoBox.OnToggled += args =>
        {
            _decalAuto = args.Pressed;
            if (_decalId is { } id)
                SelectDecal(id);
        };
        _decalSnapBox.OnToggled += args =>
        {
            _decalSnap = args.Pressed;
            UpdateDecal();
        };
        _decalCleanableBox.OnToggled += args =>
        {
            _decalCleanable = args.Pressed;
            UpdateDecal();
        };

        var rotationRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var rotation = new FloatSpinBox(90.0f, 0) { HorizontalExpand = true };
        rotation.OnValueChanged += args =>
        {
            _decalRotation = args.Value;
            UpdateDecal();
        };
        rotationRow.AddChild(new Label { Text = Loc.GetString("decal-placer-window-rotation") });
        rotationRow.AddChild(rotation);

        var zRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var zIndex = new SpinBox { HorizontalExpand = true };
        zIndex.ValueChanged += args =>
        {
            _decalZIndex = args.Value;
            UpdateDecal();
        };
        zRow.AddChild(new Label { Text = Loc.GetString("decal-placer-window-zindex") });
        zRow.AddChild(zIndex);

        right.AddChild(_decalAutoBox);
        right.AddChild(_decalSnapBox);
        right.AddChild(_decalCleanableBox);
        right.AddChild(rotationRow);
        right.AddChild(zRow);

        _decalControls.AddChild(left);
        _decalControls.AddChild(right);
        root.AddChild(_decalControls);
    }

    private void OnDecalColorPicked(Color color)
    {
        _decalColor = color;
        _decalColorPicker.Color = color;
        UpdateDecal();
    }

    private void OnDecalPalettePressed(ButtonEventArgs args)
    {
        if (_picker == null)
        {
            _picker = new PaletteColorPicker();
            _picker.OpenToLeft();
            _picker.PaletteList.OnItemSelected += selected =>
            {
                var color = ((Color?) selected.ItemList.GetSelected().First().Metadata)!.Value;
                OnDecalColorPicked(color);
            };

            return;
        }

        if (_picker.IsOpen)
            _picker.Close();
        else
            _picker.Open();
    }

    private void UpdateDecal()
    {
        if (_decalId is { } id)
            _decal.UpdateDecalInfo(id, _decalColor, _decalRotation, _decalSnap, _decalZIndex, _decalCleanable);
    }

    private void SelectDecal(string decalId)
    {
        if (!_protos.TryIndex<DecalPrototype>(decalId, out var decal))
            return;

        _decalId = decalId;

        if (_decalAuto)
        {
            _decalColor = Color.White;
            _decalCleanable = decal.DefaultCleanable;
            _decalSnap = decal.DefaultSnap;

            _decalColorPicker.Color = _decalColor;
            _decalCleanableBox.Pressed = _decalCleanable;
            _decalSnapBox.Pressed = _decalSnap;
        }

        UpdateDecal();
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
            _decalControls.Visible = false;
            return;
        }

        ClearErasers();

        switch (prototype)
        {
            case EntityPrototype entity:
                _decal.SetActive(false);
                _decalControls.Visible = false;
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
                _decalControls.Visible = false;
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
                _decalControls.Visible = true;
                SelectDecal(decal.ID);
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
