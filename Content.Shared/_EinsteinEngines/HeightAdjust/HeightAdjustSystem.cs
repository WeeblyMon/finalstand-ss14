using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._EinsteinEngines.HeightAdjust;

public sealed partial class HeightAdjustSystem : EntitySystem
{
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private SharedContentEyeSystem _eye = default!;
    [Dependency] private SharedHumanoidAppearanceSystem _appearance = default!;
    [Dependency] private IConfigurationManager _config = default!;

    public bool SetScale(EntityUid uid, float scale)
    {
        return SetScale(uid, new Vector2(scale, scale));
    }

    public bool SetScale(EntityUid uid, Vector2 scale)
    {
        var succeeded = true;
        var avg = (scale.X + scale.Y) / 2;

        if (_config.GetCVar(CCVars.HeightAdjustModifiesZoom) && TryComp<ContentEyeComponent>(uid, out var eye))
            _eye.SetMaxZoom(uid, eye.MaxZoom * avg);
        else
            succeeded = false;

        if (_config.GetCVar(CCVars.HeightAdjustModifiesHitbox) && TryComp<FixturesComponent>(uid, out var fixtures))
            foreach (var fixture in fixtures.Fixtures)
                _physics.SetRadius(uid, fixture.Key, fixture.Value, fixture.Value.Shape, MathF.MinMagnitude(fixture.Value.Shape.Radius * avg, 0.49f));
        else
            succeeded = false;

        if (HasComp<HumanoidProfileComponent>(uid))
            _appearance.SetScale(uid, scale);
        else
            succeeded = false;

        RaiseLocalEvent(uid, new HeightAdjustedEvent { NewScale = scale });

        return succeeded;
    }
}
