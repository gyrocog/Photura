using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Photura.Controls
{
    public partial class EditSliderRow : UserControl
    {
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string),
                typeof(EditSliderRow),
                new PropertyMetadata("Label", OnLabelChanged));

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(double),
                typeof(EditSliderRow),
                new PropertyMetadata(0.0, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register(nameof(Minimum), typeof(double),
                typeof(EditSliderRow),
                new PropertyMetadata(-100.0));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register(nameof(Maximum), typeof(double),
                typeof(EditSliderRow),
                new PropertyMetadata(100.0));

        public static readonly DependencyProperty DefaultValueProperty =
            DependencyProperty.Register(nameof(DefaultValue), typeof(double),
                typeof(EditSliderRow),
                new PropertyMetadata(0.0));

        // How much each mousewheel tick moves the slider
        public static readonly DependencyProperty WheelStepProperty =
            DependencyProperty.Register(nameof(WheelStep), typeof(double),
                typeof(EditSliderRow),
                new PropertyMetadata(1.0));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public double DefaultValue
        {
            get => (double)GetValue(DefaultValueProperty);
            set => SetValue(DefaultValueProperty, value);
        }

        public double WheelStep
        {
            get => (double)GetValue(WheelStepProperty);
            set => SetValue(WheelStepProperty, value);
        }

        public event RoutedPropertyChangedEventHandler<double>? ValueChanged;

        public EditSliderRow()
        {
            InitializeComponent();

            Loaded += (s, e) =>
            {
                TheSlider.Minimum = Minimum;
                TheSlider.Maximum = Maximum;
                TheSlider.Value = System.Math.Round(Value);
                LabelText.Text = Label;
                ValueText.Text = ((int)Value).ToString();
            };

            MouseDoubleClick += (s, e) => ResetToDefault();

            // Mousewheel scrolls the slider value
            PreviewMouseWheel += (s, e) =>
            {
                e.Handled = true; // prevent parent scroll
                double step = e.Delta > 0 ? WheelStep : -WheelStep;
                double newVal = System.Math.Clamp(
                    TheSlider.Value + step,
                    Minimum, Maximum);
                TheSlider.Value = System.Math.Round(newVal);
            };
        }

        private static void OnLabelChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (EditSliderRow)d;
            if (ctrl.LabelText != null)
                ctrl.LabelText.Text = e.NewValue?.ToString();
        }

        private static void OnValueChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (EditSliderRow)d;
            if (ctrl.TheSlider != null)
                ctrl.TheSlider.Value = System.Math.Round((double)e.NewValue);
        }

        private bool _suppressEvent = false;

        private void TheSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (_suppressEvent) return;

            double rounded = System.Math.Round(e.NewValue);
            ValueText.Text = ((int)rounded).ToString();

            _suppressEvent = true;
            Value = rounded;
            TheSlider.Value = rounded;
            _suppressEvent = false;

            ValueChanged?.Invoke(this, new RoutedPropertyChangedEventArgs<double>(
                System.Math.Round(e.OldValue), rounded));
        }

        public void ResetToDefault()
        {
            double target = System.Math.Round(DefaultValue);
            _suppressEvent = true;
            TheSlider.Value = target;
            Value = target;
            ValueText.Text = ((int)target).ToString();
            _suppressEvent = false;

            ValueChanged?.Invoke(this,
                new RoutedPropertyChangedEventArgs<double>(Value, target));
        }

        public void ResetToZero() => ResetToDefault();
    }
}