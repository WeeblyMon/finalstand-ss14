// Applies a fortification's grade look (base state, tint, crack overlay) and keeps crack stages in step with its max health.
using Content.Client.Damage;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.Repair;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Doors.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Fortifications;

public sealed partial class FSUpgradeSystem : SharedFSUpgradeSystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    private const string OverlayLayer = "DamageOverlay";
    private const float CrackStages = 16f;
    private const string LayerPrefix = "fs-upgrade-";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSUpgradeableComponent, AfterAutoHandleStateEvent>(OnUpgradeState);
        SubscribeLocalEvent<FSStructureHealthComponent, AfterAutoHandleStateEvent>(OnHealthState);
        SubscribeLocalEvent<FSUpgradeableComponent, AppearanceChangeEvent>((uid, comp, args) => HideWhenFolded((uid, comp), args.Sprite));
    }

    private void HideWhenFolded(Entity<FSUpgradeableComponent> ent, SpriteComponent? sprite)
    {
        if (sprite == null || !_appearance.TryGetData<DoorState>(ent, DoorVisuals.State, out var state))
            return;

        var folded = state is DoorState.Open or DoorState.Opening;
        var installed = new List<ProtoId<FSUpgradePrototype>>(ent.Comp.Modules);
        if (ent.Comp.Branch is { } branch)
            installed.Add(branch);

        foreach (var id in installed)
        {
            for (var i = 0; i < Proto.Index(id).Layers.Count; i++)
            {
                if (_sprite.LayerMapTryGet((ent.Owner, sprite), $"{LayerPrefix}{id}-{i}", out var layer, false))
                    _sprite.LayerSetVisible((ent.Owner, sprite), layer, !folded);
            }
        }
    }

    private void OnUpgradeState(Entity<FSUpgradeableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var grade = Proto.Index(ent.Comp.Grade);
        var target = (ent.Owner, sprite);
        if (TryComp<DoorComponent>(ent, out var door))
            GradeFolding((ent.Owner, door), target, grade);
        else if (grade.State is { } state)
            _sprite.LayerSetRsiState(target, 0, state);

        if (grade.Color is { } color)
            _sprite.LayerSetColor(target, 0, color);

        if (ent.Comp.Branch is { } branch)
            AddLayers(target, Proto.Index(branch));

        foreach (var module in ent.Comp.Modules)
            AddLayers(target, Proto.Index(module));

        HideWhenFolded(ent, sprite);

        var cracks = door != null ? grade.FoldingCracks : grade.Cracks;
        if (cracks == null
            || !TryComp<DamageVisualsComponent>(ent, out var damageVisuals)
            || damageVisuals.DamageOverlay is not { } overlay
            || overlay.Sprite == cracks)
            return;

        overlay.Sprite = cracks;
        if (_sprite.LayerMapTryGet(target, OverlayLayer, out var layer, false))
            _sprite.LayerSetRsi(target, layer, new ResPath(cracks), _sprite.LayerGetRsiState(target, layer));
    }

    private void GradeFolding(Entity<DoorComponent> door, Entity<SpriteComponent> sprite, FSUpgradePrototype grade)
    {
        if (grade.FoldingState is not { } upright || grade.FoldedState is not { } folded)
            return;

        door.Comp.ClosedSpriteState = upright;
        door.Comp.OpenSpriteState = folded;
        if (door.Comp.ClosedSpriteStates is { Count: > 0 } closedStates && door.Comp.OpenSpriteStates is { Count: > 0 } openStates)
        {
            closedStates[0] = (DoorVisualLayers.Base, upright);
            openStates[0] = (DoorVisualLayers.Base, folded);
        }

        _sprite.LayerSetRsiState(sprite.AsNullable(), 0, door.Comp.State is DoorState.Open or DoorState.Opening ? folded : upright);

        if (grade.FoldedCracks is { } foldedCracks && TryComp<FSFoldedDamageOverlayComponent>(door, out var overlay))
        {
            overlay.Folded = foldedCracks;
            overlay.Upright = grade.FoldingCracks;
        }
    }

    private void AddLayers(Entity<SpriteComponent> sprite, FSUpgradePrototype upgrade)
    {
        var target = sprite.AsNullable();
        var baseState = _sprite.LayerGetRsiState(target, 0).Name;
        for (var i = 0; i < upgrade.Layers.Count; i++)
        {
            var key = $"{LayerPrefix}{upgrade.ID}-{i}";
            if (!_sprite.LayerMapTryGet(target, key, out var layer, false))
            {
                layer = _sprite.AddLayer(target, upgrade.Layers[i], null);
                _sprite.LayerMapSet(target, key, layer);
            }

            if (upgrade.Layers[i].State is not { } state || baseState == null)
                continue;

            var variant = $"{baseState}_{state}";
            if (_sprite.LayerGetEffectiveRsi(target, layer)?.TryGetState(variant, out _) == true)
                _sprite.LayerSetRsiState(target, layer, variant);
        }
    }

    private void OnHealthState(Entity<FSStructureHealthComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Max > 0f
            && HasComp<FSUpgradeableComponent>(ent)
            && TryComp<DamageVisualsComponent>(ent, out var damageVisuals))
            damageVisuals.Divisor = ent.Comp.Max / CrackStages;
    }
}
