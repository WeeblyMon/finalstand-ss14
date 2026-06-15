using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;

namespace Content.Server.Humanoid;

public sealed partial class HumanoidAppearanceSystem : SharedHumanoidAppearanceSystem
{
    [Dependency] private readonly MarkingManager _markingManager = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public void RemoveMarking(EntityUid uid, string marking, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
    }

    public void SetMarkingId(EntityUid uid, MarkingCategories category, int index, string markingId, HumanoidAppearanceComponent? humanoid = null)
    {
    }

    public void SetMarkingColor(EntityUid uid, MarkingCategories category, int index, List<Color> colors,
        HumanoidAppearanceComponent? humanoid = null)
    {
    }
}
