using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Photura.Models;
using Photura.Services;

namespace Photura.ViewModels
{
    public class ViewerViewModel : INotifyPropertyChanged
    {
        private readonly FolderNavigator _navigator = new();
        private CancellationTokenSource? _loadCts;

        public event Action<ImageFile>? EditRequested;
        public event PropertyChangedEventHandler? PropertyChanged;

        private BitmapSource? _currentBitmap;
        public BitmapSource? CurrentBitmap
        {
            get => _currentBitmap;
            private set { _currentBitmap = value; OnPropertyChanged(); }
        }

        private string _fileName = string.Empty;
        public string FileName
        {
            get => _fileName;
            private set { _fileName = value; OnPropertyChanged(); }
        }

        private string _positionText = string.Empty;
        public string PositionText
        {
            get => _positionText;
            private set { _positionText = value; OnPropertyChanged(); }
        }

        private string _imageSizeText = string.Empty;
        public string ImageSizeText
        {
            get => _imageSizeText;
            private set { _imageSizeText = value; OnPropertyChanged(); }
        }

        private bool _isLoading = false;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        private double _zoomLevel = 1.0;
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                _zoomLevel = Math.Clamp(value, 0.05, 8.0);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ZoomPercent));
            }
        }

        public string ZoomPercent => $"{(int)(_zoomLevel * 100)}%";

        private bool _hasImage = false;
        public bool HasImage
        {
            get => _hasImage;
            private set { _hasImage = value; OnPropertyChanged(); }
        }

        public ICommand NextImageCommand { get; }
        public ICommand PrevImageCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand ZoomFitCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand DeleteCommand { get; }

        public ViewerViewModel()
        {
            NextImageCommand = new RelayCommand(GoNext, () => _navigator.HasNext && !IsLoading);
            PrevImageCommand = new RelayCommand(GoPrev, () => _navigator.HasPrev && !IsLoading);
            EditCommand = new RelayCommand(RequestEdit, () => HasImage && !IsLoading);
            ZoomInCommand = new RelayCommand(() => ZoomLevel += 0.15);
            ZoomOutCommand = new RelayCommand(() => ZoomLevel -= 0.15);
            ZoomFitCommand = new RelayCommand(() => ZoomLevel = 1.0);
            CopyCommand = new RelayCommand(CopyToClipboard, () => HasImage);
            DeleteCommand = new RelayCommand(DeleteCurrent, () => HasImage);
        }

        public void OpenImage(string path)
        {
            _navigator.LoadFolder(path);
            _ = LoadCurrentAsync();
        }

        public void GoNext()
        {
            _navigator.GoNext();
            _ = LoadCurrentAsync();
        }

        public void GoPrev()
        {
            _navigator.GoPrev();
            _ = LoadCurrentAsync();
        }

        public ImageFile? CurrentFile => _navigator.Current;

        private async Task LoadCurrentAsync()
        {
            var file = _navigator.Current;
            if (file == null)
            {
                CurrentBitmap = null;
                FileName = string.Empty;
                PositionText = string.Empty;
                ImageSizeText = string.Empty;
                HasImage = false;
                return;
            }

            // Cancel any in-progress load
            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            IsLoading = true;

            // Update text immediately so UI feels responsive
            FileName = file.FileName;
            PositionText = _navigator.GetPositionText();

            try
            {
                var bmp = await ImageLoader.LoadAsync(file.FullPath);

                if (token.IsCancellationRequested) return;

                CurrentBitmap = bmp;
                ImageSizeText = $"{bmp.PixelWidth} × {bmp.PixelHeight}";
                HasImage = true;
                ZoomLevel = 1.0;

                // Pre-fetch adjacent images in background
                string? prevPath = GetAdjacentPath(-1);
                string? nextPath = GetAdjacentPath(+1);
                ImageLoader.PrefetchAdjacent(prevPath, nextPath);
            }
            catch (OperationCanceledException)
            {
                // Cancelled — either by user scrolling fast or HEIC codec missing
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open image:\n{ex.Message}",
                    "Photura", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (!token.IsCancellationRequested)
                    IsLoading = false;
            }
        }

        private string? GetAdjacentPath(int offset)
        {
            return _navigator.GetPath(_navigator.CurrentIndex + offset);
        }

        private void CopyToClipboard()
        {
            if (CurrentBitmap == null) return;
            try { Clipboard.SetImage(CurrentBitmap); }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not copy image:\n{ex.Message}",
                    "Photura", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteCurrent()
        {
            var file = _navigator.Current;
            if (file == null) return;
            try
            {
                Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    file.FullPath,
                    Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                    Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                _navigator.RemoveCurrent();
                _ = LoadCurrentAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not delete file:\n{ex.Message}",
                    "Photura", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RequestEdit()
        {
            var file = _navigator.Current;
            if (file != null) EditRequested?.Invoke(file);
        }

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}