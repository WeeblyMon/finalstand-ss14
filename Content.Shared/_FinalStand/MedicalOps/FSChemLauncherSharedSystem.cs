using System.Diagnostics.CodeAnalysis;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Prototypes;

namespace Content.Shared._FinalStand.MedicalOps;

// The launcher is its own ammo provider: every shot spends reserve and spawns the active formula's canister.
public sealed partial class FSChemLauncherSharedSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FSChemLauncherComponent, TakeAmmoEvent>(OnTakeAmmo);
        SubscribeLocalEvent<FSChemLauncherComponent, GetAmmoCountEvent>(OnAmmoCount);
    }

    public bool TryGetActive(FSChemLauncherComponent launcher, [NotNullWhen(true)] out FSChemFormulaPrototype? formula)
    {
        formula = null;
        return launcher.Selected >= 0
               && launcher.Selected < launcher.Slots.Count
               && _prototypes.TryIndex(launcher.Slots[launcher.Selected], out formula);
    }

    private void OnTakeAmmo(Entity<FSChemLauncherComponent> ent, ref TakeAmmoEvent args)
    {
        if (!TryGetActive(ent.Comp, out var formula))
        {
            args.Reason = Loc.GetString("fs-chem-launcher-no-formula");
            return;
        }

        for (var i = 0; i < args.Shots; i++)
        {
            if (ent.Comp.Reserve < formula.Cost)
            {
                args.Reason = Loc.GetString("fs-chem-launcher-dry");
                break;
            }

            ent.Comp.Reserve -= formula.Cost;

            var canister = SpawnAtPosition(formula.Canister, args.Coordinates);
            var comp = EnsureComp<FSChemCanisterComponent>(canister);
            comp.Launcher = ent.Owner;

            if (ent.Comp.CatalystArmed)
            {
                comp.Catalysed = true;
                ent.Comp.CatalystArmed = false;
                ent.Comp.Catalyst = 0f;
            }

            args.Ammo.Add((canister, EnsureComp<AmmoComponent>(canister)));
        }

        Dirty(ent);
    }

    private void OnAmmoCount(Entity<FSChemLauncherComponent> ent, ref GetAmmoCountEvent args)
    {
        if (!TryGetActive(ent.Comp, out var formula) || formula.Cost <= 0)
            return;

        args.Count = (int) (ent.Comp.Reserve / formula.Cost);
        args.Capacity = (int) (ent.Comp.MaxReserve / formula.Cost);
    }
}
