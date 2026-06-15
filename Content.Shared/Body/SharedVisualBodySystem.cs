using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Body;

/// <summary>
/// Class responsible for managing the appearance of an entity with <see cref="VisualBodyComponent" /> via its organs with <see cref="VisualOrganComponent" />
/// </summary>
public abstract partial class SharedVisualBodySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MarkingManager _marking = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedBodySystem _bodySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VisualOrganComponent, BodyRelayedEvent<OrganCopyAppearanceEvent>>(OnVisualOrganCopyAppearance);
        SubscribeLocalEvent<VisualOrganMarkingsComponent, BodyRelayedEvent<OrganCopyAppearanceEvent>>(OnMarkingsOrganCopyAppearance);
        SubscribeLocalEvent<VisualOrganComponent, BodyRelayedEvent<ApplyOrganProfileDataEvent>>(OnVisualOrganApplyProfile);
        SubscribeLocalEvent<VisualOrganMarkingsComponent, BodyRelayedEvent<ApplyOrganMarkingsEvent>>(OnMarkingsOrganApplyMarkings);

        // Relay visual events from the body entity down to all contained visual organs.
        SubscribeLocalEvent<VisualBodyComponent, ApplyOrganProfileDataEvent>(OnBodyRelayProfile);
        SubscribeLocalEvent<VisualBodyComponent, ApplyOrganMarkingsEvent>(OnBodyRelayMarkings);

        InitializeModifiers();
        InitializeInitial();
    }

    private void OnBodyRelayProfile(Entity<VisualBodyComponent> body, ref ApplyOrganProfileDataEvent args)
    {
        if (!TryComp<BodyComponent>(body.Owner, out var bodyComp))
            return;
        var bodyEntity = new Entity<BodyComponent>(body.Owner, bodyComp);
        foreach (var (organUid, _) in _bodySystem.GetBodyOrgans(body.Owner, bodyComp))
        {
            if (!HasComp<VisualOrganComponent>(organUid))
                continue;
            var relayed = new BodyRelayedEvent<ApplyOrganProfileDataEvent>(bodyEntity, args);
            RaiseLocalEvent(organUid, ref relayed);
        }
    }

    private void OnBodyRelayMarkings(Entity<VisualBodyComponent> body, ref ApplyOrganMarkingsEvent args)
    {
        if (!TryComp<BodyComponent>(body.Owner, out var bodyComp))
            return;
        var bodyEntity = new Entity<BodyComponent>(body.Owner, bodyComp);
        foreach (var (organUid, _) in _bodySystem.GetBodyOrgans(body.Owner, bodyComp))
        {
            if (!HasComp<VisualOrganMarkingsComponent>(organUid))
                continue;
            var relayed = new BodyRelayedEvent<ApplyOrganMarkingsEvent>(bodyEntity, args);
            RaiseLocalEvent(organUid, ref relayed);
        }
    }

    private List<Marking> ResolveMarkings(List<Marking> markings, Color? skinColor, Color? eyeColor, Dictionary<Enum, MarkingsAppearance> appearances)
    {
        var ret = new List<Marking>();
        var forcedColors = new List<(Marking, MarkingPrototype)>();

        // This method uses two loops since some marking with constrained colors care about the colors of previous markings.
        // As such we want to ensure we can apply the markings they rely on first.
        foreach (var marking in markings)
        {
            if (!_marking.TryGetMarking(marking, out var proto))
                continue;

            if (!proto.ForcedColoring && appearances.GetValueOrDefault(proto.BodyPart)?.MatchSkin != true)
                ret.Add(marking);
            else
                forcedColors.Add((marking, proto));
        }

        foreach (var (marking, prototype) in forcedColors)
        {
            var colors = MarkingColoring.GetMarkingLayerColors(
                prototype,
                skinColor,
                eyeColor,
                ret);

            var markingWithColor = new Marking(marking.MarkingId, colors)
            {
                Forced = marking.Forced,
            };
            if (appearances.GetValueOrDefault(prototype.BodyPart) is { MatchSkin: true } appearance && skinColor is { } color)
            {
                markingWithColor = markingWithColor.WithColor(color.WithAlpha(appearance.LayerAlpha));
            }
            ret.Add(markingWithColor);
        }

        return ret;
    }

    protected virtual void SetOrganColor(Entity<VisualOrganComponent> ent, Color color)
    {
        ent.Comp.Data.Color = color;
        Dirty(ent);
    }

    protected virtual void SetOrganAppearance(Entity<VisualOrganComponent> ent, PrototypeLayerData data)
    {
        ent.Comp.Data = data;
        Dirty(ent);
    }

    protected virtual void SetOrganMarkings(Entity<VisualOrganMarkingsComponent> ent, Dictionary<HumanoidVisualLayers, List<Marking>> markings)
    {
        ent.Comp.Markings = markings;
        Dirty(ent);
    }

    private void OnVisualOrganCopyAppearance(Entity<VisualOrganComponent> ent, ref BodyRelayedEvent<OrganCopyAppearanceEvent> args)
    {
        if (!TryComp<VisualOrganComponent>(args.Args.Organ, out var other))
            return;

        if (!other.Layer.Equals(ent.Comp.Layer))
            return;

        SetOrganAppearance(ent, other.Data);
    }

    private void OnMarkingsOrganCopyAppearance(Entity<VisualOrganMarkingsComponent> ent, ref BodyRelayedEvent<OrganCopyAppearanceEvent> args)
    {
        if (!TryComp<VisualOrganMarkingsComponent>(args.Args.Organ, out var other))
            return;

        if (!other.MarkingData.Layers.SetEquals(ent.Comp.MarkingData.Layers))
            return;

        SetOrganMarkings(ent, other.Markings);
    }

    private void OnVisualOrganApplyProfile(Entity<VisualOrganComponent> ent, ref BodyRelayedEvent<ApplyOrganProfileDataEvent> args)
    {
        var relevantData = args.Args.Base;

        if (relevantData is not { } data)
            return;

        ent.Comp.Profile = data;

        if (ent.Comp.Layer.Equals(HumanoidVisualLayers.Eyes))
            SetOrganColor(ent, ent.Comp.Profile.EyeColor);
        else
            SetOrganColor(ent, ent.Comp.Profile.SkinColor);

        if (ent.Comp.SexStateOverrides is { } overrides && overrides.TryGetValue(data.Sex, out var state))
        {
            ent.Comp.Data.State = state;
            SetOrganAppearance(ent, ent.Comp.Data);
        }
    }

    private void OnMarkingsOrganApplyMarkings(Entity<VisualOrganMarkingsComponent> ent, ref BodyRelayedEvent<ApplyOrganMarkingsEvent> args)
    {
        // Organ category concept removed; markings handled via body part appearance system
    }
}

/// <summary>
/// Raised on body entity, when an organ is having its appearance copied to it
/// </summary>
[ByRefEvent]
public readonly record struct OrganCopyAppearanceEvent(EntityUid Organ);

/// <summary>
/// Raised on body entity when profiles are being applied to it
/// </summary>
[ByRefEvent]
public readonly record struct ApplyOrganProfileDataEvent(OrganProfileData? Base, Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData>? Profiles);

/// <summary>
/// Raised on body entity when a profile is being applied to it
/// </summary>
[ByRefEvent]
public readonly record struct ApplyOrganMarkingsEvent(Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> Markings);

