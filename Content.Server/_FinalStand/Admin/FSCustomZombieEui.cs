using System.Linq;
using System.Numerics;
using System.Reflection;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Server.Sprite;
using Content.Shared._FinalStand.Admin;
using Content.Shared._FinalStand.Mobs;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;

namespace Content.Server._FinalStand.Admin;

[UsedImplicitly]
public sealed partial class FSCustomZombieEui : BaseEui
{
    [Dependency] private IEntityManager _entManager = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IAdminManager _admin = default!;

    public FSCustomZombieEui()
    {
        IoCManager.InjectDependencies(this);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);

        if (!_admin.HasAdminFlag(Player, AdminFlags.Fun))
            return;

        switch (msg)
        {
            case FSCustomZombieEuiMsg.RequestFields request:
                SendFields(request.ProtoId);
                break;
            case FSCustomZombieEuiMsg.SpawnRequest spawn:
                TrySpawn(spawn);
                break;
        }
    }

    private void SendFields(string protoId)
    {
        if (!TryGetZombieProto(protoId, out var proto))
            return;

        var data = new FSCustomZombieEuiMsg.FieldData { ProtoId = protoId };

        if (proto.Components.TryGetComponent("MobThresholds", out var thresholdsRaw)
            && thresholdsRaw is MobThresholdsComponent thresholds)
        {
            var dead = thresholds.Thresholds
                .Where(pair => pair.Value == MobState.Dead)
                .Select(pair => pair.Key)
                .DefaultIfEmpty(FixedPoint2.Zero)
                .Max();
            data.Health = dead.Float();
        }

        if (proto.Components.TryGetComponent("MovementSpeedModifier", out var moveRaw)
            && moveRaw is MovementSpeedModifierComponent move)
        {
            data.WalkSpeed = move.BaseWalkSpeed;
            data.SprintSpeed = move.BaseSprintSpeed;
        }

        foreach (var (name, entry) in proto.Components)
        {
            if (!name.StartsWith("FS"))
                continue;

            foreach (var field in GetNumericFields(entry.Component.GetType()))
            {
                var raw = field.GetValue(entry.Component);
                if (raw == null)
                    continue;

                data.Fields.Add(new FSZombieFieldInfo
                {
                    Component = name,
                    Field = field.Name,
                    Value = raw is int i ? i : (float)raw,
                    IsInt = field.FieldType == typeof(int),
                });
            }
        }

        SendMessage(data);
    }

    private void TrySpawn(FSCustomZombieEuiMsg.SpawnRequest req)
    {
        if (!TryGetZombieProto(req.ProtoId, out _))
            return;

        var coords = _entManager.GetCoordinates(req.Coordinates);
        if (!coords.IsValid(_entManager))
            return;

        var uid = _entManager.SpawnEntity(req.ProtoId, coords);
        ApplyOverrides(uid, req);
    }

    private bool TryGetZombieProto(string protoId, out EntityPrototype proto)
    {
        proto = default!;

        if (!protoId.StartsWith("FSZombie"))
            return false;

        if (!_protoManager.TryIndex(protoId, out EntityPrototype? found) || found.Abstract)
            return false;

        proto = found;
        return true;
    }

    private static IEnumerable<FieldInfo> GetNumericFields(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => f.GetCustomAttribute<DataFieldAttribute>() != null)
            .Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(int));
    }

    private void ApplyOverrides(EntityUid uid, FSCustomZombieEuiMsg.SpawnRequest req)
    {
        if (req.Health > 0)
        {
            _entManager.System<MobThresholdSystem>()
                .SetMobStateThreshold(uid, FixedPoint2.New(req.Health), MobState.Dead);
        }

        if (req.WalkSpeed > 0 && req.SprintSpeed > 0
            && _entManager.TryGetComponent<MovementSpeedModifierComponent>(uid, out var move))
        {
            var movement = _entManager.System<MovementSpeedModifierSystem>();
            movement.ChangeBaseSpeed(uid, req.WalkSpeed, req.SprintSpeed, move.Acceleration, move);
            movement.RefreshMovementSpeedModifiers(uid);
        }

        if (req.Scale > 0 && Math.Abs(req.Scale - 1f) > 0.001f)
        {
            _entManager.System<ScaleVisualsSystem>()
                .SetSpriteScale(uid, new Vector2(req.Scale, req.Scale));
        }

        if (Math.Abs(req.DamageTakenMultiplier - 1f) > 0.001f)
        {
            _entManager.EnsureComponent<FSAdminBuffComponent>(uid)
                .DamageTakenMultiplier = req.DamageTakenMultiplier;
        }

        if (Math.Abs(req.DamageMultiplier - 1f) > 0.001f
            && _entManager.TryGetComponent<MeleeWeaponComponent>(uid, out var melee))
        {
            melee.Damage *= req.DamageMultiplier;
            _entManager.Dirty(uid, melee);
        }

        foreach (var over in req.Overrides)
            ApplyFieldOverride(uid, over);
    }

    private void ApplyFieldOverride(EntityUid uid, FSZombieFieldOverride over)
    {
        if (!over.Component.StartsWith("FS"))
            return;

        if (!_compFactory.TryGetRegistration(over.Component, out var registration))
            return;

        if (!_entManager.TryGetComponent(uid, registration.Type, out var comp))
            return;

        var field = registration.Type.GetField(over.Field, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || field.GetCustomAttribute<DataFieldAttribute>() == null)
            return;

        if (field.FieldType == typeof(float))
            field.SetValue(comp, over.Value);
        else if (field.FieldType == typeof(int))
            field.SetValue(comp, (int)MathF.Round(over.Value));
        else
            return;

        _entManager.Dirty(uid, comp);
    }
}
