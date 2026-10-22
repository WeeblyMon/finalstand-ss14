using Content.Shared._FinalStand.Engineering;
using Content.Shared.GameTicking;

namespace Content.Client._FinalStand.Engineering;

public sealed partial class FSEngineeringObjectiveClientSystem : EntitySystem
{
    public IReadOnlyList<string> Lines => Latest?.Lines ?? (IReadOnlyList<string>) Array.Empty<string>();

    public FSEngineeringObjectiveEvent? Latest { get; private set; }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<FSEngineeringObjectiveEvent>(ev => Latest = ev);
        // Only engineers are sent objectives, so a player who isn't one next round would keep this round's.
        SubscribeNetworkEvent<RoundRestartCleanupEvent>(_ => Latest = null);
    }
}
