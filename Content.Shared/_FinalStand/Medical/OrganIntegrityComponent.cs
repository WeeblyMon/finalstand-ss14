using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._FinalStand.Medical;

// Kept off vanilla's OrganComponent so the body layer stays upstream-clean.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class OrganIntegrityComponent : Component
{
    [DataField("intCap"), AutoNetworkedField]
    public FixedPoint2 IntegrityCap = 15;

    [DataField("integrity"), AutoNetworkedField]
    public FixedPoint2 OrganIntegrity = 15;

    [DataField, AutoNetworkedField]
    public OrganSeverity OrganSeverity = OrganSeverity.Normal;

    [DataField]
    public SoundSpecifier OrganDestroyedSound = new SoundCollectionSpecifier("OrganDestroyed");

    public Dictionary<(string, EntityUid), FixedPoint2> IntegrityModifiers = new();

    [DataField]
    public Dictionary<OrganSeverity, FixedPoint2> IntegrityThresholds = new()
    {
        { OrganSeverity.Normal, 15 },
        { OrganSeverity.Damaged, 10 },
        { OrganSeverity.Destroyed, 0 },
    };
}
