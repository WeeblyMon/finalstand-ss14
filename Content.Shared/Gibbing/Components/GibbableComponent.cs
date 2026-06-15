// SPDX-FileCopyrightText: 2024 Jezithyr <jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2024 metalgearsloth <31366439+metalgearsloth@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Gibbing;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Gibbing.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(GibbingSystem))]
public sealed partial class GibbableComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<EntProtoId> GibPrototypes = new();

    [DataField, AutoNetworkedField]
    public int GibCount;

    [DataField, AutoNetworkedField]
    public SoundSpecifier? GibSound = new SoundCollectionSpecifier("gib", AudioParams.Default.WithVariation(0.025f));

    [DataField, AutoNetworkedField]
    public float GibScatterRange = 0.3f;
}
