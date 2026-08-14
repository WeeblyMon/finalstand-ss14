// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 Spatison <137375981+Spatison@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 kurokoTurbo <92106367+kurokoTurbo@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Trest <144359854+trest100@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
// SPDX-FileCopyrightText: 2025 Kayzel <43700376+KayzelW@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._FinalStand.Medical;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared._Shitmed.BodyEffects.Subsystems;
using Robust.Shared.Map;
using Robust.Shared.Containers;
using System.Numerics;

namespace Content.Server._Shitmed.BodyEffects.Subsystems;

public sealed partial class GenerateChildPartSystem : EntitySystem
{
    [Dependency] private OrganManipulationSystem _manipulation = default!;
    [Dependency] private SharedBodyAppearanceSystem _bodySystem = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GenerateChildPartComponent, OrganGotInsertedEvent>(OnPartAttached);
        SubscribeLocalEvent<GenerateChildPartComponent, OrganGotRemovedEvent>(OnPartDetached);
    }

    private void OnPartAttached(EntityUid uid, GenerateChildPartComponent component, ref OrganGotInsertedEvent args)
    {
        CreatePart(uid, component);
    }

    private void OnPartDetached(EntityUid uid, GenerateChildPartComponent component, ref OrganGotRemovedEvent args)
    {
        if (component.ChildPart == null || TerminatingOrDeleted(component.ChildPart))
            return;

        if (!_container.TryGetContainingContainer(
                (component.ChildPart.Value, Transform(component.ChildPart.Value), MetaData(component.ChildPart.Value)),
                out var container))
            return;

        _container.Remove(component.ChildPart.Value, container, false, true);
        QueueDel(component.ChildPart);
    }

    private void CreatePart(EntityUid uid, GenerateChildPartComponent component)
    {
        if (!TryComp(uid, out OrganComponent? partComp)
            || partComp.Body is null
            || component.Active)
            return;

        var childPart = Spawn(component.Id, new EntityCoordinates(partComp.Body.Value, Vector2.Zero));

        if (!TryComp(childPart, out OrganComponent? childPartComp))
            return;

        _manipulation.InsertOrgan(partComp.Body.Value, (childPart, childPartComp), uid);
        component.ChildPart = childPart;
        component.Active = true;
        Dirty(childPart, childPartComp);
    }
}