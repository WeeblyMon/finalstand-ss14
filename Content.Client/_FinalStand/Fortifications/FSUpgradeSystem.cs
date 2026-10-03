// Applies a fortification's grade look (base state, tint, crack overlay) and keeps crack stages in step with its max health.
using Content.Client.Damage;
using Content.Shared._FinalStand.Fortifications;
using Content.Shared._FinalStand.Repair;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Fortifications;

public sealed class FSUpgradeSystem : SharedFSUpgradeSystem
{
    [Dependency] private SpriteSystem _sprite = default!;

    private const string OverlayLayer = "DamageOverlay";
    private const float CrackStages = 16f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSUpgradeableComponent, AfterAutoHandleStateEvent>(OnUpgradeState);
        SubscribeLocalEvent<FSStructureHealthComponent, AfterAutoHandleStateEvent>(OnHealthState);
    }

    private void OnUpgradeState(Entity<FSUpgradeableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var grade = Proto.Index(ent.Comp.Grade);
        var target = (ent.Owner, sprite);
        if (grade.State is { } state)
            _sprite.LayerSetRsiState(target, 0, state);

        if (grade.Color is { } color)
            _sprite.LayerSetColor(target, 0, color);

        if (grade.Cracks is not { } cracks
            || !TryComp<DamageVisualsComponent>(ent, out var damageVisuals)
            || damageVisuals.DamageOverlay is not { } overlay
            || overlay.Sprite == cracks)
            return;

        overlay.Sprite = cracks;
        if (_sprite.LayerMapTryGet(target, OverlayLayer, out var layer, false))
            _sprite.LayerSetRsi(target, layer, new ResPath(cracks), _sprite.LayerGetRsiState(target, layer));
    }

    private void OnHealthState(Entity<FSStructureHealthComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Comp.Max > 0f
            && HasComp<FSUpgradeableComponent>(ent)
            && TryComp<DamageVisualsComponent>(ent, out var damageVisuals))
            damageVisuals.Divisor = ent.Comp.Max / CrackStages;
    }
}
