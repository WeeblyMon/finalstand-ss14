using Content.Shared._FinalStand.Weapons;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Weapons;

public sealed partial class FSWeaponRechargeAudioSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedBatterySystem _battery = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FSWeaponRechargeAudioComponent, BatteryComponent, BatteryAmmoProviderComponent, BatterySelfRechargerComponent>();
        while (query.MoveNext(out var uid, out var audioComp, out var battery, out var ammoProvider, out var recharger))
        {
            var currentCharge = _battery.GetCharge((uid, battery));
            var isEmpty = currentCharge < ammoProvider.FireCost;

            if (!audioComp.WasEmpty && isEmpty)
            {
                // Battery just hit empty — apply the punishment delay and play the SFX.
                _battery.SetChargeCooldown((uid, recharger), audioComp.EmptyPenaltyDuration);
                _audio.PlayPvs(audioComp.RechargeStartSound, uid);
            }

            audioComp.WasEmpty = isEmpty;
        }
    }
}
