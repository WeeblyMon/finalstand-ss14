using Content.Shared._FinalStand.Engineering;

namespace Content.Client._FinalStand.Engineering;

public sealed partial class FSEngineeringObjectiveClientSystem : EntitySystem
{
    public IReadOnlyList<string> Lines => Latest?.Lines ?? (IReadOnlyList<string>) Array.Empty<string>();

    public FSEngineeringObjectiveEvent? Latest { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSEngineeringObjectiveEvent>(ev => Latest = ev);
    }
}
