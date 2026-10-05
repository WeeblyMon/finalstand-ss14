// Holds the latest engineering "what's next" lines from the server for the HUD to draw.
using Content.Shared._FinalStand.Engineering;

namespace Content.Client._FinalStand.Engineering;

public sealed partial class FSEngineeringObjectiveClientSystem : EntitySystem
{
    public IReadOnlyList<string> Lines { get; private set; } = Array.Empty<string>();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSEngineeringObjectiveEvent>(ev => Lines = ev.Lines);
    }
}
