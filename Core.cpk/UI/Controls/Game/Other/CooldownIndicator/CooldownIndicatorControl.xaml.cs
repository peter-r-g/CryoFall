﻿namespace AtomicTorch.CBND.CoreMod.UI.Controls.Game.Other.CooldownIndicator
{
    using System.Windows;
    using AtomicTorch.GameEngine.Common.Client.MonoGame.UI;
    using AtomicTorch.GameEngine.Common.Primitives;

    public partial class CooldownIndicatorControl : BaseUserControl
    {
        public static readonly DependencyProperty SetTotalDurationProperty
            = DependencyProperty.Register(
                nameof(SetTotalDuration),
                typeof(double),
                typeof(CooldownIndicatorControl),
                new PropertyMetadata(0.0, SetTotalDurationPropertyValueChanged));

        public static readonly DependencyProperty ViewModelProperty
            = DependencyProperty.Register(nameof(ViewModel),
                                          typeof(ViewModelCooldownIndicatorControl),
                                          typeof(CooldownIndicatorControl),
                                          new PropertyMetadata(default(ViewModelCooldownIndicatorControl)));

        private ViewModelCooldownIndicatorControl viewModel;

        public double SetTotalDuration
        {
            get => (double)this.GetValue(SetTotalDurationProperty);
            set => this.SetValue(SetTotalDurationProperty, value);
        }

        public ViewModelCooldownIndicatorControl ViewModel
        {
            get => (ViewModelCooldownIndicatorControl)this.GetValue(ViewModelProperty);
            set => this.SetValue(ViewModelProperty, value);
        }

        public void TurnOn(double cooldownDurationSeconds)
        {
            if (this.isLoaded)
            {
                this.viewModel?.TurnOn(cooldownDurationSeconds);
            }
        }

        protected override void OnLoaded()
        {
            this.ViewModel = this.viewModel = new ViewModelCooldownIndicatorControl();

            this.UpdateSize();
            this.SizeChanged += this.SizeChangedHandler;

            if (this.SetTotalDuration > 0)
            {
                this.TurnOn(this.SetTotalDuration);
            }
        }

        protected override void OnUnloaded()
        {
            this.viewModel.TurnOff();
            this.SizeChanged -= this.SizeChangedHandler;

            this.viewModel.Dispose();
        }

        private static void SetTotalDurationPropertyValueChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            ((CooldownIndicatorControl)d).TurnOn((double)e.NewValue);
        }

        private void SizeChangedHandler(object sender, SizeChangedEventArgs e)
        {
            this.UpdateSize();
        }

        private void UpdateSize()
        {
            this.viewModel.ControlSize = new Vector2Ushort((ushort)this.ActualWidth, (ushort)this.ActualHeight);
        }
    }
}