﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Game.HUD.Notifications
{
    using System;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media;
    using System.Windows.Media.Animation;
    using AtomicTorch.CBND.CoreMod.SoundPresets;
    using AtomicTorch.CBND.CoreMod.Systems.Cursor;
    using AtomicTorch.CBND.CoreMod.UI.Controls.Game.HUD.Notifications.Data;
    using AtomicTorch.CBND.GameApi.Resources;
    using AtomicTorch.CBND.GameApi.Scripting;
    using AtomicTorch.CBND.GameApi.Scripting.ClientComponents;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;

    public partial class HudNotificationControl : BaseUserControl, IHudNotificationControl
    {
        public Action CallbackOnRightClickHide;

        public SoundResource soundToPlay;

        private Control outerBorder;

        private FrameworkElement root;

        private Storyboard storyboardFadeOut;

        private Storyboard storyboardHide;

        private Storyboard storyboardShow;

        private ViewModelHudNotificationControl viewModel;

        public bool IsAutoHide { get; private set; }

        public bool IsHiding { get; private set; }

        public string Message
        {
            get => this.viewModel?.Message;
            set
            {
                if (this.viewModel is null
                    || string.Equals(this.viewModel.Message, value, StringComparison.Ordinal))
                {
                    return;
                }

                this.viewModel.Message = value;
                this.ResetNotificationSize();
            }
        }

        public string Title
        {
            get => this.viewModel?.Title;
            set
            {
                if (this.viewModel is null
                    || string.Equals(this.viewModel.Title, value, StringComparison.Ordinal))
                {
                    return;
                }

                this.viewModel.Title = value;
                this.ResetNotificationSize();
            }
        }

        public ViewModelHudNotificationControl ViewModel => this.viewModel;

        public static HudNotificationControl Create(
            string title,
            string message,
            Brush brushBackground,
            Brush brushBorder,
            ITextureResource icon,
            Action onClick,
            bool autoHide,
            SoundResource soundToPlay)
        {
            var iconBrush = icon is not null
                                ? Api.Client.UI.GetTextureBrush(icon)
                                : null;

            return new HudNotificationControl()
            {
                viewModel = new ViewModelHudNotificationControl(
                    title,
                    message,
                    brushBackground,
                    brushBorder,
                    iconBrush,
                    onClick),
                IsAutoHide = autoHide,
                soundToPlay = soundToPlay
            };
        }

        public void Hide(bool quick)
        {
            if (quick)
            {
                this.storyboardFadeOut.SpeedRatio = 6.5;
            }

            if (this.IsHiding)
            {
                // already hiding
                return;
            }

            this.IsHiding = true;

            if (!this.isLoaded)
            {
                this.RemoveControl();
                return;
            }

            this.storyboardShow.Stop();
            this.storyboardFadeOut.Begin();
        }

        public void HideAfterDelay(double delaySeconds)
        {
            this.IsAutoHide = false;

            // hide the notification control after delay
            ClientTimersSystem.AddAction(
                delaySeconds,
                () => this.Hide(quick: false));
        }

        public bool IsSame(IHudNotificationControl other)
        {
            return other is HudNotificationControl otherControl
                   && this.viewModel.IsSame(otherControl.viewModel);
        }

        public void SetupAutoHideChecker(Func<bool> checker)
        {
            Api.Client.Scene.CreateSceneObject("Notification auto hide checker")
               .AddComponent<ClientComponentNotificationAutoHideChecker>()
               .Setup(this, checker);
        }

        protected override void InitControl()
        {
            if (IsDesignTime)
            {
                return;
            }

            this.storyboardShow = this.GetResource<Storyboard>("StoryboardShow");
            this.storyboardHide = this.GetResource<Storyboard>("StoryboardHide");
            this.storyboardFadeOut = this.GetResource<Storyboard>("StoryboardFadeOut");
            this.outerBorder = this.GetByName<Control>("OuterBorder");
            this.root = this.GetByName<FrameworkElement>("LayoutRoot");
            this.DataContext = this.viewModel;
        }

        protected override void OnLoaded()
        {
            this.ResetNotificationSize();

            if (IsDesignTime)
            {
                return;
            }

            this.storyboardFadeOut.Completed += this.StoryboardFadeOutCompletedHandler;
            this.storyboardHide.Completed += this.StoryboardHideCompletedHandler;
            this.root.MouseLeftButtonDown += this.RootMouseButtonLeftHandler;
            this.root.MouseRightButtonDown += this.RootMouseButtonRightHandler;
            this.root.MouseEnter += this.RootMouseEnterHandler;
            this.root.MouseLeave += this.RootMouseLeaveHandler;

            this.storyboardShow.Begin();

            if (this.soundToPlay is not null)
            {
                Api.Client.Audio.PlayOneShot(this.soundToPlay,
                                             SoundConstants.VolumeUINotifications);
            }
        }

        protected override void OnUnloaded()
        {
            if (IsDesignTime)
            {
                return;
            }

            this.DataContext = null;
            this.viewModel.Dispose();
            this.viewModel = null;

            this.storyboardFadeOut.Completed -= this.StoryboardFadeOutCompletedHandler;
            this.storyboardHide.Completed -= this.StoryboardHideCompletedHandler;
            this.root.MouseLeftButtonDown -= this.RootMouseButtonLeftHandler;
            this.root.MouseRightButtonDown -= this.RootMouseButtonRightHandler;
            this.root.MouseEnter -= this.RootMouseEnterHandler;
            this.root.MouseLeave -= this.RootMouseLeaveHandler;

            // to ensure that the control has a hiding flag (used for ClientComponentNotificationAutoHideChecker)
            this.IsHiding = true;

            this.RemoveControl();
        }

        private void RemoveControl()
        {
            var parent = this.Parent as Panel;
            parent?.Children.Remove(this);
        }

        private void ResetNotificationSize()
        {
            this.UpdateLayout();
            this.root.Measure(new Size(this.outerBorder.ActualWidth, 1000));
            this.viewModel.RequiredHeight = (float)(this.root.DesiredSize.Height
                                                    + this.outerBorder.Padding.Top
                                                    + this.outerBorder.Padding.Bottom);
        }

        private void RootMouseButtonLeftHandler(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            this.Hide(quick: true);
            this.viewModel.CommandClick?.Execute(null);
        }

        private void RootMouseButtonRightHandler(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            this.CallbackOnRightClickHide?.Invoke();
            this.Hide(quick: true);
        }

        private void RootMouseEnterHandler(object sender, MouseEventArgs e)
        {
            ClientCursorSystem.CurrentCursorId = this.viewModel.Cursor;
        }

        private void RootMouseLeaveHandler(object sender, MouseEventArgs e)
        {
            ClientCursorSystem.CurrentCursorId = CursorId.Default;
        }

        private void StoryboardFadeOutCompletedHandler(object sender, EventArgs e)
        {
            this.storyboardHide.Begin();
        }

        private void StoryboardHideCompletedHandler(object sender, EventArgs e)
        {
            this.RemoveControl();
        }

        private class ClientComponentNotificationAutoHideChecker : ClientComponent
        {
            private Func<bool> checker;

            private HudNotificationControl control;

            public void Setup(HudNotificationControl control, Func<bool> checker)
            {
                this.control = control;
                this.checker = checker;
            }

            public override void Update(double deltaTime)
            {
                if (this.control.IsHiding)
                {
                    // checker is not required anymore
                    this.SceneObject.Destroy();
                    return;
                }

                if (!this.checker())
                {
                    return;
                }

                // auto hide check success - hide the notification
                this.control.Hide(quick: false);
                this.SceneObject.Destroy();
            }
        }
    }
}