using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Robust.Shared.Utility;

namespace Content.Server._FinalStand.Chat;

public static class FSChatExtensions
{
    public static void DispatchServerMarkupAnnouncement(this IChatManager chat, string markup, Color? colorOverride = null)
    {
        var plain = FormattedMessage.RemoveMarkupPermissive(markup);
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", markup));
        chat.ChatMessageToAll(ChatChannel.Server, plain, wrapped, EntityUid.Invalid, hideChat: false, recordReplay: true, colorOverride: colorOverride);
    }
}
