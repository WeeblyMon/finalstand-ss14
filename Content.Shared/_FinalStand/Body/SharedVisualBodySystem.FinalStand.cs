using System.Numerics;
using Content.Shared._FinalStand.Humanoid;
using Content.Shared.Preferences;

namespace Content.Shared.Body;

public abstract partial class SharedVisualBodySystem
{
    [Dependency] private FSBodySizeSystem _bodySize = default!;

    private void ApplyBodySize(EntityUid uid, HumanoidCharacterProfile profile)
    {
        if (!ProtoMan.TryIndex(profile.Species, out var species))
            return;

        var size = species.BodySize;
        _bodySize.SetSize(uid, new Vector2(size.ClampWidth(profile.Width), size.ClampHeight(profile.Height)));
    }
}
