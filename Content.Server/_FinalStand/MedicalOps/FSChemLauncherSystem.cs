using Content.Shared._FinalStand.FriendlyFire;
using Content.Shared._FinalStand.MedicalOps;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.MedicalOps;

public sealed partial class FSChemLauncherSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public const float FullCatalyst = 100f;

    private static readonly SoundSpecifier ReadySound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");
    private static readonly SoundSpecifier ArmSound = new SoundPathSpecifier("/Audio/_FinalStand/MedicalOps/buff_refresh.ogg");
    private static readonly SoundSpecifier SelectSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSChemLauncherComponent, UseInHandEvent>(OnUseInHand);
        SubscribeNetworkEvent<FSChemLauncherSelectMessage>(OnSelect);
        SubscribeNetworkEvent<FSUberActivateMessage>(OnArm);
    }

    private void OnUseInHand(Entity<FSChemLauncherComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Slots.Count < 2 || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        args.Handled = true;
        RaiseNetworkEvent(new FSChemFormulaWheelEvent(GetNetEntity(ent)), actor.PlayerSession);
    }

    private void OnSelect(FSChemLauncherSelectMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || HeldLauncher(user) is not { } launcher
            || msg.Slot < 0
            || msg.Slot >= launcher.Comp.Slots.Count
            || launcher.Comp.Selected == msg.Slot)
            return;

        launcher.Comp.Selected = msg.Slot;
        Dirty(launcher);
        _audio.PlayEntity(SelectSound, user, user, AudioParams.Default.WithVolume(-6f));
    }

    private void OnArm(FSUberActivateMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user || HeldLauncher(user) is not { } launcher)
            return;

        if (launcher.Comp.CatalystArmed)
            return;

        if (launcher.Comp.Catalyst < FullCatalyst)
        {
            _popup.PopupEntity(Loc.GetString("fs-catalyst-not-ready", ("charge", (int) launcher.Comp.Catalyst)), user, user);
            return;
        }

        launcher.Comp.CatalystArmed = true;
        Dirty(launcher);
        _audio.PlayEntity(ArmSound, user, user);
        _popup.PopupEntity(Loc.GetString("fs-catalyst-armed"), user, user, PopupType.Medium);
    }

    public Entity<FSChemLauncherComponent>? HeldLauncher(EntityUid user)
    {
        return _hands.TryGetActiveItem(user, out var held) && TryComp<FSChemLauncherComponent>(held, out var comp)
            ? (held.Value, comp)
            : null;
    }

    // The launcher in the user's hands, or failing that the first one carried in their inventory.
    public Entity<FSChemLauncherComponent>? CarriedLauncher(EntityUid user)
    {
        if (HeldLauncher(user) is { } held)
            return held;

        var query = EntityQueryEnumerator<FSChemLauncherComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (Holder(uid) == user)
                return (uid, comp);
        }

        return null;
    }

    public EntityUid? Holder(EntityUid item)
    {
        var current = item;
        for (var depth = 0; depth < 5; depth++)
        {
            if (!_containers.TryGetContainingContainer((current, null, null), out var container))
                return null;

            current = container.Owner;
            if (HasComp<FSFriendlyFireComponent>(current))
                return current;
        }

        return null;
    }

    public void AddCatalyst(EntityUid? launcher, float amount)
    {
        if (launcher is not { } uid
            || amount <= 0f
            || !TryComp<FSChemLauncherComponent>(uid, out var comp)
            || comp.CatalystArmed
            || comp.Catalyst >= FullCatalyst)
            return;

        comp.Catalyst = MathF.Min(FullCatalyst, comp.Catalyst + amount);
        Dirty(uid, comp);

        if (comp.Catalyst < FullCatalyst || Holder(uid) is not { } holder)
            return;

        _audio.PlayEntity(ReadySound, holder, holder);
        _popup.PopupEntity(Loc.GetString("fs-catalyst-ready"), holder, holder, PopupType.Medium);
    }

    public bool TrySetSlot(EntityUid user, int slot, ProtoId<FSChemFormulaPrototype> formula)
    {
        if (CarriedLauncher(user) is not { } launcher)
        {
            _popup.PopupEntity(Loc.GetString("fs-chem-launcher-none"), user, user);
            return false;
        }

        var comp = launcher.Comp;
        if (slot < 0 || slot >= comp.SlotCount || !comp.Known.Contains(formula) || !_prototypes.HasIndex(formula))
            return false;

        var existing = comp.Slots.IndexOf(formula);
        if (slot >= comp.Slots.Count)
        {
            if (existing >= 0)
                return false;

            comp.Slots.Add(formula);
        }
        else if (existing >= 0)
        {
            (comp.Slots[existing], comp.Slots[slot]) = (comp.Slots[slot], comp.Slots[existing]);
        }
        else
        {
            comp.Slots[slot] = formula;
        }

        comp.Selected = Math.Clamp(comp.Selected, 0, comp.Slots.Count - 1);
        Dirty(launcher);
        return true;
    }
}
