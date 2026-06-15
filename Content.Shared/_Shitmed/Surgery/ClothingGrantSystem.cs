// SPDX-FileCopyrightText: 2025 Goob-Station contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Clothing;

namespace Content.Shared._Shitmed.Surgery;

/// <summary>
/// Grants and revokes components on a wearer based on <see cref="ClothingGrantComponent"/>.
/// Used so surgical gloves can grant SurgeryIgnoreClothing while worn.
/// </summary>
public sealed class ClothingGrantSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClothingGrantComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ClothingGrantComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(EntityUid uid, ClothingGrantComponent comp, ref ClothingGotEquippedEvent args)
    {
        if (comp.Component == null)
            return;

        foreach (var entry in comp.Component)
            EntityManager.AddComponent(args.Wearer, entry.Value, true);
    }

    private void OnUnequipped(EntityUid uid, ClothingGrantComponent comp, ref ClothingGotUnequippedEvent args)
    {
        if (comp.Component == null)
            return;

        foreach (var entry in comp.Component)
        {
            var type = entry.Value.GetType();
            if (HasComp(args.Wearer, type))
                RemComp(args.Wearer, type);
        }
    }
}
