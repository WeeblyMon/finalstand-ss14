using Content.Shared.Actions;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSUberSharedSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier BlockedSound = new SoundPathSpecifier("/Audio/Weapons/block_metal1.ogg");
    private static readonly TimeSpan TingInterval = TimeSpan.FromSeconds(0.2);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMediGunComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<FSUberedComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<FSUberedComponent, BeforeStaminaDamageEvent>(OnBeforeStamina);
    }

    private void OnGetActions(EntityUid uid, FSMediGunComponent comp, GetItemActionsEvent args)
    {
        if (comp.Variant == FSMediGunVariant.UberCharger && args.InHands)
            args.AddAction(ref comp.UberAction, comp.UberActionId);
    }

    private void OnBeforeDamage(Entity<FSUberedComponent> ent, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.GetTotal() <= 0 || _timing.CurTime >= ent.Comp.EndTime)
            return;

        args.Cancelled = true;

        if (!_net.IsServer || _timing.CurTime < ent.Comp.NextTing)
            return;

        ent.Comp.NextTing = _timing.CurTime + TingInterval;
        _audio.PlayPvs(BlockedSound, ent, AudioParams.Default.WithVolume(-3f).WithVariation(0.15f));
    }

    private void OnBeforeStamina(Entity<FSUberedComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (ent.Comp.InfiniteStamina && args.Value > 0 && _timing.CurTime < ent.Comp.EndTime)
            args.Cancelled = true;
    }
}
