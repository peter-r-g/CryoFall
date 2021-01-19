﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects.Teleport.Data
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using AtomicTorch.CBND.CoreMod.Characters;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.Helpers.Client;
    using AtomicTorch.CBND.CoreMod.Systems;
    using AtomicTorch.CBND.CoreMod.Systems.PvE;
    using AtomicTorch.CBND.CoreMod.Systems.TeleportsSystem;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class ViewModelWindowTeleportConfirmationDialog : BaseViewModel
    {
        private readonly CharacterCurrentStats characterCurrentStats;

        public ViewModelWindowTeleportConfirmationDialog(Vector2Ushort teleportWorldPosition)
        {
            this.TeleportWorldPosition = teleportWorldPosition;
            this.characterCurrentStats = PlayerCharacter.GetPublicState(ClientCurrentCharacterHelper.Character)
                                                        .CurrentStats;
            this.characterCurrentStats.ClientSubscribe(_ => _.HealthCurrent,
                                                       _ =>
                                                       {
                                                           this.NotifyPropertyChanged(nameof(this.BloodCostText));
                                                           this.NotifyPropertyChanged(nameof(this.HasEnoughBlood));
                                                       },
                                                       this);

            ClientCurrentCharacterContainersHelper.ContainersItemsReset += this.ContainersItemsResetHandler;
            ClientCurrentCharacterContainersHelper.ItemAddedOrRemovedOrCountChanged +=
                this.ItemAddedOrRemovedOrCountChangedHandler;

            if (PveSystem.ClientIsPve(true))
            {
                return;
            }

            // For PvP: check whether there are any players camping this teleport.
            TeleportsSystem.ClientIsTeleportHasUnfriendlyPlayersNearby(teleportWorldPosition)
                           .ContinueWith(hasPlayersNearby =>
                                         {
                                             if (!this.IsDisposed)
                                             {
                                                 this.HasUnfriendlyPlayersNearby = hasPlayersNearby.Result;
                                             }
                                         },
                                         TaskContinuationOptions.ExecuteSynchronously);
        }

        public string BloodCostText
            => string.Format("{0} {1}",
                             TeleportsSystem.SharedCalculateTeleportationBloodCost(
                                 ClientCurrentCharacterHelper.Character),
                             CoreStrings.HealthPointsAbbreviation);

        public BaseCommand CommandTeleportPayWithBlood
            => new ActionCommand(
                () => this.ExecuteCommandTeleport(payWithItem: false));

        public BaseCommand CommandTeleportPayWithItem
            => new ActionCommand(
                () => this.ExecuteCommandTeleport(payWithItem: true));

        public bool HasEnoughBlood
            => this.characterCurrentStats.HealthCurrent
               > TeleportsSystem.SharedCalculateTeleportationBloodCost(this.characterCurrentStats);

        public bool HasOptionalItem
            => TeleportsSystem.SharedHasOptionalRequiredItem(ClientCurrentCharacterHelper.Character);

        public bool HasUnfriendlyPlayersNearby { get; private set; }

        public IReadOnlyList<ProtoItemWithCount> OptionalInputItems
            => new[]
            {
                new ProtoItemWithCount(
                    TeleportsSystem.OptionalTeleportationItemProto,
                    count: 1)
            };

        public Vector2Ushort TeleportWorldPosition { get; }

        protected override void DisposeViewModel()
        {
            ClientCurrentCharacterContainersHelper.ContainersItemsReset -= this.ContainersItemsResetHandler;
            ClientCurrentCharacterContainersHelper.ItemAddedOrRemovedOrCountChanged -=
                this.ItemAddedOrRemovedOrCountChangedHandler;
            base.DisposeViewModel();
        }

        private void ContainersItemsResetHandler()
        {
            this.NotifyPropertyChanged(nameof(this.HasOptionalItem));
        }

        private void ExecuteCommandTeleport(bool payWithItem)
        {
            TeleportsSystem.ClientTeleport(this.TeleportWorldPosition, payWithItem);
        }

        private void ItemAddedOrRemovedOrCountChangedHandler(IItem obj)
        {
            this.NotifyPropertyChanged(nameof(this.HasOptionalItem));
        }
    }
}