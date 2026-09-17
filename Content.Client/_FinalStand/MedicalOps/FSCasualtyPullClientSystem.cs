using Content.Shared._FinalStand.MedicalOps;

namespace Content.Client._FinalStand.MedicalOps;

public sealed class FSCasualtyPullClientSystem : EntitySystem
{
    public void Request(NetEntity patient)
    {
        RaiseNetworkEvent(new FSCasualtyPullRequestEvent(patient));
    }
}
