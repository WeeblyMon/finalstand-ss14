using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float> DragDropDeadZone =
        CVarDef.Create("control.drag_dead_zone", 12f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ToggleWalk =
        CVarDef.Create("control.toggle_walk", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> InteractionRateLimitCount =
        CVarDef.Create("interaction.rate_limit_count", 5, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float> InteractionRateLimitPeriod =
        CVarDef.Create("interaction.rate_limit_period", 0.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<int> InteractionRateLimitAnnounceAdminsDelay =
        CVarDef.Create("interaction.rate_limit_announce_admins_delay", 120, CVar.SERVERONLY);

    public static readonly CVarDef<bool> StaticStorageUI =
        CVarDef.Create("control.static_storage_ui", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> OpaqueStorageWindow =
        CVarDef.Create("control.opaque_storage_background", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> StorageWindowTitle =
        CVarDef.Create("control.storage_window_title", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> StorageLimit =
        CVarDef.Create("control.storage_limit", 10, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> NestedStorage =
        CVarDef.Create("control.nested_storage", true, CVar.REPLICATED | CVar.SERVER);

    public static readonly CVarDef<bool> ControlHoldToAttackMelee =
        CVarDef.Create("control.hold_to_attack_melee", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> ControlHoldToAttackRanged =
        CVarDef.Create("control.hold_to_attack_ranged", false, CVar.CLIENTONLY | CVar.ARCHIVE);

}
