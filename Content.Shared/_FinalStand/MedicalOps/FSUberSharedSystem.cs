using Content.Shared._FinalStand.Ailments;
using Content.Shared.Actions;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Shared._FinalStand.MedicalOps;

public sealed partial class FSUberSharedSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    private static readonly SoundSpecifier BlockedSound = new SoundPathSpecifier("/Audio/Weapons/block_metal1.ogg");
    private static readonly TimeSpan TingInterval = TimeSpan.FromSeconds(0.2);

    // A shorter cover from another source doesn't replace a longer one still running: a passing uber beam would
    // otherwise take over a medic's own uber and cut it short when it moved on.
    public void Cover(EntityUid patient, EntityUid source, TimeSpan end, float duration, Color color, bool infiniteStamina)
    {
        if (TryComp<FSUberedComponent>(patient, out var current)
            && current.Source != source
            && current.EndTime > end
            && _timing.CurTime < current.EndTime)
        {
            return;
        }

        var ubered = EnsureComp<FSUberedComponent>(patient);
        ubered.Source = source;
        ubered.EndTime = end;
        ubered.Duration = duration;
        ubered.SourceColor = color;
        ubered.InfiniteStamina = infiniteStamina;
        Dirty(patient, ubered);
    }

    // Brings forward the end of a cover given by this source; a cover from another medic's uber is left alone.
    public void EndCover(EntityUid patient, EntityUid source, TimeSpan end)
    {
        if (!TryComp<FSUberedComponent>(patient, out var ubered) || ubered.Source != source || ubered.EndTime <= end)
            return;

        ubered.EndTime = end;
        Dirty(patient, ubered);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMediGunComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<FSUberedComponent, BeforeDamageChangedEvent>(OnBeforeDamage);
        SubscribeLocalEvent<FSUberedComponent, BeforeStaminaDamageEvent>(OnBeforeStamina);
        SubscribeLocalEvent<FSUberedComponent, FSAilmentInflictAttemptEvent>(OnInflictAttempt);
    }

    private void OnInflictAttempt(Entity<FSUberedComponent> ent, ref FSAilmentInflictAttemptEvent args)
    {
        if (_timing.CurTime < ent.Comp.EndTime)
            args.Cancelled = true;
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
        RaiseNetworkEvent(new FSUberBlockedEvent(GetNetEntity(ent)), Filter.Pvs(ent));
    }

    private void OnBeforeStamina(Entity<FSUberedComponent> ent, ref BeforeStaminaDamageEvent args)
    {
        if (ent.Comp.InfiniteStamina && args.Value > 0 && _timing.CurTime < ent.Comp.EndTime)
            args.Cancelled = true;
    }
}
