// Hand teleporter portals close after a lifetime that only counts down during combat.
using Content.Server._FinalStand.GameTicking.Rules;
using Content.Server.Teleportation;
using Content.Shared._FinalStand.GameTicking;
using Content.Shared._FinalStand.Teleportation;
using Content.Shared.Teleportation.Components;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Server._FinalStand.Teleportation;

public sealed partial class FSPortalExpirySystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private HandTeleporterSystem _teleporter = default!;
    [Dependency] private WaveGameRuleSystem _wave = default!;

    private static readonly SoundSpecifier CollapseSound = new SoundCollectionSpecifier("sparks");

    public void Start(Entity<HandTeleporterComponent> ent)
    {
        if (ent.Comp.PortalLifetime is not { } lifetime)
            return;

        var left = TimeSpan.FromSeconds(lifetime);
        ent.Comp.PortalsExpireAt = _timing.CurTime + left;
        ent.Comp.PortalsPausedLeft = IsPrep() ? left : null;
        Sync(ent);
    }

    public void Sync(Entity<HandTeleporterComponent> ent)
    {
        Dirty(ent);
        if (ent.Comp.PortalsExpireAt is not { } expiresAt)
            return;

        SyncPortal(ent.Comp.FirstPortal, expiresAt, ent.Comp.PortalsPausedLeft, ent.Comp.PortalLifetime ?? 0f);
        SyncPortal(ent.Comp.SecondPortal, expiresAt, ent.Comp.PortalsPausedLeft, ent.Comp.PortalLifetime ?? 0f);
    }

    private void SyncPortal(EntityUid? portal, TimeSpan expiresAt, TimeSpan? pausedLeft, float lifetime)
    {
        if (Deleted(portal))
            return;

        var comp = EnsureComp<FSExpiringPortalComponent>(portal.Value);
        comp.ExpiresAt = expiresAt;
        comp.PausedLeft = pausedLeft;
        comp.Lifetime = lifetime;
        Dirty(portal.Value, comp);
    }

    private bool IsPrep()
    {
        return _wave.TryGetWaveStatus(out _, out var phase) && phase == WavePhase.Prep;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        bool? prep = null;
        var query = EntityQueryEnumerator<HandTeleporterComponent>();
        while (query.MoveNext(out var uid, out var teleporter))
        {
            if (teleporter.PortalsExpireAt is not { } expiresAt)
                continue;

            prep ??= IsPrep();
            if (prep.Value)
            {
                if (teleporter.PortalsPausedLeft != null)
                    continue;

                var remaining = expiresAt - now;
                teleporter.PortalsPausedLeft = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
                Sync((uid, teleporter));
                continue;
            }

            if (teleporter.PortalsPausedLeft is { } left)
            {
                teleporter.PortalsExpireAt = now + left;
                teleporter.PortalsPausedLeft = null;
                Sync((uid, teleporter));
                continue;
            }

            if (expiresAt > now)
                continue;

            PlayCollapse(teleporter.FirstPortal);
            PlayCollapse(teleporter.SecondPortal);
            _teleporter.FizzlePortals((uid, teleporter), null, false);
        }
    }

    private void PlayCollapse(EntityUid? portal)
    {
        if (!Deleted(portal))
            _audio.PlayPvs(CollapseSound, Transform(portal.Value).Coordinates);
    }
}
