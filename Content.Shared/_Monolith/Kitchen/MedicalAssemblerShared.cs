// SPDX-FileCopyrightText: 2025 Monolith-Station contributors, Final Stand contributors
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Dictionary;

namespace Content.Shared._Monolith.Kitchen;

// ── Recipe prototype ──────────────────────────────────────────────────────────

[Prototype]
public sealed partial class MedicalAssemblerRecipePrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("name")]
    private string _name = string.Empty;

    [DataField("reagents", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<FixedPoint2, ReagentPrototype>))]
    public Dictionary<string, FixedPoint2> IngredientsReagents = new();

    [DataField("solids", customTypeSerializer: typeof(PrototypeIdDictionarySerializer<FixedPoint2, EntityPrototype>))]
    public Dictionary<string, FixedPoint2> IngredientsSolids = new();

    [DataField("result", customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string Result { get; private set; } = string.Empty;

    [DataField("resultCount")]
    public int ResultCount { get; private set; } = 1;

    [DataField("time")]
    public float AssembleTime { get; private set; } = 5f;

    public string Name => Loc.GetString(_name);
}

// ── Component ─────────────────────────────────────────────────────────────────

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedicalAssemblerComponent : Component
{
    [DataField]
    public string ContainerId = "medical_assembler_container";

    [DataField, AutoNetworkedField]
    public int Capacity = 5;

    [DataField, AutoNetworkedField]
    public bool IsBusy;

    [DataField, AutoNetworkedField]
    public TimeSpan CurrentAssembleTimeEnd;

    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Machines/microwave_start_beep.ogg");

    [DataField]
    public SoundSpecifier DoneSound = new SoundPathSpecifier("/Audio/Machines/microwave_done_beep.ogg");

    [DataField]
    public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");

    [ViewVariables]
    public Container Storage = default!;

    public EntityUid? PlayingStream;
}

[RegisterComponent]
public sealed partial class ActiveMedicalAssemblerComponent : Component
{
    public float TimeRemaining;
    public MedicalAssemblerRecipePrototype? Recipe;
}

// ── BUI messages ──────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class MedicalAssemblerStartMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class MedicalAssemblerEjectMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable]
public sealed class MedicalAssemblerEjectSolidMessage : BoundUserInterfaceMessage
{
    public NetEntity EntityId;
    public MedicalAssemblerEjectSolidMessage(NetEntity entityId) { EntityId = entityId; }
}

// ── BUI state ─────────────────────────────────────────────────────────────────

[Serializable, NetSerializable]
public sealed class MedicalAssemblerUpdateUserInterfaceState : BoundUserInterfaceState
{
    public NetEntity[] ContainedSolids;
    public bool IsBusy;
    public TimeSpan CurrentAssembleTimeEnd;

    public MedicalAssemblerUpdateUserInterfaceState(
        NetEntity[] containedSolids,
        bool isBusy,
        TimeSpan currentAssembleTimeEnd)
    {
        ContainedSolids = containedSolids;
        IsBusy = isBusy;
        CurrentAssembleTimeEnd = currentAssembleTimeEnd;
    }
}

// ── Keys / enums ──────────────────────────────────────────────────────────────

[NetSerializable, Serializable]
public enum MedicalAssemblerUiKey { Key }

[NetSerializable, Serializable]
public enum MedicalAssemblerVisualState
{
    Idle,
    Assembling,
    Broken,
}
