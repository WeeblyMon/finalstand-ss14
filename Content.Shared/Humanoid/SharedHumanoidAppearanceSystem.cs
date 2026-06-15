using System.Numerics;
using Content.Goobstation.Common.Barks;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Prototypes;

namespace Content.Shared.Humanoid;

public abstract class SharedHumanoidAppearanceSystem : EntitySystem
{
    [Dependency] private readonly GrammarSystem _grammarSystem = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public static readonly ProtoId<BarkPrototype> DefaultBarkVoice = "Alto";

    public virtual void LoadProfile(EntityUid uid, HumanoidCharacterProfile? profile, HumanoidAppearanceComponent? humanoid = null)
    {
    }

    public void SetLayerVisibility(Entity<HumanoidAppearanceComponent?> ent,
        HumanoidVisualLayers layer,
        bool visible,
        SlotFlags? source = null)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var dirty = false;
        SetLayerVisibility(ent!, layer, visible, source, ref dirty);
        if (dirty)
            Dirty(ent);
    }

    public void SetLayersVisibility(Entity<HumanoidAppearanceComponent?> ent,
        IEnumerable<HumanoidVisualLayers> layers,
        bool visible)
    {
        if (!Resolve(ent.Owner, ref ent.Comp, false))
            return;

        var dirty = false;

        foreach (var layer in layers)
        {
            SetLayerVisibility(ent!, layer, visible, null, ref dirty);
        }

        if (dirty)
            Dirty(ent);
    }

    public virtual void SetLayerVisibility(
        Entity<HumanoidAppearanceComponent> ent,
        HumanoidVisualLayers layer,
        bool visible,
        SlotFlags? source,
        ref bool dirty)
    {
        if (visible)
        {
            if (source is not {} slot)
            {
                dirty |= ent.Comp.PermanentlyHidden.Remove(layer);
            }
            else if (ent.Comp.HiddenLayers.TryGetValue(layer, out var oldSlots))
            {
                ent.Comp.HiddenLayers[layer] = ~slot & oldSlots;
                if (ent.Comp.HiddenLayers[layer] == SlotFlags.NONE)
                    ent.Comp.HiddenLayers.Remove(layer);

                dirty |= (oldSlots & slot) != 0;
            }
        }
        else
        {
            if (source is not { } slot)
            {
                dirty |= ent.Comp.PermanentlyHidden.Add(layer);
            }
            else
            {
                var oldSlots = ent.Comp.HiddenLayers.GetValueOrDefault(layer);
                ent.Comp.HiddenLayers[layer] = slot | oldSlots;
                dirty |= (oldSlots & slot) != slot;
            }
        }
    }

    public void SetSex(EntityUid uid, Sex sex, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || humanoid.Sex == sex)
            return;

        var oldSex = humanoid.Sex;
        humanoid.Sex = sex;
        var sexChangedEvent = new SexChangedEvent(oldSex, sex);
        RaiseLocalEvent(uid, ref sexChangedEvent);

        if (sync)
            Dirty(uid, humanoid);
    }

    public virtual void SetSkinColor(EntityUid uid, Color skinColor, bool sync = true, bool verify = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        humanoid.SkinColor = skinColor;

        if (sync)
            Dirty(uid, humanoid);
    }

    public void SetGender(EntityUid uid, Gender gender, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid) || humanoid.Gender == gender)
            return;

        humanoid.Gender = gender;

        if (sync)
            Dirty(uid, humanoid);
    }

    public void SetBaseLayerId(EntityUid uid, HumanoidVisualLayers layer, string? id, bool sync = true,
        HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        if (humanoid.CustomBaseLayers.TryGetValue(layer, out var info))
            humanoid.CustomBaseLayers[layer] = info with { Id = id };
        else
            humanoid.CustomBaseLayers[layer] = new(id);

        if (sync)
            Dirty(uid, humanoid);
    }

    public void SetBaseLayerColor(EntityUid uid, HumanoidVisualLayers layer, Color? color, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        if (humanoid.CustomBaseLayers.TryGetValue(layer, out var info))
            humanoid.CustomBaseLayers[layer] = info with { Color = color };
        else
            humanoid.CustomBaseLayers[layer] = new(null, color);

        if (sync)
            Dirty(uid, humanoid);
    }

    public void SetScale(EntityUid uid, Vector2 scale, bool sync = true, HumanoidAppearanceComponent? humanoid = null)
    {
        if (!Resolve(uid, ref humanoid))
            return;

        humanoid.Height = scale.Y;
        humanoid.Width = scale.X;

        if (sync)
            Dirty(uid, humanoid);
    }

    public void CloneAppearance(EntityUid source, EntityUid target,
        HumanoidAppearanceComponent? sourceHumanoid = null,
        HumanoidAppearanceComponent? targetHumanoid = null)
    {
        if (!Resolve(source, ref sourceHumanoid, false) || !Resolve(target, ref targetHumanoid, false))
            return;

        targetHumanoid.SkinColor = sourceHumanoid.SkinColor;
        targetHumanoid.EyeColor = sourceHumanoid.EyeColor;
        targetHumanoid.Age = sourceHumanoid.Age;
        targetHumanoid.Height = sourceHumanoid.Height;
        targetHumanoid.Width = sourceHumanoid.Width;
        SetSex(target, sourceHumanoid.Sex, false, targetHumanoid);
        targetHumanoid.CustomBaseLayers = new(sourceHumanoid.CustomBaseLayers);

        targetHumanoid.Gender = sourceHumanoid.Gender;

        if (TryComp<GrammarComponent>(target, out var grammar))
            _grammarSystem.SetGender((target, grammar), sourceHumanoid.Gender);

        Dirty(target, targetHumanoid);
    }

    public void AddMarking(EntityUid uid, string marking, Color? color = null, bool sync = true, bool forced = false, HumanoidAppearanceComponent? humanoid = null)
    {
    }

    public void AddMarking(EntityUid uid, string marking, IReadOnlyList<Color> colors, bool sync = true, bool forced = false, HumanoidAppearanceComponent? humanoid = null)
    {
    }

    public void SetBarkVoice(EntityUid uid, string? barkvoiceId, HumanoidAppearanceComponent humanoid)
    {
        var voicePrototypeId = DefaultBarkVoice;

        if (barkvoiceId != null &&
            _proto.TryIndex<BarkPrototype>(barkvoiceId, out var bark))
        {
            voicePrototypeId = barkvoiceId;
        }

        EnsureComp<SpeechSynthesisComponent>(uid, out var comp);
        comp.VoicePrototypeId = voicePrototypeId;
        humanoid.BarkVoice = voicePrototypeId;
        Dirty(uid, comp);
    }

    public string GetSpeciesRepresentation(string speciesId)
    {
        if (_proto.TryIndex<SpeciesPrototype>(speciesId, out var species))
            return Loc.GetString(species.Name);

        Log.Error("Tried to get representation of unknown species: {speciesId}");
        return Loc.GetString("humanoid-appearance-component-unknown-species");
    }

    public string GetAgeRepresentation(string species, int age)
    {
        if (!_proto.TryIndex<SpeciesPrototype>(species, out var speciesPrototype))
        {
            Log.Error("Tried to get age representation of species that couldn't be indexed: " + species);
            return Loc.GetString("identity-age-young");
        }

        if (age < speciesPrototype.YoungAge)
            return Loc.GetString("identity-age-young");

        if (age < speciesPrototype.OldAge)
            return Loc.GetString("identity-age-middle-aged");

        return Loc.GetString("identity-age-old");
    }
}
