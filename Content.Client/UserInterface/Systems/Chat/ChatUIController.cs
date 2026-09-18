using Content.Client._FinalStand.Interface;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Administration.Managers;
using Content.Client.Chat;
using Content.Client.Chat.Managers;
using Content.Client.Chat.TypingIndicator;
using Content.Client.Chat.UI;
using Content.Client.Examine;
using Content.Client.Gameplay;
using Content.Client.Ghost;
using Content.Client.Mind;
using Content.Client.Roles;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Screens;
using Content.Client.UserInterface.Systems.Chat.Widgets;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Damage.ForceSay;
using Content.Shared.Decals;
using Content.Shared.Input;
using Content.Shared.Radio;
using Content.Shared.Roles.RoleCodeword;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects.Components.Localization;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Replays;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController : UIController
{
    [Dependency] private IClientAdminManager _admin = default!;
    [Dependency] private IChatManager _manager = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private IEyeManager _eye = default!;
    [Dependency] private IEntityManager _ent = default!;
    [Dependency] private IInputManager _input = default!;
    [Dependency] private IClientNetManager _net = default!;
    [Dependency] private IPlayerManager _player = default!;
    [Dependency] private IPrototypeManager _prototypeManager = default!;
    [Dependency] private IStateManager _state = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IReplayRecordingManager _replayRecording = default!;

    [UISystemDependency] private readonly ExamineSystem? _examine = default;
    [UISystemDependency] private readonly GhostSystem? _ghost = default;
    [UISystemDependency] private readonly TypingIndicatorSystem? _typingIndicator = default;
    [UISystemDependency] private readonly ChatSystem? _chatSys = default;
    [UISystemDependency] private readonly TransformSystem? _transform = default;
    [UISystemDependency] private readonly MindSystem? _mindSystem = default!;
    [UISystemDependency] private readonly RoleCodewordSystem? _roleCodewordSystem = default!;

    private static readonly ProtoId<ColorPalettePrototype> ChatNamePalette = "ChatNames";
    private string[] _chatNameColors = default!;
    private bool _chatNameColorsEnabled;

    private ISawmill _sawmill = default!;

    public static readonly Dictionary<char, ChatSelectChannel> PrefixToChannel = new()
    {
        {SharedChatSystem.LocalPrefix, ChatSelectChannel.Local},
        {SharedChatSystem.WhisperPrefix, ChatSelectChannel.Whisper},
        {SharedChatSystem.ConsolePrefix, ChatSelectChannel.Console},
        {SharedChatSystem.LOOCPrefix, ChatSelectChannel.LOOC},
        {SharedChatSystem.OOCPrefix, ChatSelectChannel.OOC},
        {SharedChatSystem.EmotesPrefix, ChatSelectChannel.Emotes},
        {SharedChatSystem.EmotesAltPrefix, ChatSelectChannel.Emotes},
        {SharedChatSystem.AdminPrefix, ChatSelectChannel.Admin},
        {SharedChatSystem.RadioCommonPrefix, ChatSelectChannel.Radio},
        {SharedChatSystem.DeadPrefix, ChatSelectChannel.Dead}
    };

    public static readonly Dictionary<ChatSelectChannel, char> ChannelPrefixes = new()
    {
        {ChatSelectChannel.Local, SharedChatSystem.LocalPrefix},
        {ChatSelectChannel.Whisper, SharedChatSystem.WhisperPrefix},
        {ChatSelectChannel.Console, SharedChatSystem.ConsolePrefix},
        {ChatSelectChannel.LOOC, SharedChatSystem.LOOCPrefix},
        {ChatSelectChannel.OOC, SharedChatSystem.OOCPrefix},
        {ChatSelectChannel.Emotes, SharedChatSystem.EmotesPrefix},
        {ChatSelectChannel.Admin, SharedChatSystem.AdminPrefix},
        {ChatSelectChannel.Radio, SharedChatSystem.RadioCommonPrefix},
        {ChatSelectChannel.Dead, SharedChatSystem.DeadPrefix}
    };

    private const int SingleBubbleCharLimit = 100;

    private const float BubbleDelayBase = 0.2f;

    private const float BubbleDelayFactor = 0.8f / SingleBubbleCharLimit;

    private const int SpeechBubbleCap = 4;

    private LayoutContainer _speechBubbleRoot = default!;

    private readonly Dictionary<EntityUid, List<SpeechBubble>> _activeSpeechBubbles =
        new();

    private readonly Dictionary<EntityUid, SpeechBubbleQueueData> _queuedSpeechBubbles
        = new();

    private readonly HashSet<ChatBox> _chats = new();
    public IReadOnlySet<ChatBox> Chats => _chats;

    public int MaxMessageLength => _config.GetCVar(CCVars.ChatMaxMessageLength);

    private readonly Dictionary<ChatChannel, int> _unreadMessages = new();

    // TODO add a cap for this for non-replays
    public readonly List<(GameTick Tick, ChatMessage Msg)> History = new();

    public ChatSelectChannel CanSendChannels { get; private set; }
    public ChatChannel FilterableChannels { get; private set; }
    public ChatSelectChannel SelectableChannels { get; private set; }
    private ChatSelectChannel PreferredChannel { get; set; } = ChatSelectChannel.OOC;

    public event Action<ChatSelectChannel>? CanSendChannelsChanged;
    public event Action<ChatChannel>? FilterableChannelsChanged;
    public event Action<ChatSelectChannel>? SelectableChannelsChanged;
    public event Action<ChatChannel, int?>? UnreadMessageCountsUpdated;
    public event Action<ChatMessage>? MessageAdded;

    public override void Initialize()
    {
        _sawmill = Logger.GetSawmill("chat");
        _sawmill.Level = LogLevel.Info;
        _admin.AdminStatusUpdated += UpdateChannelPermissions;
        _player.LocalPlayerAttached += OnAttachedChanged;
        _player.LocalPlayerDetached += OnAttachedChanged;
        _state.OnStateChanged += StateChanged;
        _net.RegisterNetMessage<MsgChatMessage>(OnChatMessage);
        _net.RegisterNetMessage<MsgDeleteChatMessagesBy>(OnDeleteChatMessagesBy);
        SubscribeNetworkEvent<DamageForceSayEvent>(OnDamageForceSay);
        _config.OnValueChanged(CCVars.ChatEnableColorName, (value) => { _chatNameColorsEnabled = value; });
        _chatNameColorsEnabled = _config.GetCVar(CCVars.ChatEnableColorName);

        _speechBubbleRoot = new LayoutContainer();

        UpdateChannelPermissions();

        _input.SetInputCommand(ContentKeyFunctions.FocusChat,
            InputCmdHandler.FromDelegate(_ => FocusChat()));

        _input.SetInputCommand(ContentKeyFunctions.FocusLocalChat,
            InputCmdHandler.FromDelegate(_ => FocusChannel(ChatSelectChannel.Local)));

        _input.SetInputCommand(ContentKeyFunctions.FocusEmote,
            InputCmdHandler.FromDelegate(_ => FocusChannel(ChatSelectChannel.Emotes)));

        _input.SetInputCommand(ContentKeyFunctions.FocusWhisperChat,
            InputCmdHandler.FromDelegate(_ => FocusChannel(ChatSelectChannel.Whisper)));

        _input.SetInputCommand(ContentKeyFunctions.FocusLOOC,
            InputCmdHandler.FromDelegate(_ => FocusChannel(ChatSelectChannel.LOOC)));

        _input.SetInputCommand(ContentKeyFunctions.FocusOOC,
            InputCmdHandler.FromDelegate(_ => FocusChannel(ChatSelectChannel.OOC)));

        _input.SetInputCommand(ContentKeyFunctions.FocusAdminChat,
            InputCmdHandler.FromDelegate(_ => FocusChannel(ChatSelectChannel.Admin)));

        _input.SetInputCommand(ContentKeyFunctions.FocusRadio,
            InputCmdHandler.FromDelegate(_ => FocusChannel(ChatSelectChannel.Radio)));

        _input.SetInputCommand(ContentKeyFunctions.FocusDeadChat,
            InputCmdHandler.FromDelegate(_ => FocusChannel(ChatSelectChannel.Dead)));

        _input.SetInputCommand(ContentKeyFunctions.FocusConsoleChat,
            InputCmdHandler.FromDelegate(_ => FocusChannel(ChatSelectChannel.Console)));

        _input.SetInputCommand(ContentKeyFunctions.CycleChatChannelForward,
            InputCmdHandler.FromDelegate(_ => CycleChatChannel(true)));

        _input.SetInputCommand(ContentKeyFunctions.CycleChatChannelBackward,
            InputCmdHandler.FromDelegate(_ => CycleChatChannel(false)));

        var gameplayStateLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayStateLoad.OnScreenLoad += OnScreenLoad;
        gameplayStateLoad.OnScreenUnload += OnScreenUnload;

        var nameColors = _prototypeManager.Index(ChatNamePalette).Colors.Values.ToArray();
        _chatNameColors = new string[nameColors.Length];
        for (var i = 0; i < nameColors.Length; i++)
        {
            _chatNameColors[i] = nameColors[i].ToHex();
        }

        _config.OnValueChanged(CCVars.ChatWindowOpacity, OnChatWindowOpacityChanged);

        InitializeHighlights();
    }

    public void OnScreenLoad()
    {
        SetMainChat(true);

        var viewportContainer = UIManager.ActiveScreen!.FindControl<LayoutContainer>("ViewportContainer");
        SetSpeechBubbleRoot(viewportContainer);

        SetChatWindowOpacity(_config.GetCVar(CCVars.ChatWindowOpacity));
    }

    public void OnScreenUnload()
    {
        SetMainChat(false);
    }

    private void OnChatWindowOpacityChanged(float opacity)
    {
        SetChatWindowOpacity(opacity);
    }

    private void SetChatWindowOpacity(float opacity)
    {
        var chatBox = UIManager.ActiveScreen?.GetWidget<ChatBox>() ?? UIManager.ActiveScreen?.GetWidget<ResizableChatBox>();

        var panel = chatBox?.ChatWindowPanel;
        if (panel is null)
            return;

        Color color;
        if (panel.PanelOverride is StyleBoxFlat styleBoxFlat)
            color = styleBoxFlat.BackgroundColor;
        else if (panel.TryGetStyleProperty<StyleBox>(PanelContainer.StylePropertyPanel, out var style)
                 && style is StyleBoxFlat propStyleBoxFlat)
            color = propStyleBoxFlat.BackgroundColor;
        else
            color = FSHudStyle.PanelBack;   // FINALSTAND: was #25252ADD, off the HUD palette

        // FINALSTAND: same fill and edge as every other HUD panel, so chat stops reading as a window borrowed from vanilla sitting on top of the HUD.
        panel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = color.WithAlpha(opacity),
            BorderColor = FSHudStyle.PanelEdge,
            BorderThickness = new Thickness(1),
        };
    }

    public void SetMainChat(bool setting)
    {
        if (UIManager.ActiveScreen == null)
        {
            return;
        }

        ChatBox chatBox;
        string? chatSizeRaw;

        switch (UIManager.ActiveScreen)
        {
            case DefaultGameScreen defaultScreen:
                chatBox = defaultScreen.ChatBox;
                chatSizeRaw = _config.GetCVar(CCVars.DefaultScreenChatSize);
                SetChatSizing(chatSizeRaw, defaultScreen, setting);
                break;
            case SeparatedChatGameScreen separatedScreen:
                chatBox = separatedScreen.ChatBox;
                chatSizeRaw = _config.GetCVar(CCVars.SeparatedScreenChatSize);
                SetChatSizing(chatSizeRaw, separatedScreen, setting);
                break;
            default:
                var maybeChat = UIManager.ActiveScreen.GetWidget<ChatBox>();

                chatBox = maybeChat ?? throw new Exception("Cannot get chat box in screen!");

                break;
        }

        chatBox.Main = setting;
    }

    private void SetChatSizing(string sizing, InGameScreen screen, bool setting)
    {
        if (!setting)
        {
            screen.OnChatResized -= StoreChatSize;
            return;
        }

        screen.OnChatResized += StoreChatSize;

        if (string.IsNullOrEmpty(sizing))
        {
            return;
        }

        var split = sizing.Split(",");

        var chatSize = new Vector2(
            float.Parse(split[0], CultureInfo.InvariantCulture),
            float.Parse(split[1], CultureInfo.InvariantCulture));

        screen.SetChatSize(chatSize);
    }

    private void StoreChatSize(Vector2 size)
    {
        if (UIManager.ActiveScreen == null)
        {
            throw new Exception("Cannot get active screen!");
        }

        var stringSize =
            $"{size.X.ToString(CultureInfo.InvariantCulture)},{size.Y.ToString(CultureInfo.InvariantCulture)}";
        switch (UIManager.ActiveScreen)
        {
            case DefaultGameScreen _:
                _config.SetCVar(CCVars.DefaultScreenChatSize, stringSize);
                break;
            case SeparatedChatGameScreen _:
                _config.SetCVar(CCVars.SeparatedScreenChatSize, stringSize);
                break;
            default:
                return;
        }

        _config.SaveToFile();
    }

    private void FocusChat()
    {
        foreach (var chat in _chats)
        {
            if (!chat.Main)
                continue;

            chat.Focus();
            break;
        }
    }

    private void FocusChannel(ChatSelectChannel channel)
    {
        foreach (var chat in _chats)
        {
            if (!chat.Main)
                continue;

            chat.Focus(channel);
            break;
        }
    }

    private void CycleChatChannel(bool forward)
    {
        foreach (var chat in _chats)
        {
            if (!chat.Main)
                continue;

            chat.CycleChatChannel(forward);
            break;
        }
    }

    private void StateChanged(StateChangedEventArgs args)
    {
        if (args.NewState is GameplayState)
        {
            PreferredChannel = ChatSelectChannel.Local;
        }

        UpdateChannelPermissions();
    }

    public void SetSpeechBubbleRoot(LayoutContainer root)
    {
        _speechBubbleRoot.Orphan();
        root.AddChild(_speechBubbleRoot);
        LayoutContainer.SetAnchorPreset(_speechBubbleRoot, LayoutContainer.LayoutPreset.Wide);
        _speechBubbleRoot.SetPositionLast();
    }

    private void OnAttachedChanged(EntityUid uid)
    {
        UpdateChannelPermissions();

        UpdateAutoFillHighlights();
    }

    private void AddSpeechBubble(ChatMessage msg, SpeechBubble.SpeechType speechType)
    {
        var ent = EntityManager.GetEntity(msg.SenderEntity);

        if (!EntityManager.EntityExists(ent))
        {
            _sawmill.Debug("Got local chat message with invalid sender entity: {0}", msg.SenderEntity);
            return;
        }

        EnqueueSpeechBubble(ent, msg, speechType);
    }

    private void CreateSpeechBubble(EntityUid entity, SpeechBubbleData speechData)
    {
        var bubble =
            SpeechBubble.CreateSpeechBubble(speechData.Type, speechData.Message, entity);

        bubble.OnDied += SpeechBubbleDied;

        if (_activeSpeechBubbles.TryGetValue(entity, out var existing))
        {
            foreach (var existingBubble in existing)
            {
                existingBubble.VerticalOffset += bubble.ContentSize.Y;
            }
        }
        else
        {
            existing = new List<SpeechBubble>();
            _activeSpeechBubbles.Add(entity, existing);
        }

        existing.Add(bubble);
        _speechBubbleRoot.AddChild(bubble);

        if (existing.Count > SpeechBubbleCap)
        {
            var last = existing[^(SpeechBubbleCap + 1)];
            last.FadeNow();
        }
    }

    private void SpeechBubbleDied(EntityUid entity, SpeechBubble bubble)
    {
        RemoveSpeechBubble(entity, bubble);
    }

    private void EnqueueSpeechBubble(EntityUid entity, ChatMessage message, SpeechBubble.SpeechType speechType)
    {
        // Don't enqueue speech bubbles for other maps.
        if (EntityManager.GetComponent<TransformComponent>(entity).MapID != _eye.CurrentEye.Position.MapId)
            return;

        if (!_queuedSpeechBubbles.TryGetValue(entity, out var queueData))
        {
            queueData = new SpeechBubbleQueueData();
            _queuedSpeechBubbles.Add(entity, queueData);
        }

        queueData.MessageQueue.Enqueue(new SpeechBubbleData(message, speechType));
    }

    public void RemoveSpeechBubble(EntityUid entityUid, SpeechBubble bubble)
    {
        bubble.Dispose();

        var list = _activeSpeechBubbles[entityUid];
        list.Remove(bubble);

        if (list.Count == 0)
        {
            _activeSpeechBubbles.Remove(entityUid);
        }
    }

    private void UpdateChannelPermissions()
    {
        CanSendChannels = default;
        FilterableChannels = default;

        CanSendChannels |= ChatSelectChannel.Console;

        CanSendChannels |= ChatSelectChannel.OOC;
        CanSendChannels |= ChatSelectChannel.LOOC;
        FilterableChannels |= ChatChannel.OOC;
        FilterableChannels |= ChatChannel.LOOC;

        FilterableChannels |= ChatChannel.Server;

        if (_state.CurrentState is GameplayStateBase)
        {
            FilterableChannels |= ChatChannel.Local;
            FilterableChannels |= ChatChannel.Whisper;
            FilterableChannels |= ChatChannel.Radio;
            FilterableChannels |= ChatChannel.Emotes;
            FilterableChannels |= ChatChannel.Notifications;

            // Can only send local / radio / emote when attached to a non-ghost entity.
            if (_ghost is not {IsGhost: true})
            {
                CanSendChannels |= ChatSelectChannel.Local;
                CanSendChannels |= ChatSelectChannel.Whisper;
                CanSendChannels |= ChatSelectChannel.Radio;
                CanSendChannels |= ChatSelectChannel.Emotes;
            }
        }

        if (_admin.HasFlag(AdminFlags.Admin) || _ghost is {IsGhost: true})
        {
            FilterableChannels |= ChatChannel.Dead;
            CanSendChannels |= ChatSelectChannel.Dead;
        }

        if (_admin.HasFlag(AdminFlags.Adminchat))
        {
            FilterableChannels |= ChatChannel.Admin;
            FilterableChannels |= ChatChannel.AdminAlert;
            FilterableChannels |= ChatChannel.AdminChat;
            CanSendChannels |= ChatSelectChannel.Admin;
        }

        SelectableChannels = CanSendChannels;

        DebugTools.Assert((CanSendChannels & ChatSelectChannel.OOC) != 0, "OOC must always be available");
        DebugTools.Assert((FilterableChannels & ChatChannel.OOC) != 0, "OOC must always be available");
        DebugTools.Assert((SelectableChannels & ChatSelectChannel.OOC) != 0, "OOC must always be available");

        CanSendChannelsChanged?.Invoke(CanSendChannels);
        FilterableChannelsChanged?.Invoke(FilterableChannels);
        SelectableChannelsChanged?.Invoke(SelectableChannels);
    }

    public void ClearUnfilteredUnreads(ChatChannel channels)
    {
        foreach (var channel in _unreadMessages.Keys.ToArray())
        {
            if ((channels & channel) == 0)
                continue;

            _unreadMessages[channel] = 0;
            UnreadMessageCountsUpdated?.Invoke(channel, 0);
        }
    }

    public override void FrameUpdate(FrameEventArgs delta)
    {
        UpdateQueuedSpeechBubbles(delta);
    }

    private void UpdateQueuedSpeechBubbles(FrameEventArgs delta)
    {
        if (_queuedSpeechBubbles.Count == 0 || _examine == null)
        {
            return;
        }

        foreach (var (entity, queueData) in _queuedSpeechBubbles.ShallowClone())
        {
            if (!EntityManager.EntityExists(entity))
            {
                _queuedSpeechBubbles.Remove(entity);
                continue;
            }

            queueData.TimeLeft -= delta.DeltaSeconds;
            if (queueData.TimeLeft > 0)
            {
                continue;
            }

            if (queueData.MessageQueue.Count == 0)
            {
                _queuedSpeechBubbles.Remove(entity);
                continue;
            }

            var msg = queueData.MessageQueue.Dequeue();

            queueData.TimeLeft += BubbleDelayBase + msg.Message.Message.Length * BubbleDelayFactor;

            CreateSpeechBubble(entity, msg);
        }

        var player = _player.LocalEntity;
        var predicate = static (EntityUid uid, (EntityUid compOwner, EntityUid? attachedEntity) data)
            => uid == data.compOwner || uid == data.attachedEntity;
        var playerPos = player != null
            ? _eye.CurrentEye.Position
            : MapCoordinates.Nullspace;

        var occluded = player != null && _examine.IsOccluded(player.Value);

        foreach (var (ent, bubs) in _activeSpeechBubbles)
        {
            if (EntityManager.Deleted(ent))
            {
                SetBubbles(bubs, false);
                continue;
            }

            if (ent == player)
            {
                SetBubbles(bubs, true);
                continue;
            }

            var otherPos = _transform?.GetMapCoordinates(ent) ?? MapCoordinates.Nullspace;

            if (occluded && !_examine.InRangeUnOccluded(
                    playerPos,
                    otherPos, 0f,
                    (ent, player), predicate))
            {
                SetBubbles(bubs, false);
                continue;
            }

            SetBubbles(bubs, true);
        }
    }

    private void SetBubbles(List<SpeechBubble> bubbles, bool visible)
    {
        foreach (var bubble in bubbles)
        {
            bubble.Visible = visible;
        }
    }

    public ChatSelectChannel MapLocalIfGhost(ChatSelectChannel channel)
    {
        if (channel == ChatSelectChannel.Local && _ghost is {IsGhost: true})
            return ChatSelectChannel.Dead;

        return channel;
    }

    private bool TryGetRadioChannel(string text, out RadioChannelPrototype? radioChannel)
    {
        radioChannel = null;
        return _player.LocalEntity is EntityUid { Valid: true } uid
           && _chatSys != null
           && _chatSys.TryProcessRadioMessage(uid, text, out _, out radioChannel, quiet: true);
    }

    public void UpdateSelectedChannel(ChatBox box)
    {
        var (prefixChannel, _, radioChannel) = SplitInputContents(box.ChatInput.Input.Text.ToLower());

        if (prefixChannel == ChatSelectChannel.None)
            box.ChatInput.ChannelSelector.UpdateChannelSelectButton(box.SelectedChannel, null);
        else
            box.ChatInput.ChannelSelector.UpdateChannelSelectButton(prefixChannel, radioChannel);
    }

    public (ChatSelectChannel chatChannel, string text, RadioChannelPrototype? radioChannel) SplitInputContents(string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return (ChatSelectChannel.None, text, null);

        ChatSelectChannel chatChannel;
        if (TryGetRadioChannel(text, out var radioChannel))
            chatChannel = ChatSelectChannel.Radio;
        else
            chatChannel = PrefixToChannel.GetValueOrDefault(text[0]);

        if ((CanSendChannels & chatChannel) == 0)
            return (ChatSelectChannel.None, text, null);

        if (chatChannel == ChatSelectChannel.Radio)
            return (chatChannel, text, radioChannel);

        if (chatChannel == ChatSelectChannel.Local)
        {
            if (_ghost?.IsGhost != true)
                return (chatChannel, text, null);
            else
                chatChannel = ChatSelectChannel.Dead;
        }

        return (chatChannel, text[1..].TrimStart(), null);
    }

    public void SendMessage(ChatBox box, ChatSelectChannel channel)
    {
        _typingIndicator?.ClientSubmittedChatText();

        var text = box.ChatInput.Input.Text;
        box.ChatInput.Input.Clear();
        box.ChatInput.Input.ReleaseKeyboardFocus();
        UpdateSelectedChannel(box);

        if (string.IsNullOrWhiteSpace(text))
            return;

        (var prefixChannel, text, var _) = SplitInputContents(text);

        if (text.Length > MaxMessageLength)
        {
            var locWarning = Loc.GetString("chat-manager-max-message-length",
                ("maxMessageLength", MaxMessageLength));
            box.AddLine(locWarning, Color.Orange);
            return;
        }

        if (prefixChannel != ChatSelectChannel.None)
            channel = prefixChannel;
        else if (channel == ChatSelectChannel.Radio)
        {
            text = $";{text}";
        }

        _manager.SendMessage(text, prefixChannel == 0 ? channel : prefixChannel);
    }

    private void OnDamageForceSay(DamageForceSayEvent ev, EntitySessionEventArgs _)
    {
        var chatBox = UIManager.ActiveScreen?.GetWidget<ChatBox>() ?? UIManager.ActiveScreen?.GetWidget<ResizableChatBox>();
        if (chatBox == null)
            return;

        var msg = chatBox.ChatInput.Input.Text.TrimEnd();

        var allowedChannels = ChatSelectChannel.Local | ChatSelectChannel.Whisper;
        if ((chatBox.SelectedChannel & allowedChannels) == ChatSelectChannel.None)
            return;

        var prefixChannel = SplitInputContents(msg).chatChannel;
        if (prefixChannel != ChatSelectChannel.None && (prefixChannel & allowedChannels) == ChatSelectChannel.None)
            return;

        if (_player.LocalSession?.AttachedEntity is not { } ent
            || !EntityManager.TryGetComponent<DamageForceSayComponent>(ent, out var forceSay))
            return;

        if (string.IsNullOrWhiteSpace(msg))
            return;

        var modifiedText = ev.Suffix != null
            ? Loc.GetString(forceSay.ForceSayMessageWrap,
                ("message", msg), ("suffix", ev.Suffix))
            : Loc.GetString(forceSay.ForceSayMessageWrapNoSuffix,
                ("message", msg));

        chatBox.ChatInput.Input.SetText(modifiedText);
        chatBox.ChatInput.Input.ForceSubmitText();
    }

    private void OnChatMessage(MsgChatMessage message)
    {
        var msg = message.Message;
        ProcessChatMessage(msg);

        if ((msg.Channel & ChatChannel.AdminRelated) == 0 ||
            _config.GetCVar(CCVars.ReplayRecordAdminChat))
        {
            _replayRecording.RecordClientMessage(msg);
        }
    }

    public void ProcessChatMessage(ChatMessage msg, bool speechBubble = true)
    {
        if ((msg.Channel == ChatChannel.Local || msg.Channel == ChatChannel.Whisper) && _chatNameColorsEnabled)
        {
            var grammar = _ent.GetComponentOrNull<GrammarComponent>(_ent.GetEntity(msg.SenderEntity));
            if (grammar != null && grammar.ProperNoun == true)
                msg.WrappedMessage = SharedChatSystem.InjectTagInsideTag(msg, "Name", "color", GetNameColor(SharedChatSystem.GetStringInsideTag(msg, "Name")));
        }

        foreach (var highlight in _highlights)
        {
            msg.WrappedMessage = SharedChatSystem.InjectTagAroundString(msg, highlight, "color", _highlightsColor);
        }

        if (_player.LocalUser != null && _mindSystem != null && _roleCodewordSystem != null)
        {
            if (_mindSystem.TryGetMind(_player.LocalUser.Value, out var mindId) && _ent.TryGetComponent(mindId, out RoleCodewordComponent? codewordComp))
            {
                foreach (var (_, codewordData) in codewordComp.RoleCodewords)
                {
                    foreach (string codeword in codewordData.Codewords)
                        msg.WrappedMessage = SharedChatSystem.InjectTagAroundString(msg, codeword, "color", codewordData.Color.ToHex());
                }
            }
        }

        if (!msg.HideChat)
        {
            History.Add((_timing.CurTick, msg));
            MessageAdded?.Invoke(msg);

            if (!msg.Read)
            {
                _sawmill.Debug($"Message filtered: {msg.Channel}: {msg.Message}");
                if (!_unreadMessages.TryGetValue(msg.Channel, out var count))
                    count = 0;

                count += 1;
                _unreadMessages[msg.Channel] = count;
                UnreadMessageCountsUpdated?.Invoke(msg.Channel, count);
            }
        }

        if (!speechBubble || msg.SenderEntity == default)
            return;

        switch (msg.Channel)
        {
            case ChatChannel.Local:
                AddSpeechBubble(msg, SpeechBubble.SpeechType.Say);
                break;

            case ChatChannel.Whisper:
                AddSpeechBubble(msg, SpeechBubble.SpeechType.Whisper);
                break;

            case ChatChannel.Dead:
                if (_ghost is not { IsGhost: true})
                    break;

                if (_ghost.GhostVisibility)
                    AddSpeechBubble(msg, SpeechBubble.SpeechType.Say);
                break;

            case ChatChannel.Emotes:
                AddSpeechBubble(msg, SpeechBubble.SpeechType.Emote);
                break;

            case ChatChannel.LOOC:
                if (_config.GetCVar(CCVars.LoocAboveHeadShow))
                    AddSpeechBubble(msg, SpeechBubble.SpeechType.Looc);
                break;
        }
    }

    public void OnDeleteChatMessagesBy(MsgDeleteChatMessagesBy msg)
    {
        History.RemoveAll(h => h.Msg.SenderKey == msg.Key || msg.Entities.Contains(h.Msg.SenderEntity));
        Repopulate();
    }

    public void RegisterChat(ChatBox chat)
    {
        _chats.Add(chat);
    }

    public void UnregisterChat(ChatBox chat)
    {
        _chats.Remove(chat);
    }

    public ChatSelectChannel GetPreferredChannel()
    {
        return MapLocalIfGhost(PreferredChannel);
    }

    public void NotifyChatTextChange()
    {
        _typingIndicator?.ClientChangedChatText();
    }

    public void NotifyChatFocus(bool isFocused)
    {
        _typingIndicator?.ClientChangedChatFocus(isFocused);
    }

    public void Repopulate()
    {
        foreach (var chat in _chats)
        {
            chat.Repopulate();
        }
    }

    public string GetNameColor(string name)
    {
        var colorIdx = Math.Abs(name.GetHashCode() % _chatNameColors.Length);
        return _chatNameColors[colorIdx];
    }

    private readonly record struct SpeechBubbleData(ChatMessage Message, SpeechBubble.SpeechType Type);

    private sealed class SpeechBubbleQueueData
    {
        public float TimeLeft { get; set; }

        public Queue<SpeechBubbleData> MessageQueue { get; } = new();
    }
}
