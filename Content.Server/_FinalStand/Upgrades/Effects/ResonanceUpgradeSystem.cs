using Content.Server._FinalStand.Upgrades;
using Content.Server.Atmos.EntitySystems;
using Content.Shared._FinalStand.Shop;
using Content.Shared._FinalStand.Upgrades.Effects;
using Content.Shared.Atmos.Components;

namespace Content.Server._FinalStand.Upgrades.Effects;

// per-target hit counter; ignites on 5th hit; counter cleared on ignition or target death/deletion
public sealed partial class ResonanceUpgradeSystem : EntitySystem
{
    [Dependency] private FlammableSystem _flammable = default!;

    private readonly List<EntityUid> _staleKeys = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSProjectileHitEffectEvent>(OnHit);
    }

    private void OnHit(FSProjectileHitEffectEvent ev)
    {
        if (ev.Weapon == null)
            return;
        if (!TryComp<FSResonanceComponent>(ev.Weapon.Value, out var resonance))
            return;
        if (ev.State is not { } state || !state.ResonanceEnabled)
            return;

        if (resonance.HitCounts.Count > 30)
        {
            _staleKeys.Clear();
            foreach (var key in resonance.HitCounts.Keys)
            {
                if (!Exists(key))
                    _staleKeys.Add(key);
            }
            foreach (var key in _staleKeys)
                resonance.HitCounts.Remove(key);
        }

        resonance.HitCounts.TryGetValue(ev.Target, out var count);
        count++;

        if (count >= FSResonanceComponent.HitsToIgnite)
        {
            resonance.HitCounts.Remove(ev.Target);
            if (TryComp<FlammableComponent>(ev.Target, out var flammable))
            {
                flammable.FireStacks += 3f;
                _flammable.Ignite(ev.Target, ev.Shooter ?? ev.Target, flammable, ignitionSourceUser: ev.Shooter);
            }
        }
        else
        {
            resonance.HitCounts[ev.Target] = count;
        }
    }
}
