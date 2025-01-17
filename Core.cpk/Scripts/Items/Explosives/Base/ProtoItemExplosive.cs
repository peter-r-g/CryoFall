﻿namespace AtomicTorch.CBND.CoreMod.Items.Explosives
{
    using System;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.Skills;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Deposits;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Explosives;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Walls;
    using AtomicTorch.CBND.CoreMod.Systems.ItemExplosive;
    using AtomicTorch.CBND.CoreMod.Systems.NewbieProtection;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.CoreMod.Systems.RaidingProtection;
    using AtomicTorch.CBND.CoreMod.UI;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.Items;
    using AtomicTorch.CBND.GameApi.Data.Physics;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Extensions;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.GameEngine.Common.Primitives;

    /// <summary>
    /// Item prototype for explosive items.
    /// </summary>
    public abstract class ProtoItemExplosive
        <TPrivateState,
         TPublicState,
         TClientState>
        : ProtoItem
          <TPrivateState,
              TPublicState,
              TClientState>,
          IProtoItemExplosive
        where TPrivateState : BasePrivateState, new()
        where TPublicState : BasePublicState, new()
        where TClientState : BaseClientState, new()
    {
        public virtual double DeployDistanceMax => 1.5; // allow to place in the neighbor tiles

        public abstract TimeSpan DeployDuration { get; }

        public override ushort MaxItemsPerStack => ItemStackSize.Small;

        public IProtoObjectExplosive ObjectExplosiveProto { get; private set; }

        protected abstract double PlantingExperienceMultiplier { get; }

        public void ClientOnActionCompleted(IItem item, bool isCancelled)
        {
            var isSelected = item == Client.Characters.CurrentPlayerCharacter.SharedGetPlayerSelectedHotbarItem();
            ClientExplosivePlacerHelper.Setup(item, isSelected: isSelected);
        }

        public void ServerOnUseActionFinished(ICharacter character, IItem item, Vector2Ushort targetPosition)
        {
            this.ServerValidateItemForRemoteCall(item, character);
            this.SharedValidatePlacement(character,
                                         targetPosition,
                                         logErrors: true,
                                         canPlace: out var canPlace,
                                         isTooFar: out var isTooFar,
                                         errorMessage: out _);
            if (!canPlace || isTooFar)
            {
                return;
            }

            var explosiveWorldObject = Server.World.CreateStaticWorldObject(this.ObjectExplosiveProto, targetPosition);
            this.ObjectExplosiveProto.ServerSetup(explosiveWorldObject, deployedByCharacter: character);
            Logger.Important($"{character} has placed {explosiveWorldObject} from {item}");

            SkillWeaponsHeavy.ServerAddItemExplosivePlantingExperience(
                character,
                this.PlantingExperienceMultiplier);

            this.ServerNotifyItemUsed(character, item);
            // decrease item count
            Server.Items.SetCount(item, (ushort)(item.Count - 1));
        }

        public bool SharedIsTooFarToPlace(ICharacter character, Vector2Ushort targetPosition, bool logErrors)
        {
            if (targetPosition.TileDistanceTo(character.TilePosition)
                <= this.DeployDistanceMax)
            {
                return false;
            }

            // distance exceeded - too far
            if (!logErrors)
            {
                return true;
            }

            if (IsClient)
            {
                this.ClientShowCannotPlaceTooFarNotification();
            }
            else
            {
                Logger.Warning($"{character} cannot place {this} - too far");
                this.CallClient(character, _ => _.ClientRemote_CannotPlaceTooFar());
            }

            return true;
        }

        public void SharedValidatePlacement(
            ICharacter character,
            Vector2Ushort targetPosition,
            bool logErrors,
            out bool canPlace,
            out bool isTooFar,
            out string errorMessage)
        {
            if (NewbieProtectionSystem.SharedIsNewbie(character))
            {
                if (logErrors)
                {
                    NewbieProtectionSystem.SharedNotifyNewbieCannotPerformAction(character, this);
                }

                canPlace = false;
                isTooFar = false;
                errorMessage = null;
                return;
            }

            // check whether somebody nearby is already placing a bomb there
            var tempCharactersNearby = Api.Shared.GetTempList<ICharacter>();
            if (IsServer)
            {
                Server.World.GetScopedByPlayers(character, tempCharactersNearby);
            }
            else
            {
                Client.Characters.GetKnownPlayerCharacters(tempCharactersNearby);
            }

            foreach (var otherCharacter in tempCharactersNearby.AsList())
            {
                if (otherCharacter != character
                    && otherCharacter.IsInitialized
                    && PlayerCharacter.GetPublicState(otherCharacter).CurrentPublicActionState
                        is ItemExplosiveActionPublicState explosiveActionState
                    && explosiveActionState.TargetPosition == targetPosition)
                {
                    // someone is already planting a bomb here
                    canPlace = false;
                    isTooFar = false;
                    errorMessage = null;
                    return;
                }
            }

            // check if there is a direct line of sight
            // check that there are no other objects on the way between them (defined by default layer)
            var physicsSpace = character.PhysicsBody.PhysicsSpace;
            var characterCenter = character.Position + character.PhysicsBody.CenterOffset;
            var worldObjectCenter = targetPosition.ToVector2D() + (0.5, 0.5);

            // local method for testing if there is an obstacle from current to the specified position
            bool TestHasObstacle(Vector2D toPosition)
            {
                using var obstaclesInTheWay = physicsSpace.TestLine(
                    characterCenter,
                    toPosition,
                    CollisionGroup.Default,
                    sendDebugEvent: false);
                foreach (var test in obstaclesInTheWay.AsList())
                {
                    var testPhysicsBody = test.PhysicsBody;
                    if (testPhysicsBody.AssociatedProtoTile is not null)
                    {
                        // obstacle tile on the way
                        return true;
                    }

                    var testWorldObject = testPhysicsBody.AssociatedWorldObject;
                    if (testWorldObject == character)
                    {
                        // not an obstacle - it's the character or world object itself
                        continue;
                    }

                    switch (testWorldObject.ProtoWorldObject)
                    {
                        case IProtoObjectDeposit: // allow deposits
                        case ObjectWallDestroyed: // allow destroyed walls
                            continue;
                    }

                    // obstacle object on the way
                    return true;
                }

                // no obstacles
                return false;
            }

            if (!this.ObjectExplosiveProto.CheckTileRequirements(targetPosition,
                                                                 character,
                                                                 out errorMessage,
                                                                 logErrors))
            {
                // explosive static object placement requirements failed
                canPlace = false;
                isTooFar = false;
                return;
            }

            // let's check whether there are any obstacles by casting rays
            // from character's center to the center of the planted bomb
            if (TestHasObstacle(worldObjectCenter))
            {
                // has obstacle
                if (logErrors)
                {
                    if (IsClient)
                    {
                        this.ClientShowCannotPlaceObstaclesInTheWayNotification();
                    }
                    else
                    {
                        Logger.Warning($"{character} cannot place {this} - obstacles in the way");
                        this.CallClient(character, _ => _.ClientRemote_CannotPlaceObstacles());
                    }
                }

                canPlace = false;
                isTooFar = false;
                errorMessage = CoreStrings.Notification_ObstaclesOnTheWay;
                return;
            }

            // validate distance to the character
            if (this.SharedIsTooFarToPlace(character, targetPosition, logErrors))
            {
                canPlace = true;
                isTooFar = true;
                return;
            }

            canPlace = true;
            isTooFar = false;
        }

        protected override void ClientItemHotbarSelectionChanged(ClientHotbarItemData data)
        {
            if (data.IsSelected
                && this.ObjectExplosiveProto.IsActivatesRaidBlock)
            {
                RaidingProtectionSystem.ClientShowNotificationRaidingNotAvailableIfNecessary();
            }

            ClientExplosivePlacerHelper.Setup(data.Item, data.IsSelected);
        }

        protected override bool ClientItemUseFinish(ClientItemData data)
        {
            ItemExplosiveSystem.Instance.ClientTryAbortAction();
            return false; // don't play the sound
        }

        protected sealed override void PrepareProtoItem()
        {
            base.PrepareProtoItem();
            this.PrepareProtoItemExplosive(out var objectExplosiveProto);

            this.ObjectExplosiveProto = objectExplosiveProto;
        }

        protected abstract void PrepareProtoItemExplosive(
            out IProtoObjectExplosive objectExplosiveProto);

        [RemoteCallSettings(DeliveryMode.ReliableUnordered)]
        private void ClientRemote_CannotPlaceObstacles()
        {
            this.ClientShowCannotPlaceObstaclesInTheWayNotification();
        }

        //protected override ReadOnlySoundPreset<ItemSound> PrepareSoundPresetItem()
        //{
        //    return ItemsSoundPresets.ItemExplosive;
        //}

        [RemoteCallSettings(DeliveryMode.ReliableUnordered)]
        private void ClientRemote_CannotPlaceTooFar()
        {
            this.ClientShowCannotPlaceTooFarNotification();
        }

        private void ClientShowCannotPlaceObstaclesInTheWayNotification()
        {
            NotificationSystem.ClientShowNotification(CoreStrings.Notification_CannotPlaceThere_Title,
                                                      CoreStrings.Notification_ObstaclesOnTheWay,
                                                      NotificationColor.Bad,
                                                      this.ObjectExplosiveProto.Icon);
        }

        private void ClientShowCannotPlaceTooFarNotification()
        {
            NotificationSystem.ClientShowNotification(CoreStrings.Notification_CannotPlaceThere_Title,
                                                      CoreStrings.Notification_TooFar,
                                                      NotificationColor.Bad,
                                                      this.ObjectExplosiveProto.Icon);
        }
    }

    /// <summary>
    /// Item prototype for explosive items.
    /// </summary>
    public abstract class ProtoItemExplosive
        : ProtoItemExplosive
            <EmptyPrivateState,
                EmptyPublicState,
                EmptyClientState>
    {
    }
}