using Robust.Client.Graphics;
using Robust.Shared.ContentPack;
using Robust.Shared.Graphics;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Content.Client._FinalStand.UI;

public static class FSMipmaps
{
    private const int LargestLevel = 256;
    private const int SmallestLevel = 8;

    private static readonly Dictionary<string, Texture[]?> Chains = new();

    private static readonly TextureLoadParameters Linear = new()
    {
        SampleParameters = new TextureSampleParameters { Filter = true },
        Srgb = true,
    };

    public static Texture? Pick(string path, float pixels)
    {
        if (Chain(path) is not { } chain)
            return null;

        var pick = chain[0];
        foreach (var level in chain)
        {
            if (level.Width < pixels)
                break;
            pick = level;
        }

        return pick;
    }

    private static Texture[]? Chain(string path)
    {
        if (Chains.TryGetValue(path, out var cached))
            return cached;

        Texture[]? chain = null;
        if (IoCManager.Resolve<IResourceManager>().TryContentFileRead(new ResPath(path), out var stream))
        {
            using (stream)
                chain = Build(Image.Load<Rgba32>(stream), path);
        }

        Chains[path] = chain;
        return chain;
    }

    private static Texture[] Build(Image<Rgba32> image, string path)
    {
        var clyde = IoCManager.Resolve<IClyde>();
        var levels = new List<Texture>();

        while (true)
        {
            var last = Math.Min(image.Width, image.Height) / 2 < SmallestLevel;
            if (last || Math.Max(image.Width, image.Height) <= LargestLevel)
                levels.Add(clyde.LoadTextureFromImage(image, path, Linear));

            if (last)
                break;

            var width = image.Width / 2;
            var height = image.Height / 2;
            var next = image.Clone(x => x.Resize(width, height));
            image.Dispose();
            image = next;
        }

        image.Dispose();
        return levels.ToArray();
    }
}
