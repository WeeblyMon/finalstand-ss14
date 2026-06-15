// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Shitmed.StatusEffects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;

namespace Content.Server._Shitmed.StatusEffects;

/// <summary>
/// Randomly teleports the entity to a location within <see cref="ScrambleRange"/> tiles
/// when <see cref="ScrambleLocationEffectComponent"/> is first added (e.g. from a dubious organ).
/// Replaces the upstream implementation which required Content.Goobstation.Shared.Teleportation.
/// </summary>
public sealed class ScrambleLocationEffectSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private const float ScrambleRange = 8f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ScrambleLocationEffectComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, ScrambleLocationEffectComponent comp, ComponentInit args)
    {
        var xform = Transform(uid);
        var worldPos = _xform.GetWorldPosition(xform);

        var angle = _random.NextFloat() * MathF.PI * 2f;
        var dist = _random.NextFloat() * ScrambleRange;
        var offset = new Vector2(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

        _xform.SetWorldPosition(uid, worldPos + offset);
    }
}
