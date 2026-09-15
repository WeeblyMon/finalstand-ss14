using System.Numerics;
using Content.Client.Cooldown;
using Content.Client.UserInterface.Systems.Inventory.Controls;
using Content.Shared._FinalStand.Deployables;
using Content.Shared._FinalStand.Grenades;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.UserInterface.Controls
{
    public abstract class SlotControl : Control, IEntityControl
    {
        // FINALSTAND: 32 to match the HUD's grid; hands override themselves back up to a clean 2x.
        public static int DefaultButtonSize = 32;
        private const float SourceArtSize = 32f;

        public TextureRect ButtonRect { get; }
        public TextureRect BlockedRect { get; }
        public TextureRect HighlightRect { get; }
        public SpriteView HoverSpriteView { get; }
        public Control AdminOverlays { get; }
        public TextureButton StorageButton { get; }
        public CooldownGraphic CooldownDisplay { get; }

        private SpriteView SpriteView { get; }
        private EntityPrototypeView ProtoView { get; }

        private readonly Label _chargesLabel;
        private IEntityManager? _entMan;

        public EntityUid? Entity => SpriteView.Entity;

        private bool _slotNameSet;

        private string _slotName = "";
        public string SlotName
        {
            get => _slotName;
            set
            {
                //this auto registers the button with it's parent container when it's set
                if (_slotNameSet)
                {
                    Logger.Warning("Tried to set slotName after init for:" + Name);
                    return;
                }
                _slotNameSet = true;
                if (Parent is IItemslotUIContainer container)
                {
                    container.TryRegisterButton(this, value);
                }
                Name = "SlotButton_" + value;
                _slotName = value;
            }
        }

        public bool Highlight { get => HighlightRect.Visible; set => HighlightRect.Visible = value;}

        public bool Blocked { get => BlockedRect.Visible; set => BlockedRect.Visible = value;}

        private string? _blockedTexturePath;
        public string? BlockedTexturePath
        {
            get => _blockedTexturePath;
            set
            {
                _blockedTexturePath = value;
                BlockedRect.Texture = Theme.ResolveTextureOrNull(_blockedTexturePath)?.Texture;
            }
        }

        private string? _buttonTexturePath;
        public string? ButtonTexturePath
        {
            get => _buttonTexturePath;
            set
            {
                _buttonTexturePath = value;
                UpdateButtonTexture();
            }
        }

        private string? _fullButtonTexturePath;
        public string? FullButtonTexturePath
        {
            get => _fullButtonTexturePath;
            set
            {
                _fullButtonTexturePath = value;
                UpdateButtonTexture();
            }
        }


        private string? _storageTexturePath;
        public string? StorageTexturePath
        {
            get => _buttonTexturePath;
            set
            {
                _storageTexturePath = value;
                StorageButton.TextureNormal = Theme.ResolveTextureOrNull(_storageTexturePath)?.Texture;
            }
        }

        private string? _highlightTexturePath;
        public string? HighlightTexturePath
        {
            get => _highlightTexturePath;
            set
            {
                _highlightTexturePath = value;
                HighlightRect.Texture = Theme.ResolveTextureOrNull(_highlightTexturePath)?.Texture;
            }
        }

        public event Action<GUIBoundKeyEventArgs, SlotControl>? Pressed;
        public event Action<GUIBoundKeyEventArgs, SlotControl>? Unpressed;
        public event Action<GUIBoundKeyEventArgs, SlotControl>? StoragePressed;
        public event Action<GUIMouseHoverEventArgs, SlotControl>? Hover;

        public bool EntityHover => HoverSpriteView.Sprite != null;
        public bool MouseIsHovering;

        private Label? FSLabelControl;

        /// <summary>
        /// Turns the slot into a labelled button: the word replaces the glyph, and the button
        /// graphic stretches to whatever width the word needs rather than staying a square.
        /// </summary>
        public string FSLabel
        {
            set
            {
                if (FSLabelControl == null)
                    return;

                FSLabelControl.Text = value;
                FSLabelControl.Visible = !string.IsNullOrEmpty(value);

                if (!FSLabelControl.Visible)
                    return;

                // Stretch, so the backing texture fills the control instead of drawing a square in
                // the middle of it - that mismatch is what left the word hanging over the edges.
                ButtonRect.Stretch = TextureRect.StretchMode.Scale;
                HighlightRect.Stretch = TextureRect.StretchMode.Scale;
                ButtonRect.SetSize = Vector2.Zero;
                ButtonRect.HorizontalExpand = true;
                ButtonRect.VerticalExpand = true;

                SpriteView.Visible = false;
                HoverSpriteView.Visible = false;

                var width = FSLabelControl.MinSize.X + FSLabelTextPad * 2f;
                MinSize = new Vector2(MathF.Max(MinSize.X, width), MinSize.Y);
            }
        }

        private const float FSLabelTextPad = 8f;

        /// <summary>Resizes this slot. Only integer multiples of 32 stay sharp.</summary>
        public void SetButtonSize(int size)
        {
            var scale = size / SourceArtSize;
            MinSize = new Vector2(size, size);
            ButtonRect.TextureScale = new Vector2(scale, scale);
            HighlightRect.TextureScale = new Vector2(scale, scale);

            foreach (var view in new SpriteView[] { SpriteView, ProtoView, HoverSpriteView })
            {
                view.Scale = new Vector2(scale, scale);
                view.SetSize = new Vector2(size, size);
            }
        }

        public SlotControl()
        {
            IoCManager.InjectDependencies(this);
            Name = "SlotButton_null";

            // FINALSTAND: art is authored at 32, so scale is the slot size over 32 rather than a
            // hardcoded 2. That keeps every slot size an integer multiple - the engine has no
            // mipmaps, so a fractional scale is the blur.
            var scale = DefaultButtonSize / SourceArtSize;

            MinSize = new Vector2(DefaultButtonSize, DefaultButtonSize);
            AddChild(ButtonRect = new TextureRect
            {
                TextureScale = new Vector2(scale, scale),
                MouseFilter = MouseFilterMode.Stop
            });
            AddChild(HighlightRect = new TextureRect
            {
                Visible = false,
                TextureScale = new Vector2(scale, scale),
                MouseFilter = MouseFilterMode.Ignore
            });

            ButtonRect.OnKeyBindDown += OnButtonPressed;
            ButtonRect.OnKeyBindUp += OnButtonUnpressed;

            AddChild(SpriteView = new SpriteView
            {
                Scale = new Vector2(scale, scale),
                SetSize = new Vector2(DefaultButtonSize, DefaultButtonSize),
                OverrideDirection = Direction.South
            });
            AddChild(ProtoView = new EntityPrototypeView
            {
                Visible = false,
                Scale = new Vector2(scale, scale),
                SetSize = new Vector2(DefaultButtonSize, DefaultButtonSize),
                OverrideDirection = Direction.South
            });

            AddChild(HoverSpriteView = new SpriteView
            {
                Scale = new Vector2(scale, scale),
                SetSize = new Vector2(DefaultButtonSize, DefaultButtonSize),
                OverrideDirection = Direction.South
            });

            AddChild(StorageButton = new TextureButton
            {
                Scale = new Vector2(0.75f, 0.75f),
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Bottom,
                Visible = false,
            });

            AddChild(AdminOverlays = new Control());

            AddChild(FSLabelControl = new Label
            {
                Visible = false,
                HorizontalAlignment = HAlignment.Center,
                VerticalAlignment = VAlignment.Center,
                ModulateSelfOverride = Color.FromHex("#8FA1B3"),
            });

            StorageButton.OnKeyBindDown += args =>
            {
                if (args.Function != EngineKeyFunctions.UIClick)
                {
                    OnButtonPressed(args);
                }
            };

            StorageButton.OnPressed += OnStorageButtonPressed;

            ButtonRect.OnMouseEntered += _ =>
            {
                MouseIsHovering = true;
            };
            ButtonRect.OnMouseEntered += OnButtonHover;

            ButtonRect.OnMouseExited += _ =>
            {
                MouseIsHovering = false;
                ClearHover();
            };

            AddChild(CooldownDisplay = new CooldownGraphic
            {
                Visible = false,
            });

            AddChild(BlockedRect = new TextureRect
            {
                TextureScale = new Vector2(2, 2),
                MouseFilter = MouseFilterMode.Stop,
                Visible = false
            });

            HighlightTexturePath = "slot_highlight";
            BlockedTexturePath = "blocked";

            AddChild(_chargesLabel = new Label
            {
                Name = "ChargesLabel",
                HorizontalAlignment = HAlignment.Right,
                VerticalAlignment = VAlignment.Bottom,
                Margin = new Thickness(0, 0, 2, 2),
                Visible = false,
            });
        }

        protected override void FrameUpdate(FrameEventArgs args)
        {
            base.FrameUpdate(args);

            _entMan ??= IoCManager.Resolve<IEntityManager>();
            if (Entity is { } ent && _entMan.TryGetComponent<FSGrenadePackComponent>(ent, out var pack))
            {
                _chargesLabel.Text = pack.Stock.ToString();
                _chargesLabel.Visible = true;
                _chargesLabel.FontColorOverride = pack.Stock == 0 ? Color.Gray : Color.White;
            }
            else if (Entity is { } deployableEnt && _entMan.TryGetComponent<FSDeployableItemComponent>(deployableEnt, out var deployable))
            {
                _chargesLabel.Text = deployable.Stock.ToString();
                _chargesLabel.Visible = true;
                _chargesLabel.FontColorOverride = deployable.Stock == 0 ? Color.Gray : Color.White;
            }
            else
            {
                _chargesLabel.Visible = false;
            }
        }

        public void ClearHover()
        {
            if (!EntityHover)
                return;

            var tempQualifier = HoverSpriteView.Entity;
            if (tempQualifier != null)
            {
                IoCManager.Resolve<IEntityManager>().QueueDeleteEntity(tempQualifier);
            }

            HoverSpriteView.SetEntity(null);
        }

        /// <summary>
        /// Causes the control to display a placeholder prototype, optionally faded
        /// </summary>
        public void SetEntity(EntityUid? ent)
        {
            SpriteView.SetEntity(ent);
            SpriteView.Visible = true;
            ProtoView.Visible = false;
            UpdateButtonTexture();
        }

        /// <summary>
        /// Add an overlay to in the admin overlays location
        /// </summary>
        /// <param name="texturePath">The texture path to overlay.</param>
        /// <param name="color">Color to modulate the texture with - if null no modulation.</param>
        public void AddAdminOverlay(ResPath texturePath, Color? color = null)
        {
            AdminOverlays.AddChild(new SimpleSlotOverlay(texturePath.CanonPath, color));
        }

        /// <summary>
        /// Causes the control to display a placeholder prototype, optionally faded
        /// </summary>
        public void SetPrototype(EntProtoId? proto, bool fade)
        {
            ProtoView.SetPrototype(proto);
            SpriteView.Visible = false;
            ProtoView.Visible = true;

            UpdateButtonTexture();

            if (ProtoView.Entity is not { } ent || !fade)
                return;

            var sprites = IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<SpriteSystem>();
            sprites.SetColor((ent.Owner, ent.Comp1), Color.DarkGray.WithAlpha(0.65f));
        }

        private void UpdateButtonTexture()
        {
            var fullTexture = Theme.ResolveTextureOrNull(_fullButtonTexturePath);
            var texture = Entity.HasValue && fullTexture != null
                ? fullTexture.Texture
                : Theme.ResolveTextureOrNull(_buttonTexturePath)?.Texture;
            ButtonRect.Texture = texture;
        }

        private void OnButtonPressed(GUIBoundKeyEventArgs args)
        {
            Pressed?.Invoke(args, this);
        }

        private void OnButtonUnpressed(GUIBoundKeyEventArgs args)
        {
            Unpressed?.Invoke(args, this);
        }

        private void OnStorageButtonPressed(BaseButton.ButtonEventArgs args)
        {
            if (args.Event.Function == EngineKeyFunctions.UIClick)
            {
                StoragePressed?.Invoke(args.Event, this);
            }
            else
            {
                Pressed?.Invoke(args.Event, this);
            }
        }

        private void OnButtonHover(GUIMouseHoverEventArgs args)
        {
            Hover?.Invoke(args, this);
        }

        protected override void OnThemeUpdated()
        {
            base.OnThemeUpdated();

            StorageButton.TextureNormal = Theme.ResolveTextureOrNull(_storageTexturePath)?.Texture;
            HighlightRect.Texture = Theme.ResolveTextureOrNull(_highlightTexturePath)?.Texture;
            UpdateButtonTexture();
        }

        EntityUid? IEntityControl.UiEntity => Entity;
    }
}
