// Remnant of Goob's body system. Only part appearance still lives here; it goes away with the
// markings port, when vanilla's SharedVisualBodySystem takes over.

using System.Numerics;
using Content.Shared.Body;
using Content.Shared.Gibbing;
using Content.Shared.Gibbing.Events;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Body.Systems;

public abstract partial class SharedBodyAppearanceSystem : EntitySystem
{
    [Dependency] protected GibbingSystem Gibbing = default!;
    [Dependency] protected IPrototypeManager Prototypes = default!;
    [Dependency] protected IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializePartAppearances();
    }

    public virtual HashSet<EntityUid> GibBody(
        EntityUid bodyId,
        bool gibOrgans = false,
        BodyComponent? body = null,
        bool launchGibs = true,
        Vector2? splatDirection = null,
        float splatModifier = 1,
        Angle splatCone = default,
        SoundSpecifier? gibSoundOverride = null,
        GibType gib = GibType.Gib,
        GibContentsOption contents = GibContentsOption.Drop,
        List<string>? allowedContainers = null,
        List<string>? excludedContainers = null)
    {
        return Gibbing.Gib(bodyId, launchGibs);
    }

    public virtual HashSet<EntityUid> GibPart(
        EntityUid partId,
        OrganComponent? part = null,
        bool launchGibs = true,
        Vector2? splatDirection = null,
        float splatModifier = 1,
        Angle splatCone = default,
        SoundSpecifier? gibSoundOverride = null)
    {
        return new HashSet<EntityUid>();
    }

    public virtual bool BurnPart(EntityUid partId, OrganComponent? part = null)
    {
        return false;
    }
}
