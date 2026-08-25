using Content.Client.DamageState;
using Content.Shared._FinalStand.Mobs;
using Content.Shared._FinalStand.Visuals;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._FinalStand.Visuals;

public sealed partial class FSZombieVisualizerSystem : EntitySystem
{
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;

    private const string TeslaGlowShader      = "FSTeslaGlow";
    private const string FlamerGlowShader     = "FSFlamerGlow";
    private const string ArmourGlowShader     = "FSArmourGlow";
    private const string DevastatorGlowShader = "FSDevastatorGlow";

    private static readonly Color DevastatorFullHealth = Color.White;
    private static readonly Color DevastatorNearDeath  = new(1f, 0.05f, 0.05f);

    private readonly Dictionary<string, ShaderInstance> _glows = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSZombieVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<FSZombieVisualsComponent, AfterAutoHandleStateEvent>(OnStateHandled);
        SubscribeLocalEvent<FSZombieVisualsComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<FSFlamethrowerComponent, AfterAutoHandleStateEvent>(OnFlamethrowerStateHandled);
        SubscribeLocalEvent<FSTeslaZombieComponent, AfterAutoHandleStateEvent>(OnTeslaStateHandled);
        SubscribeLocalEvent<FSArmouredDeflectComponent, AfterAutoHandleStateEvent>(OnArmouredDeflectStateHandled);
        SubscribeLocalEvent<FSDevastatorComponent, ComponentStartup>(OnDevastatorStartup);
        SubscribeLocalEvent<FSDevastatorComponent, AfterAutoHandleStateEvent>(OnDevastatorStateHandled);
    }

    private void OnStartup(EntityUid uid, FSZombieVisualsComponent comp, ComponentStartup args)
        => UpdateSprite(uid, comp);

    private void OnStateHandled(EntityUid uid, FSZombieVisualsComponent comp, AfterAutoHandleStateEvent args)
        => UpdateSprite(uid, comp);

    private void OnMobStateChanged(EntityUid uid, FSZombieVisualsComponent comp, MobStateChangedEvent args)
        => UpdateSprite(uid, comp);

    private void OnFlamethrowerStateHandled(EntityUid uid, FSFlamethrowerComponent comp, AfterAutoHandleStateEvent args)
    {
        if (TryComp<FSZombieVisualsComponent>(uid, out var visuals))
            UpdateSprite(uid, visuals);

        SetGlow(uid, FlamerGlowShader, comp.IsWindingUp || comp.IsFiring);
    }

    private void OnArmouredDeflectStateHandled(EntityUid uid, FSArmouredDeflectComponent comp, AfterAutoHandleStateEvent args)
    {
        SetGlow(uid, ArmourGlowShader, comp.IsGlowing);
    }

    private void OnTeslaStateHandled(EntityUid uid, FSTeslaZombieComponent comp, AfterAutoHandleStateEvent args)
    {
        SetGlow(uid, TeslaGlowShader, comp.IsFiring);
    }

    private void OnDevastatorStartup(EntityUid uid, FSDevastatorComponent comp, ComponentStartup args)
    {
        SetGlow(uid, DevastatorGlowShader, true);

        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.Color = DevastatorFullHealth;
    }

    private void SetGlow(EntityUid uid, string shaderId, bool enabled)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        ShaderInstance? desired = null;
        if (enabled)
        {
            if (!_glows.TryGetValue(shaderId, out desired))
            {
                desired = _protoManager.Index<ShaderPrototype>(shaderId).Instance();
                _glows[shaderId] = desired;
            }
        }

        sprite.PostShader = desired;
    }

    private void OnDevastatorStateHandled(EntityUid uid, FSDevastatorComponent comp, AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;
        sprite.Color = Color.InterpolateBetween(DevastatorFullHealth, DevastatorNearDeath, comp.BerserkRatio);
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
        else if (comp.DamagedState != null)
        {
            state = isDead ? "dead" : (comp.DamageStage > 0 ? comp.DamagedState : "base");
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
