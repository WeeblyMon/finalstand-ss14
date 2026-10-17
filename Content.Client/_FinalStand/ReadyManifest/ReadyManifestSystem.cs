using Content.Shared._FinalStand.ReadyManifest;

namespace Content.Client._FinalStand.ReadyManifest;

// Ported from Moffstation.
public sealed class ReadyManifestSystem : EntitySystem
{
    public void RequestReadyManifest()
    {
        RaiseNetworkEvent(new RequestReadyManifestMessage());
    }
}
