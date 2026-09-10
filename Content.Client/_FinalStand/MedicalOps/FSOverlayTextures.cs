using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.MedicalOps;

public static class FSOverlayTextures
{
    public static Texture? TryLoad(IResourceCache cache, string path)
    {
        try
        {
            return cache.GetResource<TextureResource>(new ResPath(path)).Texture;
        }
        catch (Exception e)
        {
            IoCManager.Resolve<ILogManager>()
                .GetSawmill("fs.overlay")
                .Error($"Overlay texture '{path}' failed to load, so it will not render: {e.Message}");

            return null;
        }
    }
}
