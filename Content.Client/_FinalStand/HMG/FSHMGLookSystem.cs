using Content.Client.Movement.Systems;
using Content.Shared._FinalStand.HMG;
using Content.Shared.Camera;
using Content.Client.Movement.Components;

namespace Content.Client._FinalStand.HMG;

public sealed partial class FSHMGLookSystem : EntitySystem
{
    [Dependency] private EyeCursorOffsetSystem _eyeOffset = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSHMGGunnerComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
        SubscribeLocalEvent<FSHMGGunnerComponent, ComponentShutdown>(OnGunnerShutdown);
    }

    private void OnGetEyeOffset(Entity<FSHMGGunnerComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (!HasComp<EyeCursorOffsetComponent>(ent.Comp.Hmg)
            || _eyeOffset.OffsetAfterMouse(ent.Comp.Hmg, null) is not { } offset)
            return;

        args.Offset += offset;
    }

    private void OnGunnerShutdown(Entity<FSHMGGunnerComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<EyeCursorOffsetComponent>(ent.Comp.Hmg, out var look))
            return;

        look.CurrentPosition = default;
        look.TargetPosition = default;
    }
}
