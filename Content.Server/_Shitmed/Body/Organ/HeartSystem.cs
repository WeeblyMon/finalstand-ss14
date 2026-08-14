// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._FinalStand.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Events;
using Content.Server.Body.Components;
using Content.Shared.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared._Shitmed.Body.Organ;
using Content.Server._Shitmed.DelayedDeath;

namespace Content.Server._Shitmed.Body.Organ;

public sealed partial class HeartSystem : EntitySystem
{
    [Dependency] private OrganLookupSystem _lookup = default!;
    [Dependency] private SharedBodyAppearanceSystem _bodySystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeartComponent, OrganGotInsertedEvent>(HandleAddition);
        SubscribeLocalEvent<HeartComponent, OrganGotRemovedEvent>(HandleRemoval);
    }

    private void HandleRemoval(EntityUid uid, HeartComponent _, ref OrganGotRemovedEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.Target))
            return;

        // TODO: Add some form of very violent bleeding effect.
        EnsureComp<DelayedDeathComponent>(args.Target);
    }

    private void HandleAddition(EntityUid uid, HeartComponent _, ref OrganGotInsertedEvent args)
    {
        if (TerminatingOrDeleted(uid) || TerminatingOrDeleted(args.Target))
            return;

        if (_lookup.TryGetBodyOrgans<BrainComponent>(args.Target, out var _))
            RemComp<DelayedDeathComponent>(args.Target);
    }
    // Shitmed-End
}