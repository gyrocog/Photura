using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Photura.Views.Scenes
{
    public partial class CropScene : UserControl
    {
        private static readonly (string Label, double Ratio)[] Presets =
        {
            ("Free",      0),
            ("Original", -1),
            ("1:1",       1.0),
            ("9:16",      9.0  / 16.0),
            ("16:9",      16.0 / 9.0),
            ("4:5",       4.0  / 5.0),
            ("5:4",       5.0  / 4.0),
            ("3:4",       3.0  / 4.0),
            ("4:3",       4.0  / 3.0),
            ("2:3",       2.0  / 3.0),
            ("3:2",       3.0  / 2.0),
            ("5:7",       5.0  / 7.0),
            ("7:5",       7.0  / 5.0),
            ("1:2",       1.0  / 2.0),
            ("2:1",       2.0  / 1.0),
        };

        public event System.EventHandler? AspectRatioChanged;
        public event System.EventHandler? RotationChanged;
        public event System.EventHandler? FlipHRequested;
        public event System.EventHandler? FlipVRequested;
        public event System.EventHandler? ShrinkRequested;
        public event System.EventHandler? ShrinkLargeRequested;
        public event System.EventHandler? EnlargeRequested;
        public event System.EventHandler? EnlargeLargeRequested;
        public event System.EventHandler? FitImageRequested;

        public double SelectedAspectRatio { get; private set; } = 0;
        public double RotationAngle => System.Math.Round(RotationSlider.Value);

        private ToggleButton? _activeAspectBtn;
        private bool _buttonsBuilt = false;

        public CropScene()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                if (_buttonsBuilt) return;
                _buttonsBuilt = true;
                BuildAspectButtons();

                // Double-click rotation slider to reset
                RotationSlider.MouseDoubleClick += (ss, ee) =>
                {
                    RotationSlider.Value = 0;
                    RotationValueLabel.Text = "0°";
                    RotationChanged?.Invoke(this, System.EventArgs.Empty);
                };
            };
        }

        private void BuildAspectButtons()
        {
            AspectPanel.Children.Clear();
            foreach (var (label, ratio) in Presets)
            {
                var btn = new ToggleButton
                {
                    Content = label,
                    Style = (Style)FindResource("ToggleButtonStyle"),
                    Margin = new Thickness(2),
                    Padding = new Thickness(10, 8, 10, 8),
                    FontSize = 13,
                    Tag = ratio,
                    MinWidth = 48
                };

                if (label == "Free")
                {
                    btn.IsChecked = true;
                    _activeAspectBtn = btn;
                }

                btn.Click += AspectBtn_Click;
                AspectPanel.Children.Add(btn);
            }
        }

        private void AspectBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn) return;
            if (_activeAspectBtn != null) _activeAspectBtn.IsChecked = false;
            _activeAspectBtn = btn;
            btn.IsChecked = true;
            SelectedAspectRatio = (double)btn.Tag;
            AspectRatioChanged?.Invoke(this, System.EventArgs.Empty);
        }

        private void RotationSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (RotationValueLabel != null)
            {
                int rounded = (int)System.Math.Round(e.NewValue);
                RotationValueLabel.Text = $"{rounded}°";
            }
            RotationChanged?.Invoke(this, System.EventArgs.Empty);
        }

        private void RotationSlider_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            double step = e.Delta > 0 ? 1.0 : -1.0;
            double newVal = System.Math.Clamp(RotationSlider.Value + step, -45, 45);
            RotationSlider.Value = System.Math.Round(newVal);
        }

        // Image resize buttons
        private void Shrink_Click(object sender, RoutedEventArgs e)
            => ShrinkRequested?.Invoke(this, System.EventArgs.Empty);

        private void ShrinkLarge_Click(object sender, RoutedEventArgs e)
            => ShrinkLargeRequested?.Invoke(this, System.EventArgs.Empty);

        private void Enlarge_Click(object sender, RoutedEventArgs e)
            => EnlargeRequested?.Invoke(this, System.EventArgs.Empty);

        private void EnlargeLarge_Click(object sender, RoutedEventArgs e)
            => EnlargeLargeRequested?.Invoke(this, System.EventArgs.Empty);

        private void FitImage_Click(object sender, RoutedEventArgs e)
            => FitImageRequested?.Invoke(this, System.EventArgs.Empty);

        public void UpdateCropInfo(System.Windows.Rect cropRect)
        {
            CropSizeLabel.Text = $"{(int)cropRect.Width} × {(int)cropRect.Height} px";
            CropPositionLabel.Text = $"X: {(int)cropRect.X}  Y: {(int)cropRect.Y}";
        }

        public void ResetRotation()
        {
            RotationSlider.Value = 0;
            RotationValueLabel.Text = "0°";
        }
    }
}