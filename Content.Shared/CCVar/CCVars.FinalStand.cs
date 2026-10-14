using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> FSAutoStart =
        CVarDef.Create("fs.autostart", true, CVar.SERVERONLY);

    // RCD picker layout: the Build Ring (radial) by default, or the Build Belt hotbar.
    public static readonly CVarDef<bool> FSRcdBeltLayout =
        CVarDef.Create("fs.rcd_belt_layout", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSPingChat =
        CVarDef.Create("fs.ping_chat", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    // Hit sprites, particles and swing trails on melee attacks.
    public static readonly CVarDef<bool> FSMeleeEffects =
        CVarDef.Create("fs.melee_effects", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSRcdHintSeen =
        CVarDef.Create("fs.rcd_hint_seen", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSHudMigrated =
        CVarDef.Create("fs.hud_migrated", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSUiScaleMigrated =
        CVarDef.Create("fs.ui_scale_migrated", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<string> FSChatSize =
        CVarDef.Create("fs.chat_size", "", CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSChatSizeMigrated =
        CVarDef.Create("fs.chat_size_migrated", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSClassicHud =
        CVarDef.Create("fs.classic_hud", false, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> FSWeaponsVolume =
        CVarDef.Create("fs.weapons_volume", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> FSAudioDynamics =
        CVarDef.Create("fs.audio_dynamics", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<float> FSWeaponsHeadroom =
        CVarDef.Create("fs.weapons_headroom", 1f, CVar.CLIENTONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> FSDifficultyVoteSeconds =
        CVarDef.Create("fs.difficulty_vote_seconds", 60, CVar.SERVERONLY);

    public static readonly CVarDef<int> FSEndlessVoteSeconds =
        CVarDef.Create("fs.endless_vote_seconds", 30, CVar.SERVERONLY);

    public static readonly CVarDef<float> FSWaveObjectiveChance =
        CVarDef.Create("fs.wave_objective_chance", 0.35f, CVar.SERVERONLY);

    public static readonly CVarDef<int> FSRoundModifierCount =
        CVarDef.Create("fs.round_modifier_count", 1, CVar.SERVERONLY);

    public static readonly CVarDef<string> FSDiscordWebhook =
        CVarDef.Create("fs.discord_webhook", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<int> FSDiscordLivePlayers =
        CVarDef.Create("fs.discord_live_players", 4, CVar.SERVERONLY);

    public static readonly CVarDef<int> FSDiscordLiveCooldownMinutes =
        CVarDef.Create("fs.discord_live_cooldown_minutes", 120, CVar.SERVERONLY);
}
