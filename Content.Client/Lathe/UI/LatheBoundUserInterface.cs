using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Tag;
using Content.Shared.Weapons.Ranged.Components;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.Lathe.UI
{
    [UsedImplicitly]
    public sealed class LatheBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private LatheMenu? _menu;
        public LatheBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            _menu = this.CreateWindowCenteredRight<LatheMenu>();
            _menu.SetEntity(Owner);

            _menu.OnServerListButtonPressed += _ =>
            {
                SendMessage(new ConsoleServerSelectionMessage());
            };

            _menu.RecipeQueueAction += (recipe, amount) =>
            {
                SendMessage(new LatheQueueRecipeMessage(recipe, amount));
            };
            _menu.QueueDeleteAction += index => SendMessage(new LatheDeleteRequestMessage(index));
            _menu.QueueMoveUpAction += index => SendMessage(new LatheMoveRequestMessage(index, -1));
            _menu.QueueMoveDownAction += index => SendMessage(new LatheMoveRequestMessage(index, 1));
            _menu.DeleteFabricatingAction += () => SendMessage(new LatheAbortFabricationMessage());
        }

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            switch (state)
            {
                case LatheUpdateState msg:
                    if (_menu != null)
                    {
                        _menu.Recipes = msg.Recipes;
                        _menu.HighlightedRecipes = GatherHighlightedRecipes(msg.Recipes);
                    }
                    _menu?.PopulateRecipes();
                    _menu?.UpdateCategories();
                    _menu?.PopulateQueueList(msg.Queue);
                    _menu?.SetQueueInfo(msg.CurrentlyProducing);
                    break;
            }
        }

        private HashSet<ProtoId<LatheRecipePrototype>> GatherHighlightedRecipes(List<ProtoId<LatheRecipePrototype>> recipes)
        {
            var result = new HashSet<ProtoId<LatheRecipePrototype>>();

            var entities = IoCManager.Resolve<IEntityManager>();
            var players = IoCManager.Resolve<IPlayerManager>();
            var protos = IoCManager.Resolve<IPrototypeManager>();

            var player = players.LocalSession?.AttachedEntity;
            if (player == null)
                return result;

            var gunAmmoTags = new HashSet<ProtoId<TagPrototype>>();
            CollectGunAmmoTags(entities, player.Value, gunAmmoTags);

            if (gunAmmoTags.Count == 0)
                return result;

            var factory = entities.ComponentFactory;
            foreach (var recipeId in recipes)
            {
                if (!protos.TryIndex(recipeId, out LatheRecipePrototype? recipe))
                    continue;
                if (recipe.Result is not { } resultId)
                    continue;
                if (!protos.TryIndex(resultId, out EntityPrototype? resultProto))
                    continue;
                if (!resultProto.TryGetComponent(out TagComponent? tagComp, factory))
                    continue;
                if (tagComp.Tags.Any(t => gunAmmoTags.Contains(t)))
                    result.Add(recipeId);
            }

            return result;
        }

        private static void CollectGunAmmoTags(IEntityManager entities, EntityUid player, HashSet<ProtoId<TagPrototype>> tags)
        {
            var handsSys = entities.System<SharedHandsSystem>();
            if (entities.TryGetComponent<HandsComponent>(player, out var hands))
            {
                foreach (var held in handsSys.EnumerateHeld(new Entity<HandsComponent?>(player, hands)))
                    TryExtractGunTags(entities, held, tags);
            }

            var invSystem = entities.System<InventorySystem>();
            if (invSystem.TryGetSlots(player, out var slots))
            {
                foreach (var slot in slots)
                {
                    if (invSystem.TryGetSlotEntity(player, slot.Name, out var item) && item.HasValue)
                        TryExtractGunTags(entities, item.Value, tags);
                }
            }
        }

        private static void TryExtractGunTags(IEntityManager entities, EntityUid item, HashSet<ProtoId<TagPrototype>> tags)
        {
            if (!entities.HasComponent<GunComponent>(item))
                return;

            if (!entities.TryGetComponent<ItemSlotsComponent>(item, out var slots))
                return;

            foreach (var (_, slot) in slots.Slots)
            {
                if (slot.Whitelist?.Tags is { } whitelistTags)
                {
                    foreach (var tag in whitelistTags)
                        tags.Add(tag);
                }

                if (slot.Item is { } loaded && entities.TryGetComponent<TagComponent>(loaded, out var loadedTags))
                {
                    foreach (var tag in loadedTags.Tags)
                        tags.Add(tag);
                }
            }
        }
    }
}
