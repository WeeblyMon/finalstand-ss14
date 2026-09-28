using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.MedicalOps;

// UberCharger: healing builds charge, the action spends it on a short window of invulnerability.
public sealed partial class FSUberSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    public const float FullCharge = 100f;

    private static readonly SoundSpecifier ReadySound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");
    private static readonly SoundSpecifier DeploySound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
    private static readonly SoundSpecifier EndSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSMediGunComponent, FSMediGunUberActionEvent>(OnUberAction);
    }

    public void AddCharge(Entity<FSMediGunComponent> gun, float amount)
    {
        var comp = gun.Comp;
        if (comp.Variant != FSMediGunVariant.UberCharger || comp.UberActive || amount <= 0f)
            return;

        var before = comp.UberCharge;
        comp.UberCharge = MathF.Min(FullCharge, comp.UberCharge + amount);
        if ((int) comp.UberCharge == (int) before)
            return;

        Dirty(gun);

        if (before < FullCharge && comp.UberCharge >= FullCharge && comp.ParentEntity is { } medic)
        {
            _audio.PlayEntity(ReadySound, medic, medic);
            _popup.PopupEntity(Loc.GetString("fs-uber-ready"), medic, medic, PopupType.Medium);
        }
    }

    public void Cover(Entity<FSMediGunComponent> gun, EntityUid patient)
    {
        if (gun.Comp.UberEndTime is not { } end)
            return;

        var ubered = EnsureComp<FSUberedComponent>(patient);
        ubered.EndTime = end;
        ubered.SourceColor = gun.Comp.BeamColor;
        ubered.InfiniteStamina = gun.Comp.UberInfiniteStamina;
        Dirty(patient, ubered);
    }

    private void OnUberAction(Entity<FSMediGunComponent> ent, ref FSMediGunUberActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var comp = ent.Comp;

        if (comp.UberActive)
            return;

        if (comp.UberCharge < FullCharge)
        {
            _popup.PopupEntity(Loc.GetString("fs-uber-not-ready", ("charge", (int) comp.UberCharge)), args.Performer, args.Performer);
            return;
        }

        comp.UberEndTime = _timing.CurTime + TimeSpan.FromSeconds(comp.UberDuration);
        Dirty(ent);

        Cover(ent, args.Performer);
        foreach (var patient in comp.HealedEntities)
            Cover(ent, patient);

        _audio.PlayPvs(DeploySound, args.Performer);
        _popup.PopupEntity(Loc.GetString("fs-uber-deployed"), args.Performer, PopupType.Large);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        var guns = EntityQueryEnumerator<FSMediGunComponent>();
        while (guns.MoveNext(out var uid, out var gun))
        {
            if (gun.UberEndTime is not { } end)
                continue;

            if (now >= end)
            {
                gun.UberEndTime = null;
                gun.UberCharge = 0f;
                Dirty(uid, gun);
                if (gun.ParentEntity is { } medic)
                    _audio.PlayEntity(EndSound, medic, medic);
                continue;
            }

            var remaining = (float) ((end - now).TotalSeconds / gun.UberDuration) * FullCharge;
            if ((int) remaining == (int) gun.UberCharge)
                continue;

            gun.UberCharge = remaining;
            Dirty(uid, gun);
        }

        var ubered = EntityQueryEnumerator<FSUberedComponent>();
        while (ubered.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.EndTime)
                RemCompDeferred<FSUberedComponent>(uid);
        }
    }
}
