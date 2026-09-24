// Asks the server to open the pre-round lobby Ready Manifest (ported from Moffstation).
using Content.Shared._FinalStand.ReadyManifest;

namespace Content.Client._FinalStand.ReadyManifest;

public sealed class ReadyManifestSystem : EntitySystem
{
    public void RequestReadyManifest()
    {
        RaiseNetworkEvent(new RequestReadyManifestMessage());
    }
}
