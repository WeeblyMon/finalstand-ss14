// Server half of the lobby Ready Manifest window, ported from Moffstation.
using Content.Server.EUI;
using Content.Shared._FinalStand.ReadyManifest;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.ReadyManifest;

public sealed class ReadyManifestEui(ReadyManifestSystem readyManifest) : BaseEui
{
    public override ReadyManifestEuiState GetNewState()
    {
        return new ReadyManifestEuiState(new Dictionary<ProtoId<JobPrototype>, int>(readyManifest.GetReadyManifest()));
    }

    public override void Closed()
    {
        readyManifest.RemoveEui(Player);
    }
}
