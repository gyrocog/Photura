using System;
using System.IO;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Photura.Models;
using Photura.Services;
using Photura.ViewModels;

namespace Photura.Views
{
    public partial class ViewerView : Window
    {
        private readonly ViewerViewModel _vm;
        private bool _updatingZoomSlider = false;
        private bool _isLoaded = false;
        private double _zoom = 1.0;

        private double _imgX = 0;
        private double _imgY = 0;

        private bool _isPanning = false;
        private Point _panStart;
        private double _panStartX;
        private double _panStartY;

        private bool _sidebarOpen = false;
        private const int SidebarWidth = 300;
        private const string RegKey = @"Software\Photura";

        public ViewerView()
        {
            InitializeComponent();
            _vm = new ViewerViewModel();
            DataContext = _vm;

            _vm.PropertyChanged += Vm_PropertyChanged;
            _vm.EditRequested += OnEditRequested;

            Loaded += (s, e) =>
            {
                _isLoaded = true;
                RestoreWindowState();

                // Sync theme toggle
                ThemeToggle.Checked -= ThemeToggle_Checked;
                ThemeToggle.Unchecked -= ThemeToggle_Unchecked;
                ThemeToggle.IsChecked = App.IsDarkTheme;
                ThemeToggle.Content = App.IsDarkTheme ? "🌙" : "☀️";
                ThemeToggle.Checked += ThemeToggle_Checked;
                ThemeToggle.Unchecked += ThemeToggle_Unchecked;
            };
        }

        public void OpenImageFromPath(string path)
        {
            _vm.OpenImage(path);
            UpdateUI();
        }

        // ── Canvas dimensions ─────────────────────────────────────
        private double CanvasW => ImageBorder.ActualWidth;
        private double CanvasH => ImageBorder.ActualHeight;

        private void ImageBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ImageCanvas.Width = e.NewSize.Width;
            ImageCanvas.Height = e.NewSize.Height;
            if (_isLoaded && _vm.CurrentBitmap != null)
                FitToWindow();
        }

        // ── Window state ─────────────────────────────────────────
        private void RestoreWindowState()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegKey);
                if (key == null) return;

                double w = Convert.ToDouble(key.GetValue("Width", 1200));
                double h = Convert.ToDouble(key.GetValue("Height", 850));
                double left = Convert.ToDouble(key.GetValue("Left", 100));
                double top = Convert.ToDouble(key.GetValue("Top", 100));
                bool max = Convert.ToBoolean(key.GetValue("Maximized", false));

                if (left >= SystemParameters.VirtualScreenLeft &&
                    top >= SystemParameters.VirtualScreenTop &&
                    left < SystemParameters.VirtualScreenLeft +
                            SystemParameters.VirtualScreenWidth &&
                    top < SystemParameters.VirtualScreenTop +
                            SystemParameters.VirtualScreenHeight)
                {
                    Left = left;
                    Top = top;
                    Width = Math.Max(700, w);
                    Height = Math.Max(500, h);
                }

                if (max) WindowState = WindowState.Maximized;
            }
            catch { }
        }

        private void SaveWindowState()
        {
            if (!_isLoaded || WindowState == WindowState.Minimized) return;
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegKey);
                if (WindowState == WindowState.Normal)
                {
                    key.SetValue("Width", Width);
                    key.SetValue("Height", Height);
                    key.SetValue("Left", Left);
                    key.SetValue("Top", Top);
                }
                key.SetValue("Maximized", WindowState == WindowState.Maximized);
            }
            catch { }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
            => SaveWindowState();

        private void Window_StateChanged(object sender, EventArgs e)
            => SaveWindowState();

        // ── Info sidebar ─────────────────────────────────────────
        private void InfoToggle_Checked(object sender, RoutedEventArgs e)
            => OpenSidebar();

        private void InfoToggle_Unchecked(object sender, RoutedEventArgs e)
            => CloseSidebar();

        private void OpenSidebar()
        {
            _sidebarOpen = true;
            SidebarColumn.Width = new GridLength(SidebarWidth);
            if (_vm.CurrentFile != null)
                _ = LoadMetadataAsync(_vm.CurrentFile.FullPath);
        }

        private void CloseSidebar()
        {
            _sidebarOpen = false;
            SidebarColumn.Width = new GridLength(0);
        }

        private async System.Threading.Tasks.Task LoadMetadataAsync(string path)
        {
            var meta = await System.Threading.Tasks.Task.Run(
                () => MetadataService.ReadAsync(path));
            InfoSidebarControl.Load(meta);
        }

        private void InfoSidebar_RenameRequested(string oldPath, string newName)
        {
            try
            {
                string dir = Path.GetDirectoryName(oldPath) ?? string.Empty;
                string newPath = Path.Combine(dir, newName);

                if (File.Exists(newPath))
                {
                    MessageBox.Show($"A file named '{newName}' already exists.",
                        "Photura", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                File.Move(oldPath, newPath);

                // Reload folder navigator with new path
                _vm.OpenImage(newPath);
                UpdateUI();

                // Reload sidebar with new metadata
                if (_sidebarOpen)
                    _ = LoadMetadataAsync(newPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not rename file:\n{ex.Message}",
                    "Photura", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── File open ────────────────────────────────────────────
        private void Open_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Open Image",
                Filter = ImageLoader.OpenFileFilter
            };
            if (dlg.ShowDialog() == true)
            {
                _vm.OpenImage(dlg.FileName);
                UpdateUI();
            }
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] files &&
                files.Length > 0)
            {
                _vm.OpenImage(files[0]);
                UpdateUI();
            }
        }

        // ── Keyboard ─────────────────────────────────────────────
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
                Open_Click(this, new RoutedEventArgs());
            if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
                _vm.CopyCommand.Execute(null);
            if (e.Key == Key.Delete)
                _vm.DeleteCommand.Execute(null);
            if (e.Key == Key.I && Keyboard.Modifiers == ModifierKeys.None)
            {
                InfoToggle.IsChecked = !InfoToggle.IsChecked;
            }

            if (_vm.HasImage)
            {
                if (e.Key == Key.Right || e.Key == Key.Down)
                { _vm.GoNext(); UpdateUI(); }
                if (e.Key == Key.Left || e.Key == Key.Up)
                { _vm.GoPrev(); UpdateUI(); }
            }
        }

        // ── Mousewheel ───────────────────────────────────────────
        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_vm.HasImage) return;
            e.Handled = true;

            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                Point cursor = e.GetPosition(ImageCanvas);
                double factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
                ZoomAround(cursor, _zoom * factor);
            }
            else
            {
                if (e.Delta < 0) { _vm.GoNext(); UpdateUI(); }
                else { _vm.GoPrev(); UpdateUI(); }
            }
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
            ApplyTransform();
            e.Handled = true;
        }

        // ── Zoom controls ─────────────────────────────────────────
        private void ZoomIn_Click(object sender, RoutedEventArgs e)
            => ZoomAroundCenter(_zoom * 1.15);

        private void ZoomOut_Click(object sender, RoutedEventArgs e)
            => ZoomAroundCenter(_zoom / 1.15);

        private void ZoomFit_Click(object sender, RoutedEventArgs e)
            => FitToWindow();

        private void ZoomSlider_ValueChanged(object sender,
            RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updatingZoomSlider || !_isLoaded) return;
            ZoomAroundCenter(e.NewValue / 100.0);
        }

        private void ZoomAroundCenter(double newZoom)
        {
            if (_vm.CurrentBitmap == null) return;
            ZoomAround(new Point(CanvasW / 2, CanvasH / 2), newZoom);
        }

        private void ZoomAround(Point anchor, double newZoom)
        {
            if (_vm.CurrentBitmap == null) return;
            newZoom = Math.Clamp(newZoom, 0.02, 8.0);

            double ipX = (anchor.X - _imgX) / _zoom;
            double ipY = (anchor.Y - _imgY) / _zoom;

            _zoom = newZoom;
            _imgX = anchor.X - ipX * _zoom;
            _imgY = anchor.Y - ipY * _zoom;

            ApplyTransform();
        }

        private void FitToWindow()
        {
            if (_vm.CurrentBitmap == null) return;
            double w = CanvasW;
            double h = CanvasH;
            if (w <= 0 || h <= 0) return;

            double fit = Math.Min(
                w / _vm.CurrentBitmap.PixelWidth,
                h / _vm.CurrentBitmap.PixelHeight);
            _zoom = Math.Min(fit, 1.0);
            _imgX = (w - _vm.CurrentBitmap.PixelWidth * _zoom) / 2.0;
            _imgY = (h - _vm.CurrentBitmap.PixelHeight * _zoom) / 2.0;

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (_vm.CurrentBitmap == null) return;

            Canvas.SetLeft(MainImage, _imgX);
            Canvas.SetTop(MainImage, _imgY);
            ImageScale.ScaleX = _zoom;
            ImageScale.ScaleY = _zoom;

            ZoomLabel.Text = $"{(int)(_zoom * 100)}%";
            _updatingZoomSlider = true;
            ZoomSlider.Value = _zoom * 100;
            _updatingZoomSlider = false;
            _vm.ZoomLevel = _zoom;
        }

        // ── UI update ─────────────────────────────────────────────
        private void UpdateUI()
        {
            if (_vm.CurrentBitmap == null)
            {
                DropHint.Visibility = Visibility.Visible;
                ImageCanvas.Visibility = Visibility.Collapsed;
                PrevButton.Visibility = Visibility.Collapsed;
                NextButton.Visibility = Visibility.Collapsed;
                FileNameLabel.Text = "Photura";
                PixelSizeLabel.Text = string.Empty;
                FileSizeLabel.Text = string.Empty;
                PositionLabel.Text = string.Empty;
                return;
            }

            MainImage.Source = _vm.CurrentBitmap;
            DropHint.Visibility = Visibility.Collapsed;
            ImageCanvas.Visibility = Visibility.Visible;
            PrevButton.Visibility = Visibility.Visible;
            NextButton.Visibility = Visibility.Visible;

            FileNameLabel.Text = _vm.FileName;
            PositionLabel.Text = _vm.PositionText;

            // Pixel size in bottom bar — read directly from bitmap if available
            if (_vm.CurrentBitmap != null)
                PixelSizeLabel.Text =
                    $"{_vm.CurrentBitmap.PixelWidth} × {_vm.CurrentBitmap.PixelHeight} px";

            // File size in bottom bar
            if (_vm.CurrentFile != null)
            {
                try
                {
                    var fi = new System.IO.FileInfo(_vm.CurrentFile.FullPath);
                    FileSizeLabel.Text = FormatFileSize(fi.Length);
                }
                catch { FileSizeLabel.Text = string.Empty; }
            }

            // Reload sidebar if open
            if (_sidebarOpen && _vm.CurrentFile != null)
                _ = LoadMetadataAsync(_vm.CurrentFile.FullPath);

            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Render, () =>
                {
                    ImageCanvas.Width = CanvasW;
                    ImageCanvas.Height = CanvasH;
                    FitToWindow();
                });
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
            return $"{bytes / (1024.0 * 1024):0.##} MB";
        }

        private void Vm_PropertyChanged(object? sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewerViewModel.CurrentBitmap))
                UpdateUI();
        }

        // ── Edit mode ─────────────────────────────────────────────
        private void OnEditRequested(ImageFile file)
        {
            var editView = new EditView(file);

            editView.Width = Width;
            editView.Height = Height;
            editView.Left = Left;
            editView.Top = Top;
            editView.WindowState = WindowState;

            editView.Owner = this;
            editView.Closed += (s, e) =>
            {
                Width = editView.Width;
                Height = editView.Height;
                Left = editView.Left;
                Top = editView.Top;
                WindowState = editView.WindowState;
                SaveWindowState();

                if (_vm.CurrentFile != null)
                {
                    _vm.OpenImage(_vm.CurrentFile.FullPath);
                    UpdateUI();
                }
                Show();
            };
            editView.Show();
            Hide();
        }

        // ── Theme ─────────────────────────────────────────────────
        private void ThemeToggle_Checked(object sender, RoutedEventArgs e)
        {
            App.SwitchTheme(true);
            ThemeToggle.Content = "🌙";
        }

        private void ThemeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            App.SwitchTheme(false);
            ThemeToggle.Content = "☀️";
        }
    }
}