using System.Linq;
using System.Numerics;
using Content.Client._FinalStand.Melee;
using Content.Client.Gameplay;
using Content.Shared.CCVar;
using Content.Shared.CombatMode;
using Content.Shared.Effects;
using Content.Shared.Hands.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.Timing;
using Robust.Shared.Configuration;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Client.Weapons.Melee;

public sealed partial class MeleeWeaponSystem : SharedMeleeWeaponSystem
{
    [Dependency] private IEyeManager _eyeManager = default!;
    [Dependency] private IInputManager _inputManager = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IStateManager _stateManager = default!;
    [Dependency] private AnimationPlayerSystem _animation = default!;
    [Dependency] private InputSystem _inputSystem = default!;
    [Dependency] private SharedColorFlashEffectSystem _color = default!;
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SpriteSystem _sprite = default!;
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IClientGameTiming _clientTiming = default!; // fs
    [Dependency] private FSMeleeJuiceSystem _fsJuice = default!; // FINALSTAND

    public const string MeleeLungeKey = "melee-lunge"; // FINALSTAND: public

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MeleeLungeEvent>(OnMeleeLunge);
        UpdatesOutsidePrediction = true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        UpdateEffects();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!Timing.IsFirstTimePredicted)
            return;

        var entityNull = _player.LocalEntity;

        if (entityNull == null)
            return;

        var entity = entityNull.Value;

        if (!TryGetWeapon(entity, out var weaponUid, out var weapon))
            return;

        if (!CombatMode.IsInCombatMode(entity) || !Blocker.CanAttack(entity, weapon: (weaponUid, weapon)))
        {
            weapon.Attacking = false;
            return;
        }

        var useDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.Use);
        var altDown = _inputSystem.CmdStates.GetState(EngineKeyFunctions.UseSecondary);

        if (weapon.AutoAttack || useDown != BoundKeyState.Down && altDown != BoundKeyState.Down || _cfg.GetCVar(CCVars.ControlHoldToAttackMelee))
        {
            if (weapon.Attacking)
            {
                RaisePredictiveEvent(new StopAttackEvent(GetNetEntity(weaponUid)));
            }
        }

        if (weapon.Attacking || weapon.NextAttack > Timing.CurTime)
        {
            return;
        }

        // TODO using targeted actions while combat mode is enabled should NOT trigger attacks.

        var mousePos = _eyeManager.PixelToMap(_inputManager.MouseScreenPosition);

        if (mousePos.MapId == MapId.Nullspace)
        {
            return;
        }

        EntityCoordinates coordinates;

        if (Maps.TryFindGridAt(mousePos, out var gridUid, out _))
        {
            coordinates = TransformSystem.ToCoordinates(gridUid, mousePos);
        }
        else
        {
            coordinates = TransformSystem.ToCoordinates(_map.GetMap(mousePos.MapId), mousePos);
        }

        // If the gun has AltFireComponent, it can be used to attack.
        if (TryComp<GunComponent>(weaponUid, out var gun) && gun.UseKey)
        {
            if (!TryComp<AltFireMeleeComponent>(weaponUid, out var altFireComponent) || altDown != BoundKeyState.Down)
                return;

            switch(altFireComponent.AttackType)
            {
                case AltFireAttackType.Light:
                    ClientLightAttack(entity, mousePos, coordinates, weaponUid, weapon);
                    break;

                case AltFireAttackType.Heavy:
                    ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
                    break;

                case AltFireAttackType.Disarm:
                    ClientDisarm(entity, mousePos, coordinates);
                    break;
            }

            return;
        }

        // Heavy attack.
        if (altDown == BoundKeyState.Down)
        {
            // If it's an unarmed attack then do a disarm
            if (weapon.AltDisarm && weaponUid == entity)
            {
                ClientDisarm(entity, mousePos, coordinates);
                return;
            }

            ClientHeavyAttack(entity, coordinates, weaponUid, weapon);
            return;
        }

        // Light attack
        if (useDown == BoundKeyState.Down)
            ClientLightAttack(entity, mousePos, coordinates, weaponUid, weapon);
    }

    protected override bool InRange(EntityUid user, EntityUid target, float range, ICommonSession? session)
    {
        var xform = Transform(target);
        var targetCoordinates = xform.Coordinates;
        var targetLocalAngle = xform.LocalRotation;

        return Interaction.InRangeUnobstructed(user, target, targetCoordinates, targetLocalAngle, range, overlapCheck: false);
    }

    protected override void DoDamageEffect(List<EntityUid> targets, EntityUid? user, TransformComponent targetXform)
    {
        // Server never sends the event to us for predictiveeevent.
        _color.RaiseEffect(Color.Red, targets, Filter.Local());
    }

    /// <summary>
    /// Raises a heavy attack event with the relevant attacked entities.
    /// This is to avoid lag effecting the client's perspective too much.
    /// </summary>
    private void ClientHeavyAttack(EntityUid user, EntityCoordinates coordinates, EntityUid meleeUid, MeleeWeaponComponent component)
    {
        // Only run on first prediction to avoid the potential raycast entities changing.
        if (!TryComp(user, out TransformComponent? userXform) ||
            !Timing.IsFirstTimePredicted)
        {
            return;
        }

        var targetMap = TransformSystem.ToMapCoordinates(coordinates);

        if (targetMap.MapId != userXform.MapID)
            return;

        var userPos = TransformSystem.GetWorldPosition(userXform);
        var direction = targetMap.Position - userPos;
        var distance = component.Angle.Theta >= Math.Tau - 0.01 ? component.Range : Math.Min(component.Range, direction.Length()); // FINALSTAND: full-circle swings reach their whole range

        // This should really be improved. GetEntitiesInArc uses pos instead of bounding boxes.
        // Server will validate it with InRangeUnobstructed.
        var entities = GetNetEntityList(ArcRayCast(userPos, direction.ToWorldAngle(), component.Angle, distance, userXform.MapID, user).OrderByDescending(e => IsEnemy(user, e)).ToList()); // FINALSTAND: living enemies take the target slots before corpses, teammates and walls
        RaisePredictiveEvent(new HeavyAttackEvent(GetNetEntity(meleeUid), entities.GetRange(0, Math.Min(MaxTargets, entities.Count)), GetNetCoordinates(coordinates)) { ViewTick = _clientTiming.LastRealTick }); // fs
    }

    private void ClientDisarm(EntityUid attacker, MapCoordinates mousePos, EntityCoordinates coordinates)
    {
        EntityUid? target = null;

        if (_stateManager.CurrentState is GameplayStateBase screen)
            target = screen.GetClickedEntity(mousePos);

        RaisePredictiveEvent(new DisarmAttackEvent(GetNetEntity(target), GetNetCoordinates(coordinates)) { ViewTick = _clientTiming.LastRealTick }); // fs
    }

    private void ClientLightAttack(EntityUid attacker, MapCoordinates mousePos, EntityCoordinates coordinates, EntityUid weaponUid, MeleeWeaponComponent meleeComponent)
    {
        var attackerPos = TransformSystem.GetMapCoordinates(attacker);

        if (mousePos.MapId != attackerPos.MapId)
            return;

        // FINALSTAND: swing toward the cursor at any distance and aim at the nearest enemy unless one was clicked
        var direction = mousePos.Position - attackerPos.Position;
        EntityUid? target = null;

        if (direction.Length() <= meleeComponent.Range && _stateManager.CurrentState is GameplayStateBase screen)
            target = screen.GetClickedEntity(mousePos);

        // Don't light-attack if interaction will be handling this instead
        if (Interaction.CombatModeCanHandInteract(attacker, target))
            return;

        if ((target == null || !IsEnemy(attacker, target.Value)) && direction != Vector2.Zero && FindLightTarget(attacker, attackerPos, direction, meleeComponent.Range) is { } assisted)
        {
            target = assisted;
            coordinates = Transform(assisted).Coordinates;
        }
        else if (direction.Length() > meleeComponent.Range)
        {
            coordinates = TransformSystem.ToCoordinates(TransformSystem.GetMap(attacker) ?? EntityUid.Invalid,
                new MapCoordinates(attackerPos.Position + direction.Normalized() * meleeComponent.Range, attackerPos.MapId));
        }

        RaisePredictiveEvent(new LightAttackEvent(GetNetEntity(target), GetNetEntity(weaponUid), GetNetCoordinates(coordinates)) { ViewTick = _clientTiming.LastRealTick }); // fs
    }

    private EntityUid? FindLightTarget(EntityUid attacker, MapCoordinates attackerPos, Vector2 direction, float range)
    {
        EntityUid? best = null;
        var bestDistance = float.MaxValue;
        foreach (var uid in ArcRayCast(attackerPos.Position, direction.ToWorldAngle(), Angle.FromDegrees(70), range, attackerPos.MapId, attacker))
        {
            if (!IsEnemy(attacker, uid))
                continue;

            var distance = (TransformSystem.GetWorldPosition(uid) - attackerPos.Position).LengthSquared();
            if (distance >= bestDistance)
                continue;

            best = uid;
            bestDistance = distance;
        }

        return best;
    }

    private bool IsEnemy(EntityUid attacker, EntityUid uid)
    {
        return HasComp<MobStateComponent>(uid) && !MobState.IsDead(uid) && !_fsJuice.IsAlly(attacker, uid);
    }

    private void OnMeleeLunge(MeleeLungeEvent ev)
    {
        var ent = GetEntity(ev.Entity);
        var entWeapon = GetEntity(ev.Weapon);

        // Entity might not have been sent by PVS.
        if (Exists(ent) && Exists(entWeapon))
            DoLunge(ent, entWeapon, ev.Angle, ev.LocalPos, ev.Animation);
    }
}
