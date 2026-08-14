// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._FinalStand.Medical;
using System.Linq;
using Content.Server.Body.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Consciousness.Components;
using Content.Shared._Shitmed.Medical.Surgery.Pain.Systems;
using Content.Shared._Shitmed.Medical.Surgery.Traumas.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Components;
using Content.Shared._Shitmed.Medical.Surgery.Wounds.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Body.Components;
using Content.Shared.Body;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared._Shitmed.Tourniquet;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Server._Shitmed.Medical.Tourniquet;

/// <summary>
/// This handles tourniqueting people
/// </summary>
public sealed partial class TourniquetSystem : EntitySystem
{
    [Dependency] private OrganLookupSystem _lookup = default!;
    [Dependency] private SharedBodyAppearanceSystem _body = default!;
    [Dependency] private WoundSystem _wound = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private PainSystem _pain = default!;
    [Dependency] private Content.Shared.Body.Systems.BloodstreamSystem _bloodstream = default!;

    private const string TourniquetContainerId = "Tourniquet";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TourniquetComponent, UseInHandEvent>(OnTourniquetUse);
        SubscribeLocalEvent<TourniquetComponent, AfterInteractEvent>(OnTourniquetAfterInteract);

        SubscribeLocalEvent<BodyComponent, TourniquetDoAfterEvent>(OnBodyDoAfter);
        SubscribeLocalEvent<BodyComponent, RemoveTourniquetDoAfterEvent>(OnTourniquetTakenOff);

        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<InnateVerb>>(OnBodyGetVerbs);
    }

    private bool TryTourniquet(EntityUid target, EntityUid user, EntityUid tourniquetEnt, TourniquetComponent tourniquet)
    {
        if (!TryComp<TargetingComponent>(user, out var targeting)
            || !HasComp<BodyComponent>(user)
            || !HasComp<ConsciousnessComponent>(user)) // To prevent people from tourniqueting simple mobs
            return false;


        var categories = OrganCategories.FromTarget(targeting.Target).ToList();
        if (categories.Any(tourniquet.BlockedBodyParts.Contains))
        {
            _popup.PopupEntity(Loc.GetString("cant-put-tourniquet-here"), target, PopupType.MediumCaution);
            return false;
        }

        _popup.PopupEntity(Loc.GetString("puts-on-a-tourniquet", ("user", user), ("part", categories.FirstOrDefault().Id)), target, PopupType.Medium);
        _audio.PlayPvs(tourniquet.TourniquetPutOnSound, target, AudioParams.Default.WithVariation(0.125f).WithVolume(1f));

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager,
                user,
                tourniquet.Delay,
                new TourniquetDoAfterEvent(),
                target,
                target: target,
                used: tourniquetEnt)
            {
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
        return true;
    }

    private void TakeOffTourniquet(EntityUid target, EntityUid user, EntityUid tourniquetEnt, TourniquetComponent tourniquet)
    {
        _popup.PopupEntity(Loc.GetString("takes-off-a-tourniquet",
            ("user", user),
            ("part", tourniquet.BodyPartTorniqueted!)),
            target,
            PopupType.Medium);
        _audio.PlayPvs(tourniquet.TourniquetPutOffSound, target, AudioParams.Default.WithVariation(0.125f).WithVolume(1f));

        var doAfterEventArgs =
            new DoAfterArgs(EntityManager, user, tourniquet.RemoveDelay, new RemoveTourniquetDoAfterEvent(), target, target: target, used: tourniquetEnt)
            {
                BreakOnDamage = true,
                NeedHand = true,
                BreakOnMove = true,
                BreakOnWeightlessMove = false,
            };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnTourniquetUse(Entity<TourniquetComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryTourniquet(args.User, args.User, ent, ent))
            args.Handled = true;
    }

    private void OnTourniquetAfterInteract(Entity<TourniquetComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled
            || !args.CanReach
            || args.Target == null)
            return;

        if (TryTourniquet(args.Target.Value, args.User, ent, ent))
            args.Handled = true;
    }

    private void OnBodyDoAfter(EntityUid ent, BodyComponent comp, ref TourniquetDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<TourniquetComponent>(args.Used, out var tourniquet))
            return;

        if (!TryComp<TargetingComponent>(args.User, out var targeting))
            return;

        var container = _container.EnsureContainer<ContainerSlot>(args.Target!.Value, TourniquetContainerId);
        if (container.ContainedEntity.HasValue)
        {
            _popup.PopupEntity(Loc.GetString("already-tourniqueted"), ent, PopupType.Medium);
            return;
        }

        var targetCategory = OrganCategories.FromTarget(targeting.Target).FirstOrDefault();

        var targetPart = _lookup.EnumerateOrgansOfCategory(ent, targetCategory)
            .Cast<Entity<OrganComponent>?>()
            .FirstOrDefault();

        if (targetPart == null)
        {
            // The limb is gone, so tourniquet whatever it used to hang off instead.
            var tourniquetable = EntityUid.Invalid;
            foreach (var bodyPart in _lookup.GetBodyOrgans((ent, comp)))
            {
                if (!_lookup.EnumerateChildOrgans(bodyPart.Owner)
                        .Any(child => child.Comp.Category == targetCategory))
                    continue;

                tourniquetable = bodyPart.Owner;
                break;
            }

            if (tourniquetable == EntityUid.Invalid)
            {
                _popup.PopupEntity(Loc.GetString("missing-body-part"), ent, args.User, PopupType.MediumCaution);
                return;
            }

            var tourniquetableWounds = new List<Entity<WoundComponent, TourniquetableComponent>>();

            foreach (var woundEnt in _wound.GetWoundableWounds(tourniquetable))
            {
                if (!TryComp<TourniquetableComponent>(woundEnt, out var tourniquetableComp))
                    continue;

                if (tourniquetableComp.SeveredCategory == targetCategory)
                    tourniquetableWounds.Add((woundEnt.Owner, woundEnt.Comp, tourniquetableComp));
            }

            if (tourniquetableWounds.Count <= 0
               || !_container.Insert(args.Used.Value, container))
            {
                _popup.PopupEntity(Loc.GetString("no-wounds-tourniquet"), ent, PopupType.Medium);
                return;
            }

            foreach (var woundEnt in tourniquetableWounds)
            {
                if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleedInflicter))
                    continue;

                _bloodstream.TryAddBleedModifier(woundEnt, "TourniquetPresent", 100, false, bleedInflicter);
                woundEnt.Comp2.CurrentTourniquetEntity = args.Used;
            }

            tourniquet.BodyPartTorniqueted = tourniquetable;
        }
        else
        {
            if (!_container.Insert(args.Used.Value, container))
            {
                _popup.PopupEntity(Loc.GetString("cant-tourniquet"), ent, PopupType.Medium);
                return;
            }
            _pain.TryAddPainFeelsModifier(args.Used.Value, "Tourniquet", targetPart.Value.Owner, -10f);
            _bloodstream.TryAddBleedModifier(targetPart.Value.Owner, "TourniquetPresent", 100, false, true);

            foreach (var woundable in _wound.GetAllWoundableChildren(targetPart.Value.Owner))
            {
                _pain.TryAddPainFeelsModifier(args.Used.Value, "Tourniquet", woundable, -10f);
                _bloodstream.TryAddBleedModifier(woundable, "TourniquetPresent", 100, false, true, woundable);
            }

            tourniquet.BodyPartTorniqueted = targetPart.Value.Owner;
        }
        args.Handled = true;
    }

    private void OnTourniquetTakenOff(Entity<BodyComponent> ent, ref RemoveTourniquetDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!TryComp<TourniquetComponent>(args.Used, out var tourniquet))
            return;

        if (!_container.TryGetContainer(ent, TourniquetContainerId, out var container))
            return;

        var tourniquetedBodyPart = tourniquet.BodyPartTorniqueted;
        if (tourniquetedBodyPart == null)
            return;

        var bodyPartComp = Comp<OrganComponent>(tourniquetedBodyPart.Value);
        if (bodyPartComp.Category is { } blockedCategory
            && tourniquet.BlockedBodyParts.Contains(blockedCategory))
        {
            foreach (var woundEnt in _wound.GetWoundableWounds(tourniquetedBodyPart.Value))
            {
                if (!TryComp<BleedInflicterComponent>(woundEnt, out var bleedInflicter))
                    continue;

                if (!TryComp<TourniquetableComponent>(woundEnt, out var tourniquetableComp))
                    continue;

                if (tourniquetableComp.CurrentTourniquetEntity != args.Used)
                    continue;

                tourniquetableComp.CurrentTourniquetEntity = null;
                _bloodstream.TryRemoveBleedModifier(woundEnt, "TourniquetPresent", bleedInflicter);
            }
        }
        else
        {
            _pain.TryRemovePainFeelsModifier(args.Used.Value, "Tourniquet", tourniquetedBodyPart.Value);
            _bloodstream.TryRemoveBleedModifier(tourniquetedBodyPart.Value, "TourniquetPresent", true);

            foreach (var woundable in _wound.GetAllWoundableChildren(tourniquetedBodyPart.Value))
            {
                _pain.TryRemovePainFeelsModifier(args.Used.Value, "Tourniquet", woundable);
                _bloodstream.TryRemoveBleedModifier(woundable, "TourniquetPresent", true, woundable);
            }
        }

        _container.Remove(args.Used.Value, container);

        _hands.TryPickupAnyHand(args.User, args.Used.Value);
        tourniquet.BodyPartTorniqueted = null;

        args.Handled = true;
    }

    private void OnBodyGetVerbs(EntityUid ent, BodyComponent comp, GetVerbsEvent<InnateVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!_container.TryGetContainer(args.Target, TourniquetContainerId, out var container))
            return;

        foreach (var entity in container.ContainedEntities)
        {
            var tourniquet = Comp<TourniquetComponent>(entity);
            InnateVerb verb = new()
            {
                Act = () => TakeOffTourniquet(args.Target, args.User, entity, tourniquet),
                Text = Loc.GetString("take-off-tourniquet", ("part", tourniquet.BodyPartTorniqueted!)),
                // Icon = new SpriteSpecifier.Texture(new ("/Textures/")),
                Priority = 2
            };
            args.Verbs.Add(verb);
        }
    }
}
