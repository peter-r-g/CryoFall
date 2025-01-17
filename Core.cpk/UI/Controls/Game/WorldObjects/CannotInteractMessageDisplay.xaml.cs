﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Game.WorldObjects
{
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.GameApi.Data.World;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.ServicesClient.Components;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;

    public partial class CannotInteractMessageDisplay : BaseUserControl
    {
        private const double TimeoutSeconds = 1.5;

        private static CannotInteractMessageDisplay lastControl;

        private IComponentAttachedControl componentAttachedControl;

        public static void ClientOnCannotInteract(
            IWorldObject worldObject,
            string message,
            bool isOutOfRange = false,
            double? hideDelay = null,
            bool playSound = true)
        {
            ShowOn(worldObject, message, hideDelay);

            var soundKey = isOutOfRange
                               ? ObjectSound.InteractOutOfRange
                               : ObjectSound.InteractFail;

            if (playSound)
            {
                worldObject.ProtoWorldObject.SharedGetObjectSoundPreset()
                           .PlaySound(soundKey);
            }
        }

        public static void Hide()
        {
            lastControl?.componentAttachedControl.Destroy();
            lastControl = null;
        }

        public static void ShowOn(
            IWorldObject worldObject,
            string message,
            double? hideDelay = null)
        {
            if (Api.IsServer)
            {
                Api.Logger.Error("Cannot call this method on server");
                return;
            }

            Hide();

            var positionOffset = worldObject.ProtoWorldObject?.SharedGetObjectCenterWorldOffset(worldObject)
                                 ?? (0, 0);

            positionOffset += (0, 1.125);

            lastControl = new CannotInteractMessageDisplay();
            lastControl.Setup(message);

            lastControl.componentAttachedControl = Api.Client.UI.AttachControl(
                worldObject,
                lastControl,
                positionOffset: positionOffset,
                isFocusable: false);

            lastControl.componentAttachedControl.Destroy(delay: hideDelay ?? TimeoutSeconds);
        }

        public void Setup(string message)
        {
            this.DataContext = message;
        }

        protected override void OnUnloaded()
        {
            if (!ReferenceEquals(lastControl, this))
            {
                return;
            }

            lastControl = null;
            // reset interaction tooltip (so it will be displayed again)
            InteractionTooltip.Hide();
        }
    }
};