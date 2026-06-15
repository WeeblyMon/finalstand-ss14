// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later
// Stub: Einstein Engines language system. Tracks what languages an entity speaks/understands.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Language;

[RegisterComponent, NetworkedComponent]
public sealed partial class LanguageKnowledgeComponent : Component
{
    [DataField]
    public List<string> Speaks = new();

    [DataField]
    public List<string> Understands = new();
}
