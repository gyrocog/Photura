using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Photura.Views.Scenes
{
    public partial class FiltersScene : UserControl
    {
        public event System.Action<string, double>? FilterSelected;

        private static readonly string[] Filters =
        {
            "None",
            "Vivid", "Warm", "Cool", "Grayscale", "Sepia",
            "Fade", "Matte", "Noir", "Chrome",
            "Kodachrome", "Fuji", "Polaroid", "Lomo", "Cross Process",
            "Golden Hour", "Moonlight", "Haze", "Cinematic", "Bleach Bypass",
            "Duotone", "Infrared", "Halation", "Faded Kodak"
        };

        private ToggleButton? _activeBtn;
        private string _activeFilter = "None";

        public string ActiveFilter => _activeFilter;
        public double FilterIntensity => IntensitySlider?.Value ?? 100;

        public FiltersScene()
        {
            InitializeComponent();
            Loaded += (s, e) =>
            {
                BuildButtons();
                // Force intensity to 100% after all controls are loaded
                IntensitySlider.Value = 100;
            };
        }

        private void BuildButtons()
        {
            FilterPanel.Children.Clear();
            foreach (var name in Filters)
            {
                var btn = new ToggleButton
                {
                    Content = name,
                    Style = (Style)FindResource("ToggleButtonStyle"),
                    Margin = new Thickness(2),
                    Padding = new Thickness(10, 6, 10, 6),
                    FontSize = 12,
                    Tag = name
                };

                if (name == "None")
                {
                    btn.IsChecked = true;
                    _activeBtn = btn;
                }

                btn.Click += FilterBtn_Click;
                FilterPanel.Children.Add(btn);
            }
        }

        private void FilterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn) return;
            if (_activeBtn != null) _activeBtn.IsChecked = false;
            _activeBtn = btn;
            btn.IsChecked = true;
            _activeFilter = (string)btn.Tag;
            FilterSelected?.Invoke(_activeFilter, FilterIntensity);
        }

        private void IntensitySlider_ValueChanged(object sender,
            System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            FilterSelected?.Invoke(_activeFilter, e.NewValue);
        }

        public void ResetToNone()
        {
            foreach (ToggleButton btn in FilterPanel.Children)
            {
                if ((string)btn.Tag == "None")
                {
                    if (_activeBtn != null) _activeBtn.IsChecked = false;
                    btn.IsChecked = true;
                    _activeBtn = btn;
                    _activeFilter = "None";
                    break;
                }
            }
        }
    }
}