// SPDX-FileCopyrightText: 2022 DrSmugleaf <DrSmugleaf@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Flipp Syder <76629141+vulppine@users.noreply.github.com>
// SPDX-FileCopyrightText: 2022 Jezithyr <Jezithyr@gmail.com>
// SPDX-FileCopyrightText: 2023 Leon Friedrich <60421075+ElectroJr@users.noreply.github.com>
// SPDX-FileCopyrightText: 2023 Visne <39844191+Visne@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 ScyronX <166930367+ScyronX@users.noreply.github.com>
// SPDX-FileCopyrightText: 2024 gluesniffler <159397573+gluesniffler@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 Spatison <137375981+Spatison@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 kurokoTurbo <92106367+kurokoTurbo@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Trest <144359854+trest100@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
// SPDX-FileCopyrightText: 2025 Kayzel <43700376+KayzelW@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._FinalStand.Medical;
using Content.Shared.Body;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid
{
    public static class HumanoidVisualLayersExtension
    {
        public static bool HasSexMorph(HumanoidVisualLayers layer)
        {
            return layer switch
            {
                HumanoidVisualLayers.Groin => true,
                HumanoidVisualLayers.Chest => true,
                HumanoidVisualLayers.Head => true,
                _ => false
            };
        }

        public static string GetSexMorph(HumanoidVisualLayers layer, Sex sex, string id)
        {
            if (!HasSexMorph(layer) || sex == Sex.Unsexed)
                return id;

            return $"{id}{sex}";
        }

        /// <summary>
        ///     Sublayers. Any other layers that may visually depend on this layer existing.
        ///     For example, the head has layers such as eyes, hair, etc. depending on it.
        /// </summary>
        /// <param name="layer"></param>
        /// <returns>Enumerable of layers that depend on that given layer. Empty, otherwise.</returns>
        /// <remarks>This could eventually be replaced by a body system implementation.</remarks>
        public static IEnumerable<HumanoidVisualLayers> Sublayers(HumanoidVisualLayers layer)
        {
            switch (layer)
            {
                case HumanoidVisualLayers.Head:
                    yield return HumanoidVisualLayers.Head;
                    yield return HumanoidVisualLayers.Eyes;
                    yield return HumanoidVisualLayers.HeadSide;
                    yield return HumanoidVisualLayers.HeadTop;
                    yield return HumanoidVisualLayers.Hair;
                    yield return HumanoidVisualLayers.FacialHair;
                    yield return HumanoidVisualLayers.Snout;
                    break;
                case HumanoidVisualLayers.LArm:
                    yield return HumanoidVisualLayers.LArm;
                    yield return HumanoidVisualLayers.LHand;
                    break;
                case HumanoidVisualLayers.RArm:
                    yield return HumanoidVisualLayers.RArm;
                    yield return HumanoidVisualLayers.RHand;
                    break;
                case HumanoidVisualLayers.LLeg:
                    yield return HumanoidVisualLayers.LLeg;
                    yield return HumanoidVisualLayers.LFoot;
                    break;
                case HumanoidVisualLayers.RLeg:
                    yield return HumanoidVisualLayers.RLeg;
                    yield return HumanoidVisualLayers.RFoot;
                    break;
                case HumanoidVisualLayers.Chest:
                    yield return HumanoidVisualLayers.Chest;
                    yield return HumanoidVisualLayers.Wings; // for IPC wings port from SimpleStation
                    yield return HumanoidVisualLayers.Tail;
                // Shitmed Change Start
                    yield return HumanoidVisualLayers.Groin;
                    break;
                case HumanoidVisualLayers.Groin:
                    yield return HumanoidVisualLayers.Groin;
                    yield return HumanoidVisualLayers.Tail;
                    break;
                case HumanoidVisualLayers.LHand:
                    yield return HumanoidVisualLayers.LHand;
                    break;
                case HumanoidVisualLayers.RHand:
                    yield return HumanoidVisualLayers.RHand;
                    break;
                case HumanoidVisualLayers.LFoot:
                    yield return HumanoidVisualLayers.LFoot;
                    break;
                case HumanoidVisualLayers.RFoot:
                    yield return HumanoidVisualLayers.RFoot;
                    break;
                // Shitmed Change End
                default:
                    yield break;
            }
        }

        private static readonly Dictionary<ProtoId<OrganCategoryPrototype>, HumanoidVisualLayers> CategoryLayers = new()
        {
            // Head uses the Sublayers method to hide the rest of the face parts.
            [OrganCategories.Torso] = HumanoidVisualLayers.Chest,
            [OrganCategories.Groin] = HumanoidVisualLayers.Groin,
            [OrganCategories.Tail] = HumanoidVisualLayers.Tail,
            [OrganCategories.Head] = HumanoidVisualLayers.Head,
            [OrganCategories.ArmLeft] = HumanoidVisualLayers.LArm,
            [OrganCategories.ArmRight] = HumanoidVisualLayers.RArm,
            [OrganCategories.HandLeft] = HumanoidVisualLayers.LHand,
            [OrganCategories.HandRight] = HumanoidVisualLayers.RHand,
            [OrganCategories.LegLeft] = HumanoidVisualLayers.LLeg,
            [OrganCategories.LegRight] = HumanoidVisualLayers.RLeg,
            [OrganCategories.FootLeft] = HumanoidVisualLayers.LFoot,
            [OrganCategories.FootRight] = HumanoidVisualLayers.RFoot,
        };

        public static HumanoidVisualLayers? ToHumanoidLayers(this OrganComponent organ)
        {
            return organ.Category is { } category && CategoryLayers.TryGetValue(category, out var layer)
                ? layer
                : null;
        }
    }
}