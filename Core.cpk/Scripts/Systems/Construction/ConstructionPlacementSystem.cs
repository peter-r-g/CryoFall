﻿namespace AtomicTorch.CBND.CoreMod.Systems.Construction
{
    using System;
    using System.Linq;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Input;
    using AtomicTorch.CBND.CoreMod.ClientComponents.StaticObjects;
    using AtomicTorch.CBND.CoreMod.Helpers.Client;
    using AtomicTorch.CBND.CoreMod.Items.Tools;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.ConstructionSite;
    using AtomicTorch.CBND.CoreMod.StaticObjects.Structures.Walls;
    using AtomicTorch.CBND.CoreMod.Systems.Creative;
    using AtomicTorch.CBND.CoreMod.Systems.Notifications;
    using AtomicTorch.CBND.CoreMod.UI;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core.Menu;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Construction;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Items.Controls;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Data.State;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Extensions;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class ConstructionPlacementSystem : ProtoSystem<ConstructionPlacementSystem>, IMenu
    {
        public const double MaxDistanceToBuild = 5;

        public const string NotificationCannotBuild_Title = "Cannot build there";

        private const bool AllowInstantPlacementInCreativeMode = true;

        private static ClientComponentObjectPlacementHelper componentObjectPlacementHelper;

        private static IProtoObjectStructure currentSelectedProtoConstruction;

        public delegate void StructureBuiltDelegate(ICharacter character, IStaticWorldObject structure);

        public static event StructureBuiltDelegate ServerStructureBuilt;

        public event Action IsOpenedChanged;

        public static bool ClientConstructionCooldownIsWaitingButtonRelease { get; private set; }

        public static bool IsInObjectPlacementMode => componentObjectPlacementHelper?.IsEnabled ?? false;

        public bool IsOpened
        {
            get
            {
                var window = WindowConstructionMenu.Instance;
                return window is not null
                       && (window.WindowState == GameWindowState.Opened
                           || window.WindowState == GameWindowState.Opening);
            }
        }

        public override string Name => "Construction placement system";

        public static void ClientDisableConstructionPlacement()
        {
            componentObjectPlacementHelper?.SceneObject.Destroy();
            componentObjectPlacementHelper = null;

            ConstructionRelocationSystem.ClientDisableConstructionRelocation();
        }

        public static void ClientToggleConstructionMenu()
        {
            if (ClientCloseConstructionMenu())
            {
                // just closed the construction menu
                return;
            }

            if (ClientCurrentCharacterHelper.PublicState.CurrentVehicle is not null)
            {
                Logger.Important("Construction menu is not accessible while in a vehicle");
                return;
            }

            WindowConstructionMenu.Open(
                onStructureProtoSelected:
                selectedProtoStructure =>
                {
                    ClientEnsureConstructionToolIsSelected();
                    currentSelectedProtoConstruction = selectedProtoStructure;

                    componentObjectPlacementHelper = Client.Scene
                                                           .CreateSceneObject("ConstructionHelper")
                                                           .AddComponent<ClientComponentObjectPlacementHelper>();

                    // repeat placement for held button only for walls, floor and farms
                    var isRepeatCallbackIfHeld = selectedProtoStructure.IsRepeatPlacement;

                    componentObjectPlacementHelper
                        .Setup(selectedProtoStructure,
                               isCancelable: true,
                               isRepeatCallbackIfHeld: isRepeatCallbackIfHeld,
                               isDrawConstructionGrid: true,
                               isBlockingInput: true,
                               validateCanPlaceCallback: ClientValidateCanBuild,
                               placeSelectedCallback: ClientConstructionPlaceSelectedCallback,
                               delayRemainsSeconds: 0.4);
                },
                onClosed: OnStructureSelectWindowOpenedOrClosed);

            OnStructureSelectWindowOpenedOrClosed();
        }

        public static void ServerReplaceConstructionSiteWithStructure(
            IStaticWorldObject worldObject,
            IProtoObjectStructure protoStructure,
            ICharacter byCharacter)
        {
            if (worldObject?.IsDestroyed ?? true)
            {
                throw new Exception("Construction site doesn't exist or already destroyed: " + worldObject);
            }

            var tilePosition = worldObject.TilePosition;

            // destroy construction site
            Server.World.DestroyObject(worldObject);

            // create structure
            var structure = ConstructionSystem.ServerCreateStructure(
                protoStructure,
                tilePosition,
                byCharacter: byCharacter);

            if (byCharacter is null)
            {
                return;
            }

            Instance.ServerNotifyOnStructurePlacedOrRelocated(structure, byCharacter);
            Api.SafeInvoke(() => ServerStructureBuilt?.Invoke(byCharacter, structure));
        }

        public void Dispose()
        {
        }

        public void InitMenu()
        {
        }

        public void ServerNotifyOnStructurePlacedOrRelocated(IStaticWorldObject structure, ICharacter byCharacter)
        {
            //// it will return empty list because the object is new!
            // var scopedByPlayers = Server.World.GetScopedByPlayers(structure);

            //// Workaround:
            using var scopedBy = Api.Shared.GetTempList<ICharacter>();
            Server.World.GetScopedByPlayers(byCharacter, scopedBy);
            this.CallClient(scopedBy.AsList(),
                            _ => _.ClientRemote_OnStructurePlaced(structure.ProtoStaticWorldObject,
                                                                  structure.TilePosition,
                                                                  /*isByCurrentPlayer: */
                                                                  false));

            this.CallClient(byCharacter,
                            _ => _.ClientRemote_OnStructurePlaced(structure.ProtoStaticWorldObject,
                                                                  structure.TilePosition,
                                                                  /*isByCurrentPlayer: */
                                                                  true));
        }

        public void Toggle()
        {
            ClientToggleConstructionMenu();
        }

        protected override void PrepareSystem()
        {
            if (IsClient)
            {
                ClientUpdateHelper.UpdateCallback += ClientUpdate;
            }
        }

        private static bool ClientCloseConstructionMenu()
        {
            if (Instance.IsOpened)
            {
                // construction window is opened
                WindowConstructionMenu.Instance.Close();
                return true;
            }

            // construction window is closed
            if (componentObjectPlacementHelper is not null)
            {
                // construction location selector is active - destroy it and don't open the construction menu
                ClientDisableConstructionPlacement();
                return true;
            }

            return false;
        }

        private static void ClientConstructionPlaceSelectedCallback(Vector2Ushort tilePosition)
        {
            ClientEnsureConstructionToolIsSelected();

            ClientConstructionCooldownIsWaitingButtonRelease = true;

            // validate if there are enough required items/resources to build the structure
            Instance.CallServer(_ => _.ServerRemote_PlaceStructure(currentSelectedProtoConstruction, tilePosition));

            if (!currentSelectedProtoConstruction.IsRepeatPlacement)
            {
                // don't repeat placement
                ClientDisableConstructionPlacement();
            }
        }

        private static void ClientEnsureConstructionToolIsSelected()
        {
            var activeItem = ClientHotbarSelectedItemManager.SelectedItem;
            if (activeItem?.ProtoItem is IProtoItemToolToolbox)
            {
                // tool is selected
                return;
            }

            var itemTool = (from item in ClientHotbarSelectedItemManager.ContainerHotbar.Items
                            let protoToolbox = item.ProtoItem as IProtoItemToolToolbox
                            where protoToolbox is not null
                            // find best tool
                            orderby protoToolbox.ConstructionSpeedMultiplier descending,
                                item.ContainerSlotId ascending
                            select item).FirstOrDefault();

            if (itemTool is null)
            {
                throw new Exception("Cannot build - the required tool is not available in the hotbar");
            }

            ClientHotbarSelectedItemManager.Select(itemTool);
        }

        private static void ClientUpdate()
        {
            if (ClientConstructionCooldownIsWaitingButtonRelease
                && !ClientInputManager.IsButtonHeld(GameButton.ActionUseCurrentItem))
            {
                ClientConstructionCooldownIsWaitingButtonRelease = false;
            }
        }

        private static void ClientValidateCanBuild(
            Vector2Ushort tilePosition,
            bool logErrors,
            out string errorMessage,
            out bool canPlace,
            out bool isTooFar)
        {
            var protoStructure = currentSelectedProtoConstruction;

            if (Client.World.GetTile(tilePosition)
                      .StaticObjects
                      .Any(so => so.ProtoStaticWorldObject == protoStructure
                                 || ProtoObjectConstructionSite.SharedIsConstructionOf(so, protoStructure)))
            {
                // this building is already built here
                canPlace = false;
                isTooFar = false;
                errorMessage = null;
                return;
            }

            var character = Client.Characters.CurrentPlayerCharacter;
            if (!protoStructure.CheckTileRequirements(
                    tilePosition,
                    character,
                    errorMessage: out errorMessage,
                    logErrors: logErrors))
            {
                // time requirements are not valid
                canPlace = false;
                isTooFar = false;
                return;
            }

            var configBuild = protoStructure.ConfigBuild;
            if (configBuild.CheckStageCanBeBuilt(character))
            {
                canPlace = true;
                isTooFar = SharedIsTooFarToPlace(protoStructure,
                                                 tilePosition,
                                                 character,
                                                 logErrors);
                return;
            }

            // not enough items to build the stage
            // close construction menu
            ClientCloseConstructionMenu();
            canPlace = false;
            isTooFar = false;
        }

        private static void OnStructureSelectWindowOpenedOrClosed()
        {
            Instance.IsOpenedChanged?.Invoke();
        }

        private static bool SharedIsTooFarToPlace(
            IProtoObjectStructure protoStructure,
            Vector2Ushort tilePosition,
            ICharacter character,
            bool logErrors)
        {
            if (character.TilePosition.TileDistanceTo(tilePosition)
                <= MaxDistanceToBuild
                || CreativeModeSystem.SharedIsInCreativeMode(character))
            {
                return false;
            }

            if (!logErrors)
            {
                return true;
            }

            Logger.Info(
                $"Cannot place {protoStructure} at {tilePosition}: player character is too far",
                character);

            if (IsClient)
            {
                Instance.ClientRemote_CannotBuildTooFar(protoStructure);
            }
            else
            {
                Instance.CallClient(character, _ => _.ClientRemote_CannotBuildTooFar(protoStructure));
            }

            return true;
        }

        [RemoteCallSettings(DeliveryMode.ReliableUnordered)]
        private void ClientRemote_CannotBuildTooFar(IProtoStaticWorldObject protoStaticWorldObject)
        {
            NotificationSystem.ClientShowNotification(
                NotificationCannotBuild_Title,
                CoreStrings.Notification_TooFar,
                NotificationColor.Bad,
                protoStaticWorldObject.Icon);
        }

        [RemoteCallSettings(DeliveryMode.ReliableOrdered)]
        private void ClientRemote_OnStructurePlaced(
            IProtoStaticWorldObject protoStaticWorldObject,
            Vector2Ushort position,
            bool isByCurrentPlayer)
        {
            var soundPreset = protoStaticWorldObject.SharedGetObjectSoundPreset();
            if (isByCurrentPlayer)
            {
                // play 2D sound
                soundPreset.PlaySound(ObjectSound.Place, limitOnePerFrame: false);
            }
            else
            {
                // play 3D sound (at the built object location)
                soundPreset.PlaySound(ObjectSound.Place,
                                      position.ToVector2D() + protoStaticWorldObject.Layout.Center);
            }
        }

        private IStaticWorldObject ServerCreateConstructionSite(
            Vector2Ushort tilePosition,
            IProtoObjectStructure protoStructure,
            ICharacter byCharacter)
        {
            if (protoStructure is null)
            {
                throw new ArgumentNullException(nameof(protoStructure));
            }

            var protoConstructionSite = protoStructure.ConstructionSitePrototype;
            var constructionSite = Server.World.CreateStaticWorldObject(protoConstructionSite, tilePosition);

            var serverState = ProtoObjectConstructionSite.GetPublicState(constructionSite);
            serverState.Setup(protoStructure);

            // reinitialize to build proper physics and occupy proper layout
            constructionSite.ServerRebuildScopeAndPhysics();

            Logger.Important("Construction site created: " + constructionSite);
            protoConstructionSite.ServerOnBuilt(constructionSite, byCharacter);
            Api.SafeInvoke(() => ServerStructureBuilt?.Invoke(byCharacter, constructionSite));
            Api.SafeInvoke(() => SharedWallConstructionRefreshHelper.SharedRefreshNeighborObjects(
                               constructionSite,
                               isDestroy: false));

            return constructionSite;
        }

        [RemoteCallSettings(DeliveryMode.ReliableOrdered, timeInterval: 0.05)]
        private void ServerRemote_PlaceStructure(
            IProtoObjectStructure protoStructure,
            Vector2Ushort tilePosition)
        {
            var character = ServerRemoteContext.Character;

            if (!protoStructure.SharedIsTechUnlocked(character))
            {
                Logger.Warning(
                    $"Cannot build {protoStructure} at {tilePosition}: player character doesn't have unlocked tech node for this structure.",
                    character);
                return;
            }

            if (SharedIsTooFarToPlace(protoStructure,
                                      tilePosition,
                                      character,
                                      logErrors: true))
            {
                return;
            }

            // validate if the structure can be placed there
            if (!protoStructure.CheckTileRequirements(tilePosition, character, logErrors: true))
            {
                return;
            }

            // validate if there are enough required items/resources to build the structure
            var configBuild = protoStructure.ConfigBuild;
            if (!configBuild.CheckStageCanBeBuilt(character))
            {
                Logger.Warning(
                    $"Cannot build {protoStructure} at {tilePosition}: player character doesn't have enough resources (or not allowed).",
                    character);
                return;
            }

            var selectedHotbarItem = PlayerCharacter.GetPublicState(character).SelectedItem;
            if (selectedHotbarItem?.ProtoItem is not IProtoItemToolToolbox)
            {
                Logger.Warning(
                    $"Cannot build {protoStructure} at {tilePosition}: player character doesn't have selected construction tool.",
                    character);
                return;
            }

            // consume required items/resources (for 1 stage)
            configBuild.ServerDestroyRequiredItems(character);

            if (configBuild.StagesCount > 1)
            {
                if (AllowInstantPlacementInCreativeMode
                    && CreativeModeSystem.SharedIsInCreativeMode(character))
                {
                    // instant placement allowed
                }
                else
                {
                    // there are multiple construction stages - spawn and setup a construction site
                    var constructionSite = this.ServerCreateConstructionSite(tilePosition, protoStructure, character);
                    this.ServerNotifyOnStructurePlacedOrRelocated(constructionSite, character);
                    return;
                }
            }

            ServerDecalsDestroyHelper.DestroyAllDecals(tilePosition, protoStructure.Layout);

            // there is only one construction stage - simply spawn the structure
            var structure = ConstructionSystem.ServerCreateStructure(
                protoStructure,
                tilePosition,
                character);

            this.ServerNotifyOnStructurePlacedOrRelocated(structure, character);
            Api.SafeInvoke(() => ServerStructureBuilt?.Invoke(character, structure));
        }
    }
}