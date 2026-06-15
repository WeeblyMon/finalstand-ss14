// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Shitmed.Augments;
using Robust.Client.UserInterface;

namespace Content.Client._Shitmed.Augments;

public sealed class AugmentToolPanelMenuBoundUserInterface : BoundUserInterface
{
    public AugmentToolPanelMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
    }
}
