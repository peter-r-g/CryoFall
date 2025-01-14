﻿namespace AtomicTorch.CBND.CoreMod.Editor
{
    using System.Windows;
    using AtomicTorch.CBND.CoreMod.Bootstrappers;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Camera;
    using AtomicTorch.CBND.CoreMod.ClientComponents.Input;
    using AtomicTorch.CBND.CoreMod.Editor.Data;
    using AtomicTorch.CBND.CoreMod.Editor.Scripts;
    using AtomicTorch.CBND.CoreMod.Editor.Tools.EditorToolMap;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Core.Menu;
    using AtomicTorch.CBND.GameApi.Data.Characters;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.Network;
    using AtomicTorch.CBND.GameApi.ServicesClient;
    using AtomicTorch.GameEngine.Common.Primitives;

    public class BootstrapperClientEditor : BaseBootstrapper
    {
        public const string EditorWelcomeMessage =
            @"Controls:
              [*][b]F8[/b] key to toggle [b][u]Editor Mode[/u][/b]
              [*][b]F2[/b] to quicksave
              [*][b]F3[/b] to quickload
              [*][b]F5[/b] for [u]Debug Tools[/u]
              [br][br]
              Please note that the Editor user interface is available only in English, though the rest of the UI and all the game content is localized to the same languages as the game.
              [br][br]To improve performance in [b][u]Editor Mode[/u][/b], please uncheck [b]""Terrain blending""[/b] in the bottom right corner.";

        private static readonly Interval<double> ZoomBoundsEditorMode
            = new(min: 0.05,
                  // please note - in Editor mode we allow to zoom closer (to check HiDPI sprites)
                  max: Api.IsEditor ? 8 : 1);

        private ClientInputContext inputContextEditorMapMenu;

        private ClientInputContext inputContextEditorUndoRedo;

        public override void ClientInitialize()
        {
            ClientInputManager.RegisterButtonsEnum<EditorButton>();

            ClientUpdateHelper.UpdateCallback += ClientUpdateCallback;
            BootstrapperClientGame.InitCallback += this.Init;
            BootstrapperClientGame.ResetCallback += this.Reset;

            ClientInputContext.Start("Editor quick save/load")
                              .HandleButtonDown(
                                  EditorButton.LoadQuickSavegame,
                                  EditorToolMap.ClientLoadSavegameQuick)
                              .HandleButtonDown(
                                  EditorButton.MakeQuickSavegame,
                                  EditorToolMap.ClientSaveSavegameQuick);

            if (Api.Shared.IsDebug)
            {
                return;
            }

            var sessionStorage = Client.Storage.GetSessionStorage("EditorWelcome");
            if (sessionStorage.TryLoad(out bool isWelcomeMessageDisplayed)
                && isWelcomeMessageDisplayed)
            {
                return;
            }

            DialogWindow.ShowDialog(
                "Welcome to CryoFall Editor!",
                new FormattedTextBlock() { Content = EditorWelcomeMessage, TextAlignment = TextAlignment.Left },
                closeByEscapeKey: true,
                okAction: () => { sessionStorage.Save(true, writeToLog: false); });
        }

        private static void ClientUpdateCallback()
        {
            if (!ClientInputManager.IsButtonDown(EditorButton.ToggleEditorMode))
            {
                return;
            }

            var currentCharacter = Client.Characters.CurrentPlayerCharacter;
            if (currentCharacter is null)
            {
                return;
            }

            // reset focus (workaround for NoesisGUI crash when not focused unloaded control receives OnKeyDown)
            Client.UI.BlurFocus();

            var protoCharacterEditorMode = Api.GetProtoEntity<PlayerCharacterEditorMode>();
            if (currentCharacter.ProtoCharacter is PlayerCharacterEditorMode)
            {
                // switch to player mode
                EditorActiveToolManager.Deactivate();
                protoCharacterEditorMode
                    .CallServer(_ => _.ServerRemote_SwitchToPlayerMode());
            }
            else
            {
                // switch to editor mode
                protoCharacterEditorMode
                    .CallServer(_ => _.ServerRemote_SwitchToEditorMode());
            }
        }

        private void Init(ICharacter currentCharacter)
        {
            if (currentCharacter.ProtoCharacter != Api.GetProtoEntity<PlayerCharacterEditorMode>())
            {
                return;
            }

            Api.Client.UI.LayoutRootChildren.Add(new EditorHUDLayoutControl());

            ClientComponentWorldCameraZoomManager.Instance.ZoomBounds = ZoomBoundsEditorMode;
            Menu.Register<WindowEditorWorldMap>();

            this.inputContextEditorMapMenu
                = ClientInputContext
                  .Start("Editor map")
                  .HandleButtonDown(
                      GameButton.MapMenu,
                      Menu.Toggle<WindowEditorWorldMap>);

            this.inputContextEditorUndoRedo
                = ClientInputContext
                  .Start("Editor undo/redo")
                  .HandleAll(
                      () =>
                      {
                          var input = Client.Input;
                          if (input.IsKeyDown(InputKey.Z)
                              && input.IsKeyHeld(InputKey.Control))
                          {
                              if (input.IsKeyHeld(InputKey.Shift))
                              {
                                  EditorClientActionsHistorySystem.Redo();
                                  return;
                              }

                              EditorClientActionsHistorySystem.Undo();
                              return;
                          }

                          if (input.IsKeyDown(InputKey.Y)
                              && input.IsKeyHeld(InputKey.Control))
                          {
                              EditorClientActionsHistorySystem.Redo();
                          }
                      });
        }

        private void Reset()
        {
            if (EditorHUDLayoutControl.Instance is not null)
            {
                Api.Client.UI.LayoutRootChildren.Remove(EditorHUDLayoutControl.Instance);
            }

            this.inputContextEditorMapMenu?.Stop();
            this.inputContextEditorMapMenu = null;

            this.inputContextEditorUndoRedo?.Stop();
            this.inputContextEditorUndoRedo = null;
        }
    }
}