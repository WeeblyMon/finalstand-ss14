// Draws a combat mech's mounted guns on its body: the heavy weapon on the shoulder, the rest in the right then left hand.
// Each mount has a layer over the mech and one under it, so whatever sits behind the body in a view is hidden by it.
using Content.Shared._FinalStand.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Tag;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._FinalStand.Mech;

public sealed partial class FSMechWeaponVisualSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private TagSystem _tags = default!;

    private static readonly ResPath Rsi = new("/Textures/_FinalStand/Objects/Mech/broadsword_weapons.rsi");
    private static readonly ProtoId<TagPrototype> Heavy = "HeavyMechMount";
    private static readonly string[] Mounts = { "right", "left", "back" };

    private readonly Dictionary<EntityUid, string> _shown = new();
    private readonly string?[] _states = new string?[3];
    private readonly List<EntityUid> _gone = new();

    public override void FrameUpdate(float frameTime)
    {
        var query = EntityQueryEnumerator<FSCombatMechComponent, MechComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out _, out var mech, out var sprite))
        {
            Assign(mech);
            var key = string.Join('|', _states);
            if (_shown.TryGetValue(uid, out var last) && last == key)
                continue;

            _shown[uid] = key;
            for (var i = 0; i < Mounts.Length; i++)
            {
                var state = _states[i] is { } s ? $"{s}_{Mounts[i]}" : null;
                Show((uid, sprite), $"fs-mount-{Mounts[i]}", state, false);
                Show((uid, sprite), $"fs-mount-{Mounts[i]}-far", state == null ? null : state + "_far", true);
            }
        }

        foreach (var uid in _shown.Keys)
        {
            if (TerminatingOrDeleted(uid))
                _gone.Add(uid);
        }

        foreach (var uid in _gone)
            _shown.Remove(uid);
        _gone.Clear();
    }

    private void Assign(MechComponent mech)
    {
        Array.Clear(_states);
        foreach (var item in mech.EquipmentContainer.ContainedEntities)
        {
            if (!TryComp<FSMechMountVisualComponent>(item, out var visual))
                continue;

            var slot = _tags.HasTag(item, Heavy) ? 2 : _states[0] == null ? 0 : _states[1] == null ? 1 : 2;
            if (_states[slot] == null)
                _states[slot] = visual.State;
        }
    }

    private void Show(Entity<SpriteComponent> mech, string key, string? rsiState, bool under)
    {
        if (!_sprite.LayerMapTryGet(mech.AsNullable(), key, out var layer, false))
        {
            if (rsiState == null)
                return;

            layer = _sprite.AddRsiLayer(mech.AsNullable(), rsiState, Rsi, under ? 0 : null);
            _sprite.LayerMapSet(mech.AsNullable(), key, layer);
        }

        var rsi = _sprite.LayerGetEffectiveRsi(mech.AsNullable(), layer);
        var visible = rsiState != null && rsi != null && rsi.TryGetState(rsiState, out _);
        _sprite.LayerSetVisible(mech.AsNullable(), layer, visible);
        if (visible)
            _sprite.LayerSetRsiState(mech.AsNullable(), layer, rsiState!);
    }
}
