﻿namespace AtomicTorch.CBND.CoreMod.Bootstrappers
{
    using System;
    using AtomicTorch.CBND.CoreMod.Characters.Player;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Camera;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Input;
    using AtomicTorch.CBND.CoreMod.ClientComponents.InputListeners;
    using AtomicTorch.CBND.CoreMod.ClientComponents.StaticObjects;
    using AtomicTorch.CBND.CoreMod.Helpers.Client;
    using AtomicTorch.CBND.CoreMod.Systems.Creative;
    using AtomicTorch.CBND.CoreMod.Systems.ServerOperator;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core.Menu;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Chat;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Completionist;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Crafting;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Faction;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.HUD;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Items.Managers;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Map;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Player;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Politics;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Quests;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Skills;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Social;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.Technologies;
    using AtomicTorch.CBND.CoreMod.UI.Services;
    using AtomicTorch.CBND.GameApi.Data;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.GameEngine.Common.Primitives;

    [PrepareOrder(afterType: typeof(BootstrapperClientCore))]
    public static class BootstrapperClientGame
    {
        private static readonly Interval<double> ZoomBoundsGameplayMode
            = new(min: 0.5,
                max: Api.IsEditor ? 2 : 1);

        private static ClientInputContext gameplayInputContext;

        private static bool isInitialized;

        private static IClientSceneObject sceneObjectInputComponents;

        public static event Action<ICharacter> InitCallback;

        public static event Action<ICharacter> InitEndCallback;

        public static event Action ResetCallback;

        public static void Init(ICharacter currentCharacter)
        {
            Reset();

            isInitialized = true;

            if (currentCharacter is null)
            {
                return;
            }

            ClientCurrentCharacterHelper.Init(currentCharacter);
            InitCallback?.Invoke(currentCharacter);

            if (currentCharacter.ProtoCharacter == Api.GetProtoEntity<PlayerCharacter>()
                || currentCharacter.ProtoCharacter == Api.GetProtoEntity<PlayerCharacterSpectator>()
                || currentCharacter.ProtoCharacter == Api.GetProtoEntity<PlayerCharacterMob>())
            {
                InitGameplayMode(currentCharacter);
            }

            CreativeModeSystem.ClientRequestCurrentUserIsInCreativeMode();
            ServerOperatorSystem.ClientRequestCurrentUserIsOperator();

            InitEndCallback?.Invoke(currentCharacter);
        }

        private static void InitGameplayMode(ICharacter currentCharacter)
        {
            ClientCurrentCharacterContainersHelper.Init(currentCharacter);
            ClientItemsManager.Init(currentCharacter);
            ClientDroppedItemsNotifier.Init(currentCharacter);

            var layoutRootChildren = Api.Client.UI.LayoutRootChildren;
            layoutRootChildren.Add(new HUDLayoutControl());
            layoutRootChildren.Add(new ChatPanel());

            ClientContainersExchangeManager.Reset();

            CraftingQueueControl.Instance?.Refresh();

            var input = Api.Client.Scene.CreateSceneObject("Input components");
            input.AddComponent<ClientComponentHotbarHelper>();
            input.AddComponent<ClientComponentWindowStructureSelectListToogle>();
            input.AddComponent<ClientComponentObjectInteractionHelper>();

            // ReSharper disable once CanExtractXamlLocalizableStringCSharp
            gameplayInputContext =
                ClientInputContext
                    .Start("Menu toggles")
                    .HandleButtonDown(
                        GameButton.InventoryMenu,
                        () =>
                        {
                            if (Menu.IsOpenedAny())
                            {
                                // shortcut to close currently opened menus
                                // (basically, it's alternative to pressing ESC in such case)
                                Menu.CloseAll();
                                return;
                            }

                            Menu.Toggle<WindowInventory>();
                        })
                    .HandleButtonDown(GameButton.CraftingMenu,      Menu.Toggle<WindowHandCrafting>)
                    .HandleButtonDown(GameButton.MapMenu,           Menu.Toggle<WindowWorldMap>)
                    .HandleButtonDown(GameButton.SkillsMenu,        Menu.Toggle<WindowSkills>)
                    .HandleButtonDown(GameButton.TechnologiesMenu,  Menu.Toggle<WindowTechnologies>)
                    .HandleButtonDown(GameButton.SocialMenu,        Menu.Toggle<WindowSocial>)
                    .HandleButtonDown(GameButton.PoliticsMenu,      Menu.Toggle<WindowPolitics>)
                    .HandleButtonDown(GameButton.QuestsMenu,        Menu.Toggle<WindowQuests>)
                    .HandleButtonDown(GameButton.CompletionistMenu, Menu.Toggle<WindowCompletionist>)
                    .HandleButtonDown(GameButton.FactionMenu,
                                      () =>
                                      {
                                          if (Menu.Exist<WindowFaction>())
                                          {
                                              Menu.Toggle<WindowFaction>();
                                          }
                                      })
                    .HandleButtonDown(GameButton.OpenChat,
                                      () =>
                                      {
                                          if (WindowsManager.OpenedWindowsCount == 0)
                                          {
                                              ChatPanel.Instance.Open();
                                          }
                                      });

            sceneObjectInputComponents = input;

            ClientComponentWorldCameraZoomManager.Instance.ZoomBounds = ZoomBoundsGameplayMode;
        }

        private static void Reset()
        {
            if (!isInitialized)
            {
                return;
            }

            isInitialized = false;

            Menu.CloseAll();

            ClientComponentObjectPlacementHelper.DestroyInstanceIfExist();

            if (HUDLayoutControl.Instance is not null)
            {
                Api.Client.UI.LayoutRootChildren.Remove(HUDLayoutControl.Instance);
            }

            if (ChatPanel.Instance is not null)
            {
                Api.Client.UI.LayoutRootChildren.Remove(ChatPanel.Instance);
            }

            ResetCallback?.Invoke();

            ClientContainersExchangeManager.Reset();

            ClientCurrentCharacterContainersHelper.Reset();

            sceneObjectInputComponents?.Destroy();
            sceneObjectInputComponents = null;

            gameplayInputContext?.Stop();
            gameplayInputContext = null;
        }
    }
}