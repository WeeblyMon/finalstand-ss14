// SPDX-FileCopyrightText: 2022 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2022 metalgearsloth <metalgearsloth@gmail.com>
// SPDX-FileCopyrightText: 2023 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Systems;
using Content.Shared.Body.Part;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client.Body.Systems;

public sealed class BodySystem : SharedBodySystem
{
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly SpriteSystem _spriteSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartComponent, AfterAutoHandleStateEvent>(OnBodyPartStateApplied);
        SubscribeLocalEvent<BodyPartComponent, ComponentStartup>(OnBodyPartStartup);
    }

    private void OnBodyPartStartup(EntityUid uid, BodyPartComponent comp, ref ComponentStartup args)
    {
        TryHideAttachedPart(uid, comp);
    }

    private void OnBodyPartStateApplied(EntityUid uid, BodyPartComponent comp, ref AfterAutoHandleStateEvent args)
    {
        TryHideAttachedPart(uid, comp);
    }

    private void TryHideAttachedPart(EntityUid uid, BodyPartComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var shouldHide = comp.Body.HasValue;
        _spriteSystem.SetVisible((uid, sprite), !shouldHide);
        _spriteSystem.SetContainerOccluded((uid, sprite), shouldHide);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<BodyPartComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var bodyPart, out var sprite))
        {
            var shouldHide = bodyPart.Body.HasValue;
            if (shouldHide && sprite.Visible)
            {
                _spriteSystem.SetVisible((uid, sprite), false);
                _spriteSystem.SetContainerOccluded((uid, sprite), true);
            }
            else if (!shouldHide && !sprite.Visible)
            {
                _spriteSystem.SetVisible((uid, sprite), true);
                _spriteSystem.SetContainerOccluded((uid, sprite), false);
            }
        }
    }

    private void ApplyMarkingToPart(MarkingPrototype markingPrototype,
        IReadOnlyList<Color>? colors,
        bool visible,
        SpriteComponent sprite)
    {
        for (var j = 0; j < markingPrototype.Sprites.Count; j++)
        {
            var markingSprite = markingPrototype.Sprites[j];

            if (markingSprite is not SpriteSpecifier.Rsi rsi)
                continue;

            var layerId = $"{markingPrototype.ID}-{rsi.RsiState}";

            if (!sprite.LayerMapTryGet(layerId, out _))
            {
                var layer = sprite.AddLayer(markingSprite, j + 1);
                sprite.LayerMapSet(layerId, layer);
                sprite.LayerSetSprite(layerId, rsi);
            }

            sprite.LayerSetVisible(layerId, visible);

            if (!visible)
                continue;

            if (colors != null && j < colors.Count)
                sprite.LayerSetColor(layerId, colors[j]);
            else
                sprite.LayerSetColor(layerId, Color.White);
        }
    }

    protected override void ApplyPartMarkings(EntityUid target, BodyPartAppearanceComponent component)
    {
        if (!TryComp(target, out SpriteComponent? sprite))
            return;

        if (component.Color != null)
            sprite.Color = component.Color.Value;

        foreach (var (visualLayer, markingList) in component.Markings)
            foreach (var marking in markingList)
            {
                if (!_markingManager.TryGetMarking(marking, out var markingPrototype))
                    continue;

                ApplyMarkingToPart(markingPrototype, marking.MarkingColors, visible: true, sprite);
            }
    }
}
