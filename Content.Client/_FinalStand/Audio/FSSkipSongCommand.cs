using Content.Client.Audio;
using Robust.Shared.Console;

namespace Content.Client._FinalStand.Audio;

public sealed partial class FSSkipSongCommand : LocalizedEntityCommands
{
    [Dependency] private ContentAudioSystem _audio = default!;

    public override string Command => "skipsong";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!_audio.SkipLobbySong())
            shell.WriteError(Loc.GetString("cmd-skipsong-none"));
    }
}
