using Content.Client.DamageState;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;

namespace Content.Client._FinalStand.Visuals;

public sealed class FSZombieVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSZombieVisualsComponent, AfterAutoHandleStateEvent>(OnStateHandled);
        SubscribeLocalEvent<FSZombieVisualsComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FSFlamethrowerComponent, AfterAutoHandleStateEvent>(OnFlamethrowerStateHandled);
    }

    private void OnStartup(EntityUid uid, FSZombieVisualsComponent comp, ComponentStartup args)
        => UpdateSprite(uid, comp);

    private void OnStateHandled(EntityUid uid, FSZombieVisualsComponent comp, AfterAutoHandleStateEvent args)
        => UpdateSprite(uid, comp);

    private void OnMobStateChanged(EntityUid uid, FSZombieVisualsComponent comp, MobStateChangedEvent args)
        => UpdateSprite(uid, comp);

    private void OnFlamethrowerStateHandled(EntityUid uid, FSFlamethrowerComponent _, AfterAutoHandleStateEvent args)
    {
        if (TryComp<FSZombieVisualsComponent>(uid, out var visuals))
            UpdateSprite(uid, visuals);
    }

    private void UpdateSprite(EntityUid uid, FSZombieVisualsComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        var isDead = TryComp<MobStateComponent>(uid, out var mobState)
                     && mobState.CurrentState == MobState.Dead;

        string state;

        if (comp.SimpleSpriteMode)
        {
            if (isDead)
                state = "dead";
            else if (TryComp<FSFlamethrowerComponent>(uid, out var ft) && ft.IsFiring)
                state = "base_lowered";
            else
                state = "base";
        }
        else if (isDead)
        {
            state = comp.DeathAlt switch
            {
                1 => "base5-alt1-dead",
                2 => "base5-alt2-dead",
                _ => "base5-dead",
            };
        }
        else
        {
            state = comp.DamageStage switch
            {
                0 => "base",
                1 => "base2",
                2 => "base3",
                3 => "base4",
                4 => comp.DeathAlt switch
                {
                    1 => "base5-alt1",
                    2 => "base5-alt2",
                    _ => "base5",
                },
                _ => "base",
            };
        }

        if (!_sprite.LayerMapTryGet((uid, sprite), DamageStateVisualLayers.Base, out _, false))
            return;

        _sprite.LayerSetRsiState((uid, sprite), DamageStateVisualLayers.Base, state);
    }
}
