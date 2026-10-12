using Content.Shared._FinalStand.Titles;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.Onboarding;

public sealed partial class FSPlayerBadgeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSPlayerBadgeComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<FSPlayerBadgeComponent> ent, ref ExaminedEvent args)
    {
        using (args.PushGroup(nameof(FSPlayerBadgeComponent)))
        {
            if (ent.Comp.Title is { } titleId && _proto.TryIndex(titleId, out var title))
            {
                args.PushMarkup(Loc.GetString("fs-badge-examine-title",
                    ("color", title.Color.ToHex()),
                    ("title", Loc.GetString(title.Name))));
            }

            if (ent.Comp.Rookie)
                args.PushMarkup(Loc.GetString("fs-badge-examine-rookie"));
        }
    }

    public void SetBadge(EntityUid body, bool rookie, ProtoId<FSTitlePrototype>? title)
    {
        var badge = EnsureComp<FSPlayerBadgeComponent>(body);
        if (badge.Rookie == rookie && badge.Title == title)
            return;

        badge.Rookie = rookie;
        badge.Title = title;
        Dirty(body, badge);
    }
}
