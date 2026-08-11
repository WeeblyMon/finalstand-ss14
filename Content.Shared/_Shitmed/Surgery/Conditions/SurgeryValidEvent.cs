// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared._Shitmed.Medical.Surgery.Conditions;

// Raised on the entity that is receiving surgery.

[ByRefEvent]
public record struct SurgeryValidEvent(
    EntityUid Body,
    EntityUid Organ,
    bool Cancelled = false,
    ProtoId<OrganCategoryPrototype>? Category = null);
