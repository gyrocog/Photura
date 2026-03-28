using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Photura.Models;
using Photura.Services;

namespace Photura.ViewModels
{
    public class EditViewModel : INotifyPropertyChanged
    {
        public event Action? DoneRequested;
        public event Action? ImageResampled;
        public event PropertyChangedEventHandler? PropertyChanged;

        private ImageFile? _file;
        private BitmapSource? _originalBitmap;
        private BitmapSource? _cleanBitmap;    // no adjustments — for compare mode
        public EditState State { get; } = new();

        private BitmapSource? _previewBitmap;
        public BitmapSource? PreviewBitmap
        {
            get => _previewBitmap;
            private set { _previewBitmap = value; OnPropertyChanged(); }
        }

        private string _fileName = string.Empty;
        public string FileName
        {
            get => _fileName;
            private set { _fileName = value; OnPropertyChanged(); }
        }

        private string _imageSizeText = string.Empty;
        public string ImageSizeText
        {
            get => _imageSizeText;
            private set { _imageSizeText = value; OnPropertyChanged(); }
        }

        private double _zoomLevel = 1.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set { _zoomLevel = Math.Clamp(value, 0.05, 8.0); OnPropertyChanged(); }
        }

        private string _activeScene = "Crop";
        public string ActiveScene
        {
            get => _activeScene;
            set { _activeScene = value; OnPropertyChanged(); }
        }

        private bool _isComparing = false;
        public bool IsComparing
        {
            get => _isComparing;
            set
            {
                _isComparing = value;
                OnPropertyChanged();
                // Show clean or processed bitmap
                PreviewBitmap = _isComparing ? _cleanBitmap : GetProcessedBitmap();
            }
        }

        private bool _isResampling = false;
        public bool IsResampling
        {
            get => _isResampling;
            set { _isResampling = value; OnPropertyChanged(); }
        }

        private bool _isSaving = false;
        public bool IsSaving
        {
            get => _isSaving;
            private set { _isSaving = value; OnPropertyChanged(); }
        }

        public int OriginalWidth { get; private set; }
        public int OriginalHeight { get; private set; }

        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand DoneCommand { get; }
        public ICommand RotateCWCommand { get; }
        public ICommand RotateCCWCommand { get; }
        public ICommand FlipHCommand { get; }
        public ICommand FlipVCommand { get; }
        public ICommand ResetCommand { get; }

        public EditViewModel()
        {
            SaveCommand = new RelayCommand(Save, () => _originalBitmap != null && !IsResampling && !IsSaving);
            SaveAsCommand = new RelayCommand(SaveAs, () => _originalBitmap != null && !IsResampling && !IsSaving);
            DoneCommand = new RelayCommand(Done);
            RotateCWCommand = new RelayCommand(() => RotateBy(90), () => _originalBitmap != null);
            RotateCCWCommand = new RelayCommand(() => RotateBy(-90), () => _originalBitmap != null);
            FlipHCommand = new RelayCommand(() => FlipImage(true), () => _originalBitmap != null);
            FlipVCommand = new RelayCommand(() => FlipImage(false), () => _originalBitmap != null);
            ResetCommand = new RelayCommand(ResetAll, () => _originalBitmap != null);
        }

        public void LoadImage(ImageFile file)
        {
            _file = file;
            _originalBitmap = ImageLoader.Load(file);
            _cleanBitmap = _originalBitmap;
            FileName = file.FileName;
            OriginalWidth = _originalBitmap.PixelWidth;
            OriginalHeight = _originalBitmap.PixelHeight;
            State.Reset();
            State.CropRect = new Rect(0, 0,
                _originalBitmap.PixelWidth, _originalBitmap.PixelHeight);
            ZoomLevel = 1.0;
            ActiveScene = "Crop";
            RefreshPreview();
        }

        public void NotifyAdjustmentChanged()
        {
            if (!_isComparing) RefreshPreview();
        }

        public void SetFilterAndIntensity(string filter, double intensity)
        {
            State.ActiveFilter = filter;
            State.FilterIntensity = intensity;
            if (!_isComparing) RefreshPreview();
        }

        public void SetRotationAngle(double degrees)
        {
            State.RotationAngle = degrees;
        }

        public async Task ResampleByFactorAsync(double factor)
        {
            if (_originalBitmap == null || IsResampling) return;

            int oldW = _originalBitmap.PixelWidth;
            int oldH = _originalBitmap.PixelHeight;
            int newW = Math.Max(10, (int)Math.Round(oldW * factor));
            int newH = Math.Max(10, (int)Math.Round(oldH * factor));

            // Capture current bitmap for background thread
            var sourceForResample = _originalBitmap;

            IsResampling = true;
            try
            {
                var resampled = await Task.Run(() =>
                    ImageProcessor.Resample(sourceForResample, newW, newH));

                _originalBitmap = resampled;
                _cleanBitmap = resampled;

                // Move crop proportionally (option C)
                double cropX = Math.Round(State.CropRect.X * factor);
                double cropY = Math.Round(State.CropRect.Y * factor);
                double cropW = State.CropRect.Width;
                double cropH = State.CropRect.Height;

                cropX = Math.Clamp(cropX, 0, newW - cropW);
                cropY = Math.Clamp(cropY, 0, newH - cropH);
                cropW = Math.Min(cropW, newW - cropX);
                cropH = Math.Min(cropH, newH - cropY);

                State.CropRect = new Rect(cropX, cropY,
                    Math.Max(10, cropW), Math.Max(10, cropH));
                State.IsCropModified = true;

                UpdateImageSizeText();
                RefreshPreview();
                ImageResampled?.Invoke();
            }
            finally
            {
                IsResampling = false;
            }
        }

        private void UpdateImageSizeText()
        {
            if (_originalBitmap == null) return;
            ImageSizeText = $"{_originalBitmap.PixelWidth} × {_originalBitmap.PixelHeight} px";
        }

        private BitmapSource? GetProcessedBitmap()
        {
            if (_originalBitmap == null) return null;
            return State.HasAnyAdjustment
                ? ImageProcessor.ApplyAdjustments(_originalBitmap, State)
                : _originalBitmap;
        }

        private void RefreshPreview()
        {
            if (_originalBitmap == null) return;
            PreviewBitmap = GetProcessedBitmap();
            UpdateImageSizeText();
        }

        public void RotateBy(double degrees)
        {
            if (_originalBitmap == null) return;

            double cx = (State.CropRect.X + State.CropRect.Width / 2.0) / _originalBitmap.PixelWidth;
            double cy = (State.CropRect.Y + State.CropRect.Height / 2.0) / _originalBitmap.PixelHeight;

            _originalBitmap = ImageProcessor.Rotate(_originalBitmap, degrees);
            _cleanBitmap = _originalBitmap;

            double newImgW = _originalBitmap.PixelWidth;
            double newImgH = _originalBitmap.PixelHeight;

            double newCx, newCy;
            if (Math.Abs(degrees - 90) < 0.1) { newCx = cy; newCy = 1.0 - cx; }
            else if (Math.Abs(degrees + 90) < 0.1) { newCx = 1.0 - cy; newCy = cx; }
            else { newCx = cx; newCy = cy; }

            double cropW = Math.Min(State.CropRect.Width, newImgW);
            double cropH = Math.Min(State.CropRect.Height, newImgH);
            double cropX = Math.Clamp(newCx * newImgW - cropW / 2, 0, newImgW - cropW);
            double cropY = Math.Clamp(newCy * newImgH - cropH / 2, 0, newImgH - cropH);

            State.CropRect = new Rect(cropX, cropY, cropW, cropH);
            State.IsCropModified = true;
            State.RotationAngle = 0;

            RefreshPreview();
        }

        public void FlipImage(bool horizontal)
        {
            if (_originalBitmap == null) return;

            double cx = (State.CropRect.X + State.CropRect.Width / 2.0) / _originalBitmap.PixelWidth;
            double cy = (State.CropRect.Y + State.CropRect.Height / 2.0) / _originalBitmap.PixelHeight;

            _originalBitmap = ImageProcessor.Flip(_originalBitmap, horizontal);
            _cleanBitmap = _originalBitmap;

            double newImgW = _originalBitmap.PixelWidth;
            double newImgH = _originalBitmap.PixelHeight;

            double newCx = horizontal ? 1.0 - cx : cx;
            double newCy = horizontal ? cy : 1.0 - cy;

            double cropW = State.CropRect.Width;
            double cropH = State.CropRect.Height;
            double cropX = Math.Clamp(newCx * newImgW - cropW / 2, 0, newImgW - cropW);
            double cropY = Math.Clamp(newCy * newImgH - cropH / 2, 0, newImgH - cropH);

            State.CropRect = new Rect(cropX, cropY, cropW, cropH);
            State.IsCropModified = true;

            RefreshPreview();
        }

        private void ResetAll()
        {
            if (_originalBitmap == null || _file == null) return;
            _originalBitmap = ImageLoader.Load(_file);
            _cleanBitmap = _originalBitmap;
            OriginalWidth = _originalBitmap.PixelWidth;
            OriginalHeight = _originalBitmap.PixelHeight;
            State.Reset();
            State.CropRect = new Rect(0, 0,
                _originalBitmap.PixelWidth, _originalBitmap.PixelHeight);
            ZoomLevel = 1.0;
            IsComparing = false;
            RefreshPreview();
            ImageResampled?.Invoke();
        }

        private void Save()
        {
            if (_file == null) { SaveAs(); return; }
            ExportTo(_file.FullPath);
        }

        private void SaveAs()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Save As",
                Filter = "JPEG|*.jpg;*.jpeg|PNG|*.png|BMP|*.bmp|TIFF|*.tif;*.tiff",
                FileName = Path.GetFileNameWithoutExtension(_file?.FileName ?? "image")
            };
            if (dlg.ShowDialog() == true)
                ExportTo(dlg.FileName);
        }

        private async void ExportTo(string path)
        {
            if (_originalBitmap == null || string.IsNullOrEmpty(path)) return;

            IsSaving = true;
            try
            {
                // Capture everything we need before going to background thread
                var bitmapToProcess = _originalBitmap;
                var stateCopy = new EditState
                {
                    Brightness = State.Brightness,
                    Exposure = State.Exposure,
                    Contrast = State.Contrast,
                    Highlights = State.Highlights,
                    Shadows = State.Shadows,
                    Vignette = State.Vignette,
                    Saturation = State.Saturation,
                    Warmth = State.Warmth,
                    Tint = State.Tint,
                    Sharpness = State.Sharpness,
                    ActiveFilter = State.ActiveFilter,
                    FilterIntensity = State.FilterIntensity,
                    RotationAngle = State.RotationAngle,
                    CropRect = State.CropRect
                };

                await Task.Run(() =>
                {
                    var output = stateCopy.HasAnyAdjustment
                        ? ImageProcessor.ApplyAdjustments(bitmapToProcess, stateCopy)
                        : bitmapToProcess;

                    if (stateCopy.RotationAngle != 0)
                        output = ImageProcessor.Rotate(output, stateCopy.RotationAngle);

                    var cropPx = new Int32Rect(
                        (int)Math.Max(0, stateCopy.CropRect.X),
                        (int)Math.Max(0, stateCopy.CropRect.Y),
                        (int)Math.Min(output.PixelWidth - (int)stateCopy.CropRect.X,
                                      stateCopy.CropRect.Width),
                        (int)Math.Min(output.PixelHeight - (int)stateCopy.CropRect.Y,
                                      stateCopy.CropRect.Height));

                    output = ImageProcessor.Crop(output, cropPx);

                    // Encoding must happen on a thread but BitmapEncoder
                    // needs the bitmap to be frozen (which it already is)
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    BitmapEncoder encoder = ext switch
                    {
                        ".png" => new PngBitmapEncoder(),
                        ".bmp" => new BmpBitmapEncoder(),
                        ".tif" or ".tiff" => new TiffBitmapEncoder(),
                        _ => new JpegBitmapEncoder { QualityLevel = 95 }
                    };

                    encoder.Frames.Add(BitmapFrame.Create(output));
                    using var fs = new FileStream(path,
                        FileMode.Create, FileAccess.Write, FileShare.None);
                    encoder.Save(fs);
                });

                Done();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not save:\n{ex.Message}",
                    "Photura", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSaving = false;
            }
        }

        private void Done() => DoneRequested?.Invoke();

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}