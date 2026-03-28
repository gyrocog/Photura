using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Photura.Models;
using Photura.ViewModels;

namespace Photura.Views
{
    public partial class EditView : Window
    {
        private readonly EditViewModel _vm;
        private bool _isLoaded = false;
        private bool _updatingZoomSlider = false;
        private double _zoom = 1.0;

        private double _imgX = 0;
        private double _imgY = 0;

        private bool _isPanning = false;
        private Point _panStart;
        private double _panStartX;
        private double _panStartY;

        public EditView(ImageFile file)
        {
            InitializeComponent();
            _vm = new EditViewModel();
            DataContext = _vm;

            _vm.PropertyChanged += Vm_PropertyChanged;
            _vm.DoneRequested += OnDoneRequested;
            _vm.ImageResampled += OnImageResampled;

            CropScenePanel.AspectRatioChanged += CropScene_AspectRatioChanged;
            CropScenePanel.RotationChanged += CropScene_RotationChanged;
            CropScenePanel.FlipHRequested += (s, e) => DoFlip(true);
            CropScenePanel.FlipVRequested += (s, e) => DoFlip(false);
            CropScenePanel.ShrinkRequested += async (s, e) => await _vm.ResampleByFactorAsync(1.0 / 1.05);
            CropScenePanel.ShrinkLargeRequested += async (s, e) => await _vm.ResampleByFactorAsync(1.0 / 1.20);
            CropScenePanel.EnlargeRequested += async (s, e) => await _vm.ResampleByFactorAsync(1.05);
            CropScenePanel.EnlargeLargeRequested += async (s, e) => await _vm.ResampleByFactorAsync(1.20);
            CropScenePanel.FitImageRequested += async (s, e) => await FitImageToCropAsync();

            Loaded += (s, e) =>
            {
                _isLoaded = true;
                _vm.LoadImage(file);
                FileNameLabel.Text = _vm.FileName;
                UpdateImageDisplay();
            };

            ImageCanvas.MouseDown += ImageCanvas_MouseDown;
            ImageCanvas.MouseUp += ImageCanvas_MouseUp;
            ImageCanvas.MouseMove += ImageCanvas_MouseMove;
        }

        // ── Compare toggle ────────────────────────────────────────
        private void CompareToggle_Checked(object sender, RoutedEventArgs e)
        {
            _vm.IsComparing = true;
        }

        private void CompareToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            _vm.IsComparing = false;
        }

        // ── Scene tabs ────────────────────────────────────────────
        private void SceneTab_Checked(object sender, RoutedEventArgs e)
        {
            if (CropScenePanel == null) return;
            CropScenePanel.Visibility = Visibility.Collapsed;
            AdjustScenePanel.Visibility = Visibility.Collapsed;
            FilterScenePanel.Visibility = Visibility.Collapsed;
            CropOverlayControl.Visibility = Visibility.Collapsed;

            if (CropTab.IsChecked == true)
            {
                CropScenePanel.Visibility = Visibility.Visible;
                CropOverlayControl.Visibility = Visibility.Visible;
                _vm.ActiveScene = "Crop";
            }
            else if (AdjustTab.IsChecked == true)
            {
                AdjustScenePanel.Visibility = Visibility.Visible;
                _vm.ActiveScene = "Adjustments";
            }
            else if (FilterTab.IsChecked == true)
            {
                FilterScenePanel.Visibility = Visibility.Visible;
                _vm.ActiveScene = "Filters";
            }
        }

        // ── Canvas resize ─────────────────────────────────────────
        private void CanvasBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_isLoaded || _vm.PreviewBitmap == null) return;
            FitToCanvas();
        }

        // ── Crop overlay ─────────────────────────────────────────
        private void CropOverlay_CropChanged(Rect cropRect)
        {
            _vm.State.CropRect = cropRect;
            _vm.State.IsCropModified = true;
            UpdateCropInfo();
            CropScenePanel.UpdateCropInfo(cropRect);
        }

        private void UpdateCropInfo()
        {
            var r = _vm.State.CropRect;
            CropInfoLabel.Text = $"Crop: {(int)r.Width} × {(int)r.Height} px";
        }

        // ── Aspect ratio ──────────────────────────────────────────
        private void CropScene_AspectRatioChanged(object? sender, EventArgs e)
        {
            double ratio = CropScenePanel.SelectedAspectRatio;
            if (ratio == -1)
                CropOverlayControl.SetOriginalAspectRatio();
            else
                CropOverlayControl.SetAspectRatio(ratio);
        }

        // ── Straighten ────────────────────────────────────────────
        private void CropScene_RotationChanged(object? sender, EventArgs e)
        {
            double angle = Math.Round(CropScenePanel.RotationAngle);
            _vm.State.RotationAngle = angle;
            ImageRotation.Angle = angle;
            ApplyImageTransform();
        }

        // ── Flip ─────────────────────────────────────────────────
        private void DoFlip(bool horizontal)
        {
            var crop = _vm.State.CropRect;
            double cropCenterCanvasX = _imgX + (crop.X + crop.Width / 2) * _zoom;
            double cropCenterCanvasY = _imgY + (crop.Y + crop.Height / 2) * _zoom;

            _vm.FlipImage(horizontal);

            var newCrop = _vm.State.CropRect;
            _imgX = cropCenterCanvasX - (newCrop.X + newCrop.Width / 2) * _zoom;
            _imgY = cropCenterCanvasY - (newCrop.Y + newCrop.Height / 2) * _zoom;
        }

        // ── Resample ──────────────────────────────────────────────
        private void OnImageResampled()
        {
            if (!_isLoaded || _vm.PreviewBitmap == null) return;

            var crop = _vm.State.CropRect;
            double cropCenterCanvasX = _imgX + (crop.X + crop.Width / 2) * _zoom;
            double cropCenterCanvasY = _imgY + (crop.Y + crop.Height / 2) * _zoom;

            MainImage.Source = _vm.PreviewBitmap;
            ImageSizeLabel.Text = _vm.ImageSizeText;

            _imgX = cropCenterCanvasX - (crop.X + crop.Width / 2) * _zoom;
            _imgY = cropCenterCanvasY - (crop.Y + crop.Height / 2) * _zoom;

            CropOverlayControl.Initialize(
                _vm.PreviewBitmap.PixelWidth,
                _vm.PreviewBitmap.PixelHeight,
                _zoom, _imgX, _imgY);

            CropOverlayControl.SetCropRect(_vm.State.CropRect);
            CropScenePanel.UpdateCropInfo(_vm.State.CropRect);
            UpdateCropInfo();
            ApplyImageTransform();
        }

        private async System.Threading.Tasks.Task FitImageToCropAsync()
        {
            if (_vm.PreviewBitmap == null) return;
            var crop = _vm.State.CropRect;
            if (crop.Width <= 0 || crop.Height <= 0) return;

            double factorW = crop.Width / (double)_vm.PreviewBitmap.PixelWidth;
            double factorH = crop.Height / (double)_vm.PreviewBitmap.PixelHeight;
            double factor = Math.Max(factorW, factorH);

            await _vm.ResampleByFactorAsync(factor);
        }

        // ── Adjustments ──────────────────────────────────────────
        private void AdjustScene_Changed(object? sender, EventArgs e)
        {
            AdjustScenePanel.ApplyToState(_vm.State);
            _vm.NotifyAdjustmentChanged();
        }

        // ── Filters ──────────────────────────────────────────────
        private void FilterScene_Selected(string filterName, double intensity)
        {
            _vm.SetFilterAndIntensity(filterName, intensity);
        }

        // ── Done ─────────────────────────────────────────────────
        private void Done_Click(object sender, RoutedEventArgs e)
            => _vm.DoneCommand.Execute(null);

        private void OnDoneRequested()
        {
            Owner?.Show();
            Close();
        }

        // ── Mousewheel zoom ───────────────────────────────────────
        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            e.Handled = true;

            Point cursorPos = e.GetPosition(ImageCanvas);
            double oldZoom = _zoom;
            double factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
            double newZoom = Math.Clamp(oldZoom * factor, 0.05, 8.0);

            double imagePointX = (cursorPos.X - _imgX) / oldZoom;
            double imagePointY = (cursorPos.Y - _imgY) / oldZoom;

            _zoom = newZoom;
            _imgX = cursorPos.X - imagePointX * newZoom;
            _imgY = cursorPos.Y - imagePointY * newZoom;

            ApplyImageTransform();
        }

        // ── Panning ───────────────────────────────────────────────
        private void ImageCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed ||
                e.RightButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _panStart = e.GetPosition(ImageCanvas);
                _panStartX = _imgX;
                _panStartY = _imgY;
                ImageCanvas.CaptureMouse();
                ImageCanvas.Cursor = Cursors.ScrollAll;
                e.Handled = true;
            }
        }

        private void ImageCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning &&
                (e.MiddleButton == MouseButtonState.Released ||
                 e.RightButton == MouseButtonState.Released))
            {
                _isPanning = false;
                ImageCanvas.ReleaseMouseCapture();
                ImageCanvas.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        private void ImageCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;
            Point current = e.GetPosition(ImageCanvas);
            _imgX = _panStartX + (current.X - _panStart.X);
            _imgY = _panStartY + (current.Y - _panStart.Y);
            ApplyImageTransform();
            e.Handled = true;
        }

        // ── Zoom controls ─────────────────────────────────────────
        private void ZoomIn_Click(object sender, RoutedEventArgs e) => ZoomAtCenter(_zoom * 1.15);
        private void ZoomOut_Click(object sender, RoutedEventArgs e) => ZoomAtCenter(_zoom / 1.15);
        private void ZoomFit_Click(object sender, RoutedEventArgs e) => FitToCanvas();
        private void Zoom100_Click(object sender, RoutedEventArgs e)
        {
            _zoom = 1.0;
            CenterImage();
            ApplyImageTransform();
        }

        private void ZoomSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingZoomSlider || !_isLoaded) return;
            ZoomAtCenter(e.NewValue / 100.0);
        }

        private void ZoomAtCenter(double newZoom)
        {
            if (!_isLoaded || _vm.PreviewBitmap == null) return;
            double cx = CanvasBorder.ActualWidth / 2;
            double cy = CanvasBorder.ActualHeight / 2;

            double ipX = (cx - _imgX) / _zoom;
            double ipY = (cy - _imgY) / _zoom;

            _zoom = Math.Clamp(newZoom, 0.05, 8.0);
            _imgX = cx - ipX * _zoom;
            _imgY = cy - ipY * _zoom;

            ApplyImageTransform();
        }

        // ── Core transform ────────────────────────────────────────
        private void ApplyImageTransform()
        {
            if (!_isLoaded || _vm.PreviewBitmap == null) return;

            Canvas.SetLeft(MainImage, _imgX);
            Canvas.SetTop(MainImage, _imgY);
            ImageScale.ScaleX = _zoom;
            ImageScale.ScaleY = _zoom;

            CropOverlayControl.Width = CanvasBorder.ActualWidth;
            CropOverlayControl.Height = CanvasBorder.ActualHeight;
            Canvas.SetLeft(CropOverlayControl, 0);
            Canvas.SetTop(CropOverlayControl, 0);

            CropOverlayControl.SetZoom(_zoom, _imgX, _imgY);

            ZoomLabel.Text = $"{(int)(_zoom * 100)}%";
            _updatingZoomSlider = true;
            ZoomSlider.Value = _zoom * 100;
            _updatingZoomSlider = false;

            _vm.ZoomLevel = _zoom;
            UpdateCropInfo();
        }

        private void CenterImage()
        {
            if (_vm.PreviewBitmap == null) return;
            _imgX = (CanvasBorder.ActualWidth - _vm.PreviewBitmap.PixelWidth * _zoom) / 2;
            _imgY = (CanvasBorder.ActualHeight - _vm.PreviewBitmap.PixelHeight * _zoom) / 2;
        }

        private void FitToCanvas()
        {
            if (!_isLoaded || _vm.PreviewBitmap == null) return;
            double availW = CanvasBorder.ActualWidth;
            double availH = CanvasBorder.ActualHeight;
            if (availW <= 0 || availH <= 0) return;

            double fit = Math.Min(
                availW / _vm.PreviewBitmap.PixelWidth,
                availH / _vm.PreviewBitmap.PixelHeight);
            _zoom = Math.Min(fit, 1.0);
            CenterImage();
            ApplyImageTransform();
        }

        // ── Image display ─────────────────────────────────────────
        private void UpdateImageDisplay()
        {
            if (!_isLoaded || _vm.PreviewBitmap == null) return;

            MainImage.Source = _vm.PreviewBitmap;
            ImageSizeLabel.Text = _vm.ImageSizeText;

            CropOverlayControl.Initialize(
                _vm.PreviewBitmap.PixelWidth,
                _vm.PreviewBitmap.PixelHeight,
                _zoom, _imgX, _imgY);

            CropOverlayControl.SetCropRect(_vm.State.CropRect);
            CropScenePanel.UpdateCropInfo(_vm.State.CropRect);
            UpdateCropInfo();

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
                FitToCanvas());
        }

        // ── Keyboard ─────────────────────────────────────────────
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                _vm.SaveCommand.Execute(null);
            if (e.Key == Key.S &&
                Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                _vm.SaveAsCommand.Execute(null);
            if (e.Key == Key.Escape)
                _vm.DoneCommand.Execute(null);
        }

        // ── ViewModel events ──────────────────────────────────────
        private void Vm_PropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditViewModel.PreviewBitmap))
                UpdateImageDisplay();

            if (e.PropertyName == nameof(EditViewModel.IsSaving))
            {
                SavingLabel.Visibility = _vm.IsSaving
                    ? Visibility.Visible : Visibility.Collapsed;
            }

            if (e.PropertyName == nameof(EditViewModel.IsResampling))
            {
                ResamplingLabel.Visibility = _vm.IsResampling
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}