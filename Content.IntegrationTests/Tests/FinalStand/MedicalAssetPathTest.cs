#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.FinalStand;

[TestFixture]
public sealed class MedicalAssetPathTest : GameTest
{
    private static readonly string[] SourceRoots =
    [
        "Content.Server/_FinalStand",
        "Content.Shared/_FinalStand",
        "Content.Client/_FinalStand",
    ];

    private static readonly Regex AssetLiteral =
        new(@"""(/(?:Audio|Textures)/[^""]+\.(?:ogg|wav|png|rsi))""", RegexOptions.Compiled);

    [Test]
    public async Task EveryReferencedAssetExists()
    {
        var server = Pair.Server;
        var resources = server.ResolveDependency<IResourceManager>();

        var repo = FindRepoRoot();
        Assert.That(repo, Is.Not.Null, "Could not locate the repository root from the test working directory.");

        var missing = new List<string>();

        await server.WaitAssertion(() =>
        {
            foreach (var root in SourceRoots)
            {
                var dir = Path.Combine(repo!, root);
                if (!Directory.Exists(dir))
                    continue;

                foreach (var file in Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
                {
                    foreach (Match match in AssetLiteral.Matches(File.ReadAllText(file)))
                    {
                        var path = match.Groups[1].Value;

                        if (path.Contains('{'))
                            continue;

                        var probe = path.EndsWith(".rsi") ? path + "/meta.json" : path;

                        if (resources.ContentFileExists(new ResPath(probe)))
                            continue;

                        missing.Add($"{Path.GetFileName(file)} -> {path}");
                    }
                }
            }

            Assert.That(missing, Is.Empty,
                "Asset paths referenced in _FinalStand code do not exist:\n" + string.Join("\n", missing));
        });
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SpaceStation14.slnx")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }
}
