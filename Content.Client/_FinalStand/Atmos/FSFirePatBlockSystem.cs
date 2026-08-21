using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Interaction;

namespace Content.Client._FinalStand.Atmos;

public sealed class FSFirePatBlockSystem : EntitySystem
{
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExtinguishOnInteractComponent, InteractHandEvent>(OnInteractHand,
            before: new[] { typeof(InteractionPopupSystem) });
    }

    private void OnInteractHand(EntityUid uid, ExtinguishOnInteractComponent component, InteractHandEvent args)
    {
        if (args.Handled || args.User == uid)
            return;

        if (!_appearance.TryGetData<bool>(uid, FireVisuals.OnFire, out var onFire) || !onFire)
            return;

        args.Handled = true;
    }
}
