using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared._Shitmed.Body.Part;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Network;

namespace Content.Shared.Body.Systems;
public partial class SharedBodySystem
{
    [Dependency] private readonly MarkingManager _markingManager = default!;
    [Dependency] private readonly INetManager _net = default!;

    private void InitializePartAppearances()
    {
        base.Initialize();

        SubscribeLocalEvent<BodyPartAppearanceComponent, ComponentStartup>(OnPartAppearanceStartup);
        SubscribeLocalEvent<BodyPartAppearanceComponent, AfterAutoHandleStateEvent>(HandleState);
        SubscribeLocalEvent<BodyComponent, BodyPartAddedEvent>(OnPartAttachedToBody);
        SubscribeLocalEvent<BodyComponent, BodyPartRemovedEvent>(OnPartDroppedFromBody);
    }

    private void OnPartAppearanceStartup(EntityUid uid, BodyPartAppearanceComponent component, ComponentStartup args)
    {
        // Appearance setup handled by server/client implementations when HumanoidAppearanceSystem is available.
    }

    private void HandleState(EntityUid uid, BodyPartAppearanceComponent component, ref AfterAutoHandleStateEvent args) =>
        ApplyPartMarkings(uid, component);

    private void OnPartAttachedToBody(EntityUid uid, BodyComponent component, ref BodyPartAddedEvent args)
    {
        if (!TryComp(uid, out HumanoidAppearanceComponent? bodyAppearance)
            || _net.IsClient
            || !bodyAppearance.ProfileLoaded)
            return;

        BodyPartAppearanceComponent? partAppearance = null;

        if (!TryComp(args.Part, out partAppearance))
            partAppearance = EnsureComp<BodyPartAppearanceComponent>(args.Part);

        UpdateAppearance(uid, partAppearance);
    }

    private void OnPartDroppedFromBody(EntityUid uid, BodyComponent component, ref BodyPartRemovedEvent args)
    {
        if (TerminatingOrDeleted(uid)
            || TerminatingOrDeleted(args.Part)
            || !TryComp(uid, out HumanoidAppearanceComponent? bodyAppearance)
            || _timing.ApplyingState)
            return;

        BodyPartAppearanceComponent? partAppearance = null;
        if (!TryComp<BodyPartAppearanceComponent>(args.Part, out partAppearance))
            partAppearance = EnsureComp<BodyPartAppearanceComponent>(args.Part);

        RemoveAppearance(uid, partAppearance, args.Part);
    }

    protected virtual void UpdateAppearance(EntityUid target, BodyPartAppearanceComponent component)
    {
        // Overridden in server/client implementations when HumanoidAppearanceSystem is available.
    }

    protected virtual void RemoveAppearance(EntityUid entity, BodyPartAppearanceComponent component, EntityUid partEntity)
    {
        // Overridden in server/client implementations when HumanoidAppearanceSystem is available.
    }

    protected virtual void ApplyPartMarkings(EntityUid target, BodyPartAppearanceComponent component)
    {
        // Overridden in client for sprite marking application.
    }

    protected virtual void RemoveBodyMarkings(EntityUid target, BodyPartAppearanceComponent partAppearance, HumanoidAppearanceComponent bodyAppearance)
    {
        // Overridden in server for marking removal.
    }

    public void ModifyMarkings(EntityUid uid,
        Entity<BodyPartAppearanceComponent?> partAppearance,
        HumanoidAppearanceComponent bodyAppearance,
        HumanoidVisualLayers targetLayer,
        string markingId,
        bool remove = false)
    {
        // No-op until SharedHumanoidAppearanceSystem is available.
    }
}
