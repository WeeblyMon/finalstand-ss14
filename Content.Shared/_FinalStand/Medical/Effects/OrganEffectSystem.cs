// Applies an organ's granted components to its body, refreshing them so removal can't be missed.

using Content.Shared.Body;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Medical.Effects;

public sealed partial class OrganEffectSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISerializationManager _serManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganEffectComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<OrganEffectComponent, OrganGotRemovedEvent>(OnRemoved);
    }

    // Two organs can grant the same component, and we cannot tell which one added it, so active
    // effects are periodically reapplied rather than tracked by source.
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<OrganEffectComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var effect, out var organ))
        {
            if (now < effect.NextUpdate || effect.Active.Count == 0 || organ.Body is not { } body)
                continue;

            effect.NextUpdate = now + effect.Delay;
            AddComponents(body, (uid, effect), effect.Active);
        }
    }

    private void OnInserted(Entity<OrganEffectComponent> ent, ref OrganGotInsertedEvent args)
    {
        if (ent.Comp.OnAdd is { } onAdd)
            AddComponents(args.Target, ent, onAdd);

        if (ent.Comp.OnRemove is { } onRemove)
            RemoveComponents(args.Target, ent, onRemove);
    }

    private void OnRemoved(Entity<OrganEffectComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (ent.Comp.OnAdd is { } onAdd)
            RemoveComponents(args.Target, ent, onAdd);

        if (ent.Comp.OnRemove is { } onRemove)
            AddComponents(args.Target, ent, onRemove);
    }

    public void AddComponents(EntityUid body, Entity<OrganEffectComponent> organ, ComponentRegistry registry)
    {
        foreach (var (key, entry) in registry)
        {
            organ.Comp.Active[key] = entry;

            if (HasComp(body, entry.Component.GetType()))
                continue;

            var comp = (Component) _serManager.CreateCopy(entry.Component, notNullableOverride: true);
#pragma warning disable RA0045
            EntityManager.AddComponent(body, comp, true);
#pragma warning restore RA0045
        }
    }

    public void RemoveComponents(EntityUid body, Entity<OrganEffectComponent> organ, ComponentRegistry registry)
    {
        foreach (var (key, entry) in registry)
        {
            RemComp(body, entry.Component.GetType());
            organ.Comp.Active.Remove(key);
        }
    }
}
