// Plays a mech gun's reload or recharge sound at the mech, so the pilot and anyone nearby hear when it's coming back.
using Content.Shared._FinalStand.Mech;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Mech;

public sealed partial class FSMechCycleSoundSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    private static readonly TimeSpan RealCooldown = TimeSpan.FromSeconds(0.5);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMechCycleSoundComponent, AmmoShotEvent>(OnShot);
    }

    private void OnShot(Entity<FSMechCycleSoundComponent> ent, ref AmmoShotEvent args)
    {
        var now = _timing.CurTime;
        if (ent.Comp.Delay is { } delay)
        {
            ent.Comp.PlayAt = now + TimeSpan.FromSeconds(delay);
            return;
        }

        ent.Comp.PlayAt = TryComp<GunComponent>(ent, out var gun) && gun.NextFire - now >= RealCooldown ? gun.NextFire : null;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<FSMechCycleSoundComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.PlayAt is not { } at || now < at)
                continue;

            comp.PlayAt = null;
            var source = _container.TryGetContainingContainer(uid, out var container) ? container.Owner : uid;
            _audio.PlayPvs(comp.Sound, source);
        }
    }
}
