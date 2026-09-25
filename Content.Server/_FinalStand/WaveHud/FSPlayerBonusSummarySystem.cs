using Content.Shared._FinalStand.WaveHud;
using Content.Server._FinalStand.Perks;
using Content.Server._FinalStand.Research;
using Content.Shared._FinalStand.Grenades;
using Content.Shared._FinalStand.Leveling;
using Content.Shared._FinalStand.Perks;
using Content.Shared._FinalStand.Weapons;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Mind;
using Content.Shared.Tag;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Player;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.WaveHud;

// Drives the wave-HUD "current bonuses" indicator, recomputed from source data each time.
public sealed partial class FSPlayerBonusSummarySystem : EntitySystem
{
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private FSResearchBuffSystem _researchBuff = default!;
    [Dependency] private FSResearchStaticGrantSystem _researchStatic = default!;
    [Dependency] private FSWeaponClassifierSystem _classifier = default!;
    [Dependency] private FSCombatMedicSystem _combatMedic = default!;
    [Dependency] private FSBerserkerSystem _berserker = default!;
    [Dependency] private FSBloodloadSystem _bloodload = default!;

    private static readonly FSBonusCategory Empty = new(0f, Array.Empty<string>());

    private TimeSpan _nextSweep;

    // Last summary each player was sent; the sweep would otherwise resend it every second.
    private readonly Dictionary<NetUserId, FSPlayerBonusSummaryEvent> _lastSent = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FSResearchNodeCompletedEvent>(OnResearchNodeCompleted);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnResearchNodeCompleted(FSResearchNodeCompletedEvent ev) => RecomputeAll();

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        if (ev.Player is { } session)
            _lastSent.Remove(session.UserId);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _nextSweep)
            return;
        _nextSweep = now + TimeSpan.FromSeconds(1);
        RecomputeAll();
    }

    private void RecomputeAll()
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is { } mob)
                RecomputeFor(mob, session);
        }
    }

    // Public so FSOfficerSystem can force an instant refresh the moment a whistle buff lands.
    public void RecomputeFor(EntityUid mob, ICommonSession session)
    {
        if (!_mind.TryGetMind(mob, out var mindId, out _))
            return;

        var perks = CompOrNull<FSPerkLevelsComponent>(mindId);
        var hasOfficer = TryComp<FSOfficerBuffComponent>(mindId, out var officerBuff) && _timing.CurTime < officerBuff.EndTime;
        var officerLevel = hasOfficer ? officerBuff!.Level : 0;
        var deathAuraStacks = (perks?.GetSlottedLevel("DeathAura") ?? 0) > 0
            ? CompOrNull<FSDeathAuraComponent>(mindId)?.Stacks ?? 0
            : 0;

        EntityUid? held = null;
        if (TryComp<HandsComponent>(mob, out var hands) && hands.ActiveHandId != null)
            _hands.TryGetHeldItem((mob, hands), hands.ActiveHandId, out held);

        var gunDamage = Empty;
        var fireRate = Empty;
        var explosiveDamage = Empty;
        var reloadSpeed = Empty;
        var magazineSize = Empty;
        var meleeDamage = Empty;

        if (held is { } heldUid)
        {
            if (HasComp<GunComponent>(heldUid))
                ComputeGunCategories(mob, heldUid, perks, hasOfficer, officerLevel, deathAuraStacks,
                    out gunDamage, out fireRate, out explosiveDamage, out reloadSpeed, out magazineSize);
            else if (HasComp<MeleeWeaponComponent>(heldUid))
                meleeDamage = ComputeMeleeDamage(mob, perks);
            else if (HasComp<FSGrenadePackComponent>(heldUid))
                explosiveDamage = ComputeGrenadeExplosiveDamage(perks);
        }

        var summary = new FSPlayerBonusSummaryEvent(gunDamage, fireRate, meleeDamage, explosiveDamage, reloadSpeed, magazineSize);

        if (_lastSent.TryGetValue(session.UserId, out var previous) && summary.Matches(previous))
            return;

        _lastSent[session.UserId] = summary;
        RaiseNetworkEvent(summary, Filter.SinglePlayer(session));
    }

    private void ComputeGunCategories(EntityUid mob, EntityUid heldUid, FSPerkLevelsComponent? perks,
        bool hasOfficer, int officerLevel, int deathAuraStacks,
        out FSBonusCategory gunDamage, out FSBonusCategory fireRate, out FSBonusCategory explosiveDamage,
        out FSBonusCategory reloadSpeed, out FSBonusCategory magazineSize)
    {
        var kind = _classifier.Classify(heldUid);
        var isBallistic = kind.Ballistic;
        var isEnergy = kind.Energy;
        var isLauncher = kind.Launcher;
        var isL6 = kind.L6;
        var isMinigun = kind.Minigun;
        var isHydra = kind.Hydra;
        var isRpg = kind.Rpg;
        var isXray = kind.Xray;
        var isTesla = kind.Tesla;

        var dmgMul = _researchBuff.GetDamageMultiplier(isBallistic, isEnergy, isLauncher, isL6, isMinigun, isHydra, isRpg, isXray, isTesla);
        var dmgPct = (dmgMul - 1f) * 100f;
        var dmgSources = new List<string>();
        if (dmgMul != 1f)
            dmgSources.Add(FormatPct("Ordnance research", dmgPct));

        var spLevel = perks?.GetSlottedLevel("StoppingPower") ?? 0;
        if (spLevel > 0 && !isLauncher)
        {
            var pct = spLevel * FSPerkBonusConstants.StoppingPowerPerLevel * 100f;
            dmgPct += pct;
            dmgSources.Add(FormatPct("Stopping Power", pct));
        }

        var gcLevel = perks?.GetSlottedLevel("GlassCannon") ?? 0;
        if (gcLevel > 0)
        {
            var pct = gcLevel * FSPerkBonusConstants.GlassCannonPerLevel * 100f;
            dmgPct += pct;
            dmgSources.Add(FormatPct("Glass Cannon", pct));
        }

        if (deathAuraStacks > 0)
        {
            var pct = deathAuraStacks * FSPerkBonusConstants.DeathAuraPerStack * 100f;
            dmgPct += pct;
            dmgSources.Add(FormatPct("Death Aura", pct));
        }

        if (hasOfficer)
        {
            var pct = officerLevel * FSPerkBonusConstants.OfficerBuffPerLevel * 100f;
            dmgPct += pct;
            dmgSources.Add(FormatPct("Officer buff", pct));
        }

        if ((perks?.GetSlottedLevel("Pacifist") ?? 0) > 0)
        {
            var pct = -FSPerkBonusConstants.PacifistPenalty * 100f;
            dmgPct += pct;
            dmgSources.Add(FormatPct("Pacifist", pct));
        }

        AddPerkDamage(mob, perks, ranged: true, ref dmgPct, dmgSources);
        if (isLauncher)
            AddImplosion(perks, ref dmgPct, dmgSources);

        var damageCategory = new FSBonusCategory(dmgPct, dmgSources.ToArray());
        gunDamage = isLauncher ? Empty : damageCategory;
        explosiveDamage = isLauncher ? damageCategory : Empty;

        var (fireRateMul, reloadPct) = _researchBuff.GetFireRateReloadTotals(isBallistic, isL6, isMinigun, isHydra, isEnergy, isTesla);
        var frPct = (fireRateMul - 1f) * 100f;
        var frSources = new List<string>();
        if (fireRateMul != 1f)
            frSources.Add(FormatPct("Ordnance research", frPct));

        var bsLevel = perks?.GetSlottedLevel("BulletStorm") ?? 0;
        if (bsLevel > 0)
        {
            var pct = bsLevel * FSPerkBonusConstants.BulletStormPerLevel * 100f;
            frPct += pct;
            frSources.Add(FormatPct("Bullet Storm", pct));
        }
        fireRate = new FSBonusCategory(frPct, frSources.ToArray());

        var reloadTotal = reloadPct * 100f;
        var reloadSources = new List<string>();
        if (reloadPct != 0f)
            reloadSources.Add(FormatPct("Ordnance research", reloadTotal));

        var bloodload = _bloodload.GetReloadTimeMultiplier(mob);
        if (bloodload < 1f)
        {
            var pct = (1f / bloodload - 1f) * 100f;
            reloadTotal += pct;
            reloadSources.Add(FormatPct("Bloodload", pct));
        }
        reloadSpeed = reloadSources.Count > 0 ? new FSBonusCategory(reloadTotal, reloadSources.ToArray()) : Empty;

        var magPct = _researchStatic.GetMagazinePercentBonus(isBallistic, isL6, isMinigun, isHydra) * 100f;
        var magSources = new List<string>();
        if (magPct != 0f)
            magSources.Add(FormatPct("Ordnance research", magPct));

        var bandolierLevel = perks?.GetSlottedLevel("Bandolier") ?? 0;
        if (bandolierLevel > 0)
        {
            var pct = bandolierLevel * FSPerkBonusConstants.BandolierPerLevel * 100f;
            magPct += pct;
            magSources.Add(FormatPct("Bandolier", pct));
        }
        magazineSize = magSources.Count > 0 ? new FSBonusCategory(magPct, magSources.ToArray()) : Empty;
    }

    private FSBonusCategory ComputeMeleeDamage(EntityUid mob, FSPerkLevelsComponent? perks)
    {
        var pct = 0f;
        var sources = new List<string>();

        var snsLevel = perks?.GetSlottedLevel("SwordAndShield") ?? 0;
        if (snsLevel > 0)
        {
            var p = snsLevel * FSPerkBonusConstants.SwordAndShieldPerLevel * 100f;
            pct += p;
            sources.Add(FormatPct("Sword & Shield", p));
        }

        var gcLevel = perks?.GetSlottedLevel("GlassCannon") ?? 0;
        if (gcLevel > 0)
        {
            var p = gcLevel * FSPerkBonusConstants.GlassCannonPerLevel * 100f;
            pct += p;
            sources.Add(FormatPct("Glass Cannon", p));
        }

        if ((perks?.GetSlottedLevel("Pacifist") ?? 0) > 0)
        {
            var p = -FSPerkBonusConstants.PacifistPenalty * 100f;
            pct += p;
            sources.Add(FormatPct("Pacifist", p));
        }

        AddPerkDamage(mob, perks, ranged: false, ref pct, sources);

        return sources.Count > 0 ? new FSBonusCategory(pct, sources.ToArray()) : Empty;
    }

    private void AddPerkDamage(EntityUid mob, FSPerkLevelsComponent? perks, bool ranged, ref float total, List<string> sources)
    {
        if (perks == null || !_mind.TryGetMind(mob, out var mindId, out _))
            return;

        var medic = (_combatMedic.GetDamageMultiplier(mindId) - 1f) * 100f;
        if (medic > 0f)
        {
            total += medic;
            sources.Add(FormatPct("Combat Medic", medic));
        }

        var berserker = (_berserker.GetMultiplier(mob, perks, ranged) - 1f) * 100f;
        if (berserker > 0.5f)
        {
            total += berserker;
            sources.Add(FormatPct("Berserker", berserker));
        }
    }

    private static void AddImplosion(FSPerkLevelsComponent? perks, ref float total, List<string> sources)
    {
        var level = perks?.GetSlottedLevel("Implosion") ?? 0;
        if (level <= 0)
            return;

        var pct = FSPerkBonusConstants.ImplosionDamage[Math.Min(level, FSPerkDef.MaxLevel) - 1] * 100f;
        total += pct;
        sources.Add(FormatPct("Implosion", pct));
    }

    private FSBonusCategory ComputeGrenadeExplosiveDamage(FSPerkLevelsComponent? perks)
    {
        var pct = 0f;
        var sources = new List<string>();

        var mul = _researchBuff.GetDamageMultiplier(false, false, true, false, false, false, false, false, false);
        if (mul != 1f)
        {
            pct = (mul - 1f) * 100f;
            sources.Add(FormatPct("Ordnance research", pct));
        }

        AddImplosion(perks, ref pct, sources);
        return sources.Count > 0 ? new FSBonusCategory(pct, sources.ToArray()) : Empty;
    }

    private static string FormatPct(string label, float pct) => $"{label} {(pct >= 0 ? "+" : "")}{pct:0.#}%";

    // Debug-only, backs the fsdebugbonus console command.
    public string DescribeFor(EntityUid mob)
    {
        if (!_mind.TryGetMind(mob, out var mindId, out _))
            return "No mind on that entity.";

        var perks = CompOrNull<FSPerkLevelsComponent>(mindId);
        var hasOfficer = TryComp<FSOfficerBuffComponent>(mindId, out var officerBuff) && _timing.CurTime < officerBuff.EndTime;
        var officerLevel = hasOfficer ? officerBuff!.Level : 0;
        var deathAuraStacks = (perks?.GetSlottedLevel("DeathAura") ?? 0) > 0
            ? CompOrNull<FSDeathAuraComponent>(mindId)?.Stacks ?? 0
            : 0;

        EntityUid? held = null;
        if (TryComp<HandsComponent>(mob, out var hands) && hands.ActiveHandId != null)
            _hands.TryGetHeldItem((mob, hands), hands.ActiveHandId, out held);

        if (held is not { } heldUid)
            return "Not holding anything.";

        var protoId = Prototype(heldUid)?.ID ?? "(no prototype)";
        var kind = _classifier.Classify(heldUid);
        var isBallistic = kind.Ballistic;
        var isEnergy = kind.Energy;
        var isLauncher = kind.Launcher;
        var isMinigun = kind.Minigun;
        var hasGun = HasComp<GunComponent>(heldUid);
        var hasMelee = HasComp<MeleeWeaponComponent>(heldUid);
        var hasGrenadePack = HasComp<FSGrenadePackComponent>(heldUid);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Held: {protoId} (uid {heldUid})");
        sb.AppendLine($"  HasGun={hasGun} HasMelee={hasMelee} HasGrenadePack={hasGrenadePack}");
        sb.AppendLine($"  Tags: Ballistic={isBallistic} Energy={isEnergy} Launcher={isLauncher} FSMinigunComponent={isMinigun}");

        if (hasGrenadePack)
        {
            var grenadeExplosive = ComputeGrenadeExplosiveDamage(perks);
            sb.AppendLine($"  ExplosiveDamage (grenade): {grenadeExplosive.Percent:0.##}%  [{string.Join(", ", grenadeExplosive.Sources)}]");
            return sb.ToString();
        }

        if (!hasGun && !hasMelee)
        {
            sb.AppendLine("  -> neither GunComponent, MeleeWeaponComponent, nor FSGrenadePackComponent - nothing will show for this item.");
            return sb.ToString();
        }

        if (hasGun)
        {
            ComputeGunCategories(mob, heldUid, perks, hasOfficer, officerLevel, deathAuraStacks,
                out var gunDamage, out var fireRate, out var explosiveDamage, out var reloadSpeed, out var magazineSize);
            sb.AppendLine($"  GunDamage: {gunDamage.Percent:0.##}%  [{string.Join(", ", gunDamage.Sources)}]");
            sb.AppendLine($"  FireRate: {fireRate.Percent:0.##}%  [{string.Join(", ", fireRate.Sources)}]");
            sb.AppendLine($"  ExplosiveDamage: {explosiveDamage.Percent:0.##}%  [{string.Join(", ", explosiveDamage.Sources)}]");
            sb.AppendLine($"  ReloadSpeed: {reloadSpeed.Percent:0.##}%  [{string.Join(", ", reloadSpeed.Sources)}]");
            sb.AppendLine($"  MagazineSize: {magazineSize.Percent:0.##}  [{string.Join(", ", magazineSize.Sources)}]");
        }
        if (hasMelee)
        {
            var melee = ComputeMeleeDamage(mob, perks);
            sb.AppendLine($"  MeleeDamage: {melee.Percent:0.##}%  [{string.Join(", ", melee.Sources)}]");
        }

        return sb.ToString();
    }
}
