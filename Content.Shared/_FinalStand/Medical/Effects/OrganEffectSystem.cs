using Content.Shared.Body;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.Medical.Effects;

// Refreshes granted components rather than tracking them, so a removal can't be missed.
public sealed partial class OrganEffectSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ISerializationManager _serManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganEffectComponent, OrganGotInsertedEvent>(OnInserted);
        SubscribeLocalEvent<OrganEffectComponent, OrganGotRemovedEvent>(OnRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // The refresh is a server-side safety net; the client gets the granted components from state.
        if (_net.IsClient)
            return;

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<OrganEffectComponent, OrganComponent>();
        while (query.MoveNext(out var uid, out var effect, out var organ))
        {
            if (now < effect.NextUpdate
                || effect.Active.Count == 0
                || organ.Body is not { } body
                || HasComp<OrganDisabledComponent>(uid))
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
            StripComponents(args.Target, ent, onRemove);
    }

    private void OnRemoved(Entity<OrganEffectComponent> ent, ref OrganGotRemovedEvent args)
    {
        if (ent.Comp.OnAdd is { } onAdd)
            RemoveComponents(args.Target, ent, onAdd);

        if (ent.Comp.OnRemove is { } onRemove)
            RestoreComponents(args.Target, ent, onRemove);
    }

    public void AddComponents(EntityUid body, Entity<OrganEffectComponent> organ, ComponentRegistry registry)
    {
        foreach (var (key, entry) in registry)
        {
            var type = entry.Component.GetType();
            if (HasComp(body, type))
            {
                if (!organ.Comp.Active.ContainsKey(key))
                    organ.Comp.Preexisting.Add(key);

                organ.Comp.Active[key] = entry;
                continue;
            }

            organ.Comp.Active[key] = entry;
            AddCopy(body, entry.Component);
        }
    }

    public void RemoveComponents(EntityUid body, Entity<OrganEffectComponent> organ, ComponentRegistry registry)
    {
        foreach (var (key, entry) in registry)
        {
            organ.Comp.Active.Remove(key);
            if (!organ.Comp.Preexisting.Remove(key))
                RemComp(body, entry.Component.GetType());
        }
    }

    private void StripComponents(EntityUid body, Entity<OrganEffectComponent> organ, ComponentRegistry registry)
    {
        foreach (var (key, entry) in registry)
        {
            if (RemComp(body, entry.Component.GetType()))
                organ.Comp.Stripped.Add(key);
        }
    }

    private void RestoreComponents(EntityUid body, Entity<OrganEffectComponent> organ, ComponentRegistry registry)
    {
        foreach (var (key, entry) in registry)
        {
            if (organ.Comp.Stripped.Remove(key) && !HasComp(body, entry.Component.GetType()))
                AddCopy(body, entry.Component);
        }
    }

    private void AddCopy(EntityUid body, IComponent component)
    {
        var comp = (Component) _serManager.CreateCopy(component, notNullableOverride: true);
#pragma warning disable RA0045
        EntityManager.AddComponent(body, comp, true);
#pragma warning restore RA0045
    }
}
