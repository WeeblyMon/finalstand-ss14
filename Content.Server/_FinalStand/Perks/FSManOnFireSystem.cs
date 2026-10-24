using Content.Shared._FinalStand.Perks;
using Content.Shared.Atmos;

namespace Content.Server._FinalStand.Perks;

public sealed partial class FSManOnFireSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSManOnFireComponent, GetFireProtectionEvent>(OnGetFireProtection);
    }

    public static float GetResist(int level)
    {
        return level <= 0 ? 0f : FSPerkBonusConstants.ManOnFireResist[Math.Min(level, FSPerkDef.MaxLevel) - 1];
    }

    public float GetHeatResist(EntityUid uid)
    {
        return TryComp<FSManOnFireComponent>(uid, out var comp) ? GetResist(comp.Level) : 0f;
    }

    private void OnGetFireProtection(Entity<FSManOnFireComponent> ent, ref GetFireProtectionEvent args)
    {
        args.Reduce(GetResist(ent.Comp.Level));
    }
}
