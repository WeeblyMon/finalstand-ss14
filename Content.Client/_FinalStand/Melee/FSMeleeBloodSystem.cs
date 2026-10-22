using Content.Client.Items.Systems;
using Content.Shared._FinalStand.Melee;
using Content.Shared.Hands;
using Content.Shared.Item;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Melee;

// Draws a weapon's kills as blood over its sprite, on the floor and in hand.
public sealed class FSMeleeBloodSystem : EntitySystem
{
    [Dependency] private SharedItemSystem _item = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    private const string Layer = "fs-melee-blood";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSMeleeBloodComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<FSMeleeBloodComponent, GetInhandVisualsEvent>(OnInhand, after: [typeof(ItemSystem)]);
    }

    private static string Shader(int stage) => $"FSBloodStain{stage}";

    private void OnState(Entity<FSMeleeBloodComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
        {
            var entity = (ent.Owner, sprite);
            _sprite.RemoveLayer(entity, Layer, false);

            var stage = ent.Comp.Stage;
            if (stage > 0 && _sprite.TryGetLayer(entity, 0, out var layer, false) && layer.ActualRsi is { } rsi)
            {
                var index = _sprite.AddLayer(entity, new PrototypeLayerData
                {
                    RsiPath = rsi.Path.ToString(),
                    State = layer.State.Name,
                    Shader = Shader(stage),
                }, null);
                _sprite.LayerMapSet(entity, Layer, index);
            }
        }

        _item.VisualsChanged(ent);
    }

    private void OnInhand(EntityUid uid, FSMeleeBloodComponent blood, GetInhandVisualsEvent args)
    {
        var stage = blood.Stage;
        if (stage == 0)
            return;

        var count = args.Layers.Count;
        for (var i = 0; i < count; i++)
        {
            var (key, layer) = args.Layers[i];
            if (layer.State == null)
                continue;

            args.Layers.Add(($"{key}-{Layer}", new PrototypeLayerData
            {
                RsiPath = layer.RsiPath,
                State = layer.State,
                Offset = layer.Offset,
                Scale = layer.Scale,
                Rotation = layer.Rotation,
                Shader = Shader(stage),
            }));
        }
    }
}
