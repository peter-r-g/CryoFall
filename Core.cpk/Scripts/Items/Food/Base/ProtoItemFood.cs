﻿namespace AtomicTorch.CBND.CoreMod.Items.Food
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects;
    using AtomicTorch.CBND.CoreMod.CharacterStatusEffects.Debuffs;
    using AtomicTorch.CBND.CoreMod.Helpers.Client;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Stats;
    using AtomicTorch.CBND.CoreMod.Systems.ItemFreshnessSystem;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Items.Controls.Tooltips;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.GameEngine.Common.Helpers;

    /// <summary>
    /// Item prototype for food items.
    /// </summary>
    public abstract class ProtoItemFood
        <TPrivateState,
         TPublicState,
         TClientState>
        : ProtoItemWithFreshness
          <TPrivateState,
              TPublicState,
              TClientState>,
          IProtoItemFood
        where TPrivateState : BasePrivateState, IItemWithFreshnessPrivateState, new()
        where TPublicState : BasePublicState, new()
        where TClientState : BaseClientState, new()
    {
        public override bool CanBeSelectedInVehicle => true;

        public IReadOnlyList<EffectAction> Effects { get; private set; }

        public virtual float FoodRestore => 0;

        public virtual bool IsAvailableInCompletionist => true;

        public virtual string ItemUseCaption => ItemUseCaptions.Eat;

        /// <summary>
        /// For all food items the default value is "Tiny" stack size.
        /// </summary>
        public override ushort MaxItemsPerStack => ItemStackSize.Small;

        public abstract ushort OrganicValue { get; }

        public virtual float StaminaRestore => 0;

        public virtual float WaterRestore => 0;

        protected override bool ClientItemUseFinish(ClientItemData data)
        {
            var character = Client.Characters.CurrentPlayerCharacter;
            var item = data.Item;
            var stats = PlayerCharacter.GetPublicState(character).CurrentStatsExtended;
            if (!this.SharedCanEat(
                    new ItemEatData(item,
                                    character,
                                    stats,
                                    ItemFreshnessSystem.SharedGetFreshnessEnum(item))))
            {
                return false;
            }

            this.CallServer(_ => _.ServerRemote_Eat(item));
            return true;
        }

        protected override void ClientTooltipCreateControlsInternal(IItem item, List<UIElement> controls)
        {
            base.ClientTooltipCreateControlsInternal(item, controls);

            if (this.FoodRestore != 0
                || this.StaminaRestore != 0
                || this.WaterRestore != 0)
            {
                var tooltip = ItemTooltipFoodNutrition.Create(this);
                tooltip.Margin = new Thickness(0, 3, 0, 3);
                controls.Add(tooltip);
            }

            if (this.Effects.Count > 0)
            {
                controls.Add(ItemTooltipInfoEffectActionsControl.Create(this.Effects));
            }

            // display a "Never consumed" message if this food is available in Completionist menu
            if (this.IsAvailableInCompletionist)
            {
                var isUnlockedInCompletionist = false;
                foreach (var entry in ClientCurrentCharacterHelper.PrivateState.CompletionistData.ListFood)
                {
                    if (ReferenceEquals(entry.Prototype, this))
                    {
                        isUnlockedInCompletionist = true;
                        break;
                    }
                }

                if (!isUnlockedInCompletionist)
                {
                    controls.Add(
                        new TextBlock()
                        {
                            Text = "(" + ItemHints.NeverConsumed + ")",
                            TextWrapping = TextWrapping.Wrap,
                            FontWeight = FontWeights.Bold,
                            Foreground = Api.Client.UI.GetApplicationResource<Brush>("BrushColor4")
                        });
                }
            }
        }

        protected override string GenerateIconPath()
        {
            return "Items/Food/" + this.GetType().Name;
        }

        protected virtual void PrepareEffects(EffectActionsList effects)
        {
        }

        protected override void PrepareHints(List<string> hints)
        {
            base.PrepareHints(hints);
            hints.Add(ItemHints.AltClickToUseItem);
        }

        protected virtual void PrepareProtoItemFood()
        {
        }

        protected sealed override void PrepareProtoItemWithFreshness()
        {
            base.PrepareProtoItemWithFreshness();
            this.PrepareProtoItemFood();

            var effects = new EffectActionsList();
            this.PrepareEffects(effects);
            this.Effects = effects.ToReadOnly();
        }

        protected override ReadOnlySoundPreset<ItemSound> PrepareSoundPresetItem()
        {
            return ItemsSoundPresets.ItemFood;
        }

        protected override void ServerInitialize(ServerInitializeData data)
        {
            base.ServerInitialize(data);
            ItemFreshnessSystem.ServerInitializeItem(data.PrivateState, data.IsFirstTimeInit);
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        protected virtual void ServerOnEat(ItemEatData data)
        {
            var freshnessCoef = ItemFreshnessSystem.SharedGetFreshnessPositiveEffectsCoef(data.Freshness);

            data.CurrentStats.SharedSetStaminaCurrent(data.CurrentStats.StaminaCurrent
                                                      + ApplyFreshness(this.StaminaRestore));

            data.CurrentStats.ServerSetFoodCurrent(data.CurrentStats.FoodCurrent
                                                   + ApplyFreshness(this.FoodRestore));

            data.CurrentStats.ServerSetWaterCurrent(data.CurrentStats.WaterCurrent
                                                    + ApplyFreshness(this.WaterRestore));

            // Please note: if player has an artificial stomach than the food freshness cannot be red.
            if (data.Freshness == ItemFreshness.Red)
            {
                // 20% chance to get food poisoning
                if (RandomHelper.RollWithProbability(0.2))
                {
                    data.Character.ServerAddStatusEffect<StatusEffectNausea>(intensity: 0.5); // 5 minutes
                }
            }

            foreach (var effect in this.Effects)
            {
                effect.Execute(new EffectActionContext(data.Character));
            }

            float ApplyFreshness(float value)
            {
                if (value <= 0)
                {
                    return value;
                }

                value *= freshnessCoef;
                return value;
            }
        }

        protected virtual bool SharedCanEat(ItemEatData data)
        {
            return !StatusEffectNausea.SharedCheckIsNauseous(
                       data.Character,
                       showNotificationIfNauseous: true);
        }

        [RemoteCallSettings(DeliveryMode.ReliableOrdered,
                            timeInterval: 0.2,
                            clientMaxSendQueueSize: 20)]
        private void ServerRemote_Eat(IItem item)
        {
            var character = ServerRemoteContext.Character;
            this.ServerValidateItemForRemoteCall(item, character);

            var stats = PlayerCharacter.GetPublicState(character).CurrentStatsExtended;

            var freshness = ItemFreshnessSystem.SharedGetFreshnessEnum(item);

            // check that the player has perk to eat a spoiled food
            if (freshness == ItemFreshness.Red
                && character.SharedHasPerk(StatName.PerkEatSpoiledFood))
            {
                freshness = ItemFreshness.Yellow;
            }

            var itemEatData = new ItemEatData(item, character, stats, freshness);
            if (!this.SharedCanEat(itemEatData))
            {
                return;
            }

            this.ServerOnEat(itemEatData);
            Logger.Important(character + " consumed " + item);

            this.ServerNotifyItemUsed(character, item);
            // decrease item count
            Server.Items.SetCount(item, (ushort)(item.Count - 1));
        }

        [Serializable]
        public struct ItemEatData
        {
            public ItemEatData(
                IItem item,
                ICharacter character,
                PlayerCharacterCurrentStats currentStats,
                ItemFreshness freshness)
            {
                this.Item = item;
                this.Character = character;
                this.CurrentStats = currentStats;
                this.Freshness = freshness;
            }

            public ICharacter Character { get; }

            public PlayerCharacterCurrentStats CurrentStats { get; }

            public ItemFreshness Freshness { get; }

            public IItem Item { get; }
        }
    }

    /// <summary>
    /// Item prototype for food items (without state).
    /// </summary>
    public abstract class ProtoItemFood
        : ProtoItemFood
            <ItemWithFreshnessPrivateState,
                EmptyPublicState,
                EmptyClientState>
    {
    }
}