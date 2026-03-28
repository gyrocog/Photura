using System;
using System.Windows;
using System.Windows.Controls;
using Photura.Models;

namespace Photura.Views.Scenes
{
    public partial class AdjustmentsScene : UserControl
    {
        public event EventHandler? AdjustmentChanged;

        public AdjustmentsScene() => InitializeComponent();

        private void Slider_ValueChanged(object sender,
            System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            AdjustmentChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ApplyToState(EditState state)
        {
            state.Brightness = BrightnessSlider.Value;
            state.Exposure   = ExposureSlider.Value;
            state.Contrast   = ContrastSlider.Value;
            state.Highlights = HighlightsSlider.Value;
            state.Shadows    = ShadowsSlider.Value;
            state.Vignette   = VignetteSlider.Value;
            state.Saturation = SaturationSlider.Value;
            state.Warmth     = WarmthSlider.Value;
            state.Tint       = TintSlider.Value;
            state.Sharpness  = SharpnessSlider.Value;
        }

        private void ResetAll_Click(object sender, RoutedEventArgs e)
        {
            BrightnessSlider.ResetToZero();
            ExposureSlider.ResetToZero();
            ContrastSlider.ResetToZero();
            HighlightsSlider.ResetToZero();
            ShadowsSlider.ResetToZero();
            VignetteSlider.ResetToZero();
            SaturationSlider.ResetToZero();
            WarmthSlider.ResetToZero();
            TintSlider.ResetToZero();
            SharpnessSlider.ResetToZero();
            AdjustmentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}