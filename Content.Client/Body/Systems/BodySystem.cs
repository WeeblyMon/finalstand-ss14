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
using Content.Shared.Body;
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

    public override void Initialize()
    {
        base.Initialize();
        // Body parts have enableOverrideDir: North on their sprite, so they are visible
        // as a phantom when ContainerOccluded is false. The container system sets this flag
        // in UpdateEntityRecursively, but there is a race condition: the root body part's
        // parent is set (from transform state) before the container state arrives, so
        // UpdateEntityRecursively finds no container and leaves ContainerOccluded = false.
        //
        // Fix: subscribe to AfterAutoHandleStateEvent on BodyPartComponent. The networked
        // Body field is set at the same time as container state (same server tick), so
        // Body.HasValue is a reliable proxy for "this part is inside a body and should be
        // occluded." This fires after each state application and corrects the flag directly.
        SubscribeLocalEvent<BodyPartComponent, AfterAutoHandleStateEvent>(OnBodyPartStateApplied);
    }

    private void OnBodyPartStateApplied(EntityUid uid, BodyPartComponent comp, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        // Hide when attached to a body; show when detached (on ground, surgery table, etc.).
        // The container system will also set ContainerOccluded = true in FrameUpdate when
        // the part is in a non-ShowContents container, so these two agree in steady state.
        sprite.ContainerOccluded = comp.Body.HasValue;
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
