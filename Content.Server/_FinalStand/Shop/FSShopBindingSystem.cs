using Content.Shared.Examine;
using Content.Shared.Mind;

namespace Content.Server._FinalStand.Shop;

public sealed partial class FSShopBindingSystem : EntitySystem
{
    [Dependency] private SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSShopBoundComponent, ExaminedEvent>(OnExamined);
    }

    public void Bind(EntityUid item, EntityUid ownerMind)
    {
        EnsureComp<FSShopBoundComponent>(item).OwnerMind = ownerMind;
    }

    public bool IsUsableBy(EntityUid item, EntityUid user)
    {
        if (!TryComp<FSShopBoundComponent>(item, out var bound))
            return true;

        return _mind.TryGetMind(user, out var mindId, out _) && mindId == bound.OwnerMind;
    }

    public bool OwnsAny(EntityUid ownerMind, IReadOnlySet<string> protoIds)
    {
        var query = EntityQueryEnumerator<FSShopBoundComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var bound, out var meta))
        {
            if (bound.OwnerMind == ownerMind
                && meta.EntityPrototype is { } proto
                && protoIds.Contains(proto.ID)
                && !TerminatingOrDeleted(uid))
                return true;
        }

        return false;
    }

    private void OnExamined(Entity<FSShopBoundComponent> ent, ref ExaminedEvent args)
    {
        var mine = _mind.TryGetMind(args.Examiner, out var mindId, out _) && mindId == ent.Comp.OwnerMind;
        if (mine)
        {
            args.PushMarkup(Loc.GetString("fs-shop-bound-examine-yours"));
            return;
        }

        var name = TryComp<MindComponent>(ent.Comp.OwnerMind, out var mind) && mind.CharacterName is { } character
            ? character
            : Loc.GetString("fs-shop-bound-someone");
        args.PushMarkup(Loc.GetString("fs-shop-bound-examine-other", ("owner", name)));
    }
}
