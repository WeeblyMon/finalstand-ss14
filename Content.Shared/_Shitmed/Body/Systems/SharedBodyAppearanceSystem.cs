// Remnant of Goob's body system. Only part appearance still lives here; it goes away with the
// markings port, when vanilla's SharedVisualBodySystem takes over.

using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared.Body.Systems;

public abstract partial class SharedBodyAppearanceSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager Prototypes = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        InitializePartAppearances();
    }
}
