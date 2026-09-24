// Pre-round lobby "Ready Manifest", ported from Moffstation (moff-station-14 PR #447).
using Content.Shared.Eui;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._FinalStand.ReadyManifest;

[Serializable, NetSerializable]
public sealed class RequestReadyManifestMessage : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class ReadyManifestEuiState(Dictionary<ProtoId<JobPrototype>, int> jobCounts) : EuiStateBase
{
    public readonly Dictionary<ProtoId<JobPrototype>, int> JobCounts = jobCounts;
}
