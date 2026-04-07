using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Photura.Models;

namespace Photura.Controls
{
    public partial class InfoSidebar : UserControl
    {
        public event Action<string, string>? RenameRequested;

        private ImageMetadata? _meta;

        public InfoSidebar()
        {
            InitializeComponent();
        }

        public void Load(ImageMetadata meta)
        {
            _meta = meta;

            // File
            FileNameBox.Text = Path.GetFileNameWithoutExtension(meta.FileName);
            PathLabel.Text = Path.GetDirectoryName(meta.FullPath) ?? string.Empty;

            // Image
            PixelSizeLabel.Text = meta.PixelSize;
            FileSizeLabel.Text = meta.FileSizeText;
            DpiLabel.Text = meta.Dpi;
            ColorDepthLabel.Text = meta.ColorDepth;

            // Date and Time
            bool hasDate = !string.IsNullOrEmpty(meta.DateTaken);
            SetVisibility(hasDate, DateTimeHeader, DateTimePanel, DateTimeSep);
            DateLabel.Text = meta.DateTaken;
            TimeLabel.Text = meta.TimeTaken;

            // Location
            bool hasLocation = !string.IsNullOrEmpty(meta.Coordinates);
            SetVisibility(hasLocation, LocationHeader, LocationPanel, LocationSep);
            PlaceLabel.Text = string.IsNullOrEmpty(meta.PlaceName)
                ? "Resolving..." : meta.PlaceName;
            CoordinatesLabel.Text = meta.Coordinates;

            // Camera
            bool hasCam = !string.IsNullOrEmpty(meta.Make) ||
                          !string.IsNullOrEmpty(meta.Model);
            SetVisibility(hasCam, CameraHeader, CameraPanel, CameraSep);
            MakeLabel.Text = meta.Make;
            ModelLabel.Text = meta.Model;
            LensLabel.Text = meta.Lens;
            FocalLengthLabel.Text = meta.FocalLength;
            FStopLabel.Text = meta.FStop;
            ShutterLabel.Text = meta.ShutterSpeed;
            IsoLabel.Text = meta.Iso;
            ExposureLabel.Text = meta.Exposure;
            FlashLabel.Text = meta.Flash;
            WhiteBalanceLabel.Text = meta.WhiteBalance;

            // Description
            bool hasDesc = !string.IsNullOrEmpty(meta.Description) ||
                           !string.IsNullOrEmpty(meta.Copyright);
            SetVisibility(hasDesc, DescHeader, DescPanel);
            DescriptionLabel.Text = meta.Description;
            CopyrightLabel.Text = meta.Copyright;
        }

        public void UpdatePlaceName(string placeName)
        {
            PlaceLabel.Text = string.IsNullOrEmpty(placeName) ? "—" : placeName;
        }

        private static void SetVisibility(bool visible, params UIElement[] elements)
        {
            var v = visible ? Visibility.Visible : Visibility.Collapsed;
            foreach (var el in elements) el.Visibility = v;
        }

        // ── Rename ────────────────────────────────────────────────
        private void FileNameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                DoRename();
            if (e.Key == Key.Escape && _meta != null)
                FileNameBox.Text = Path.GetFileNameWithoutExtension(_meta.FileName);
        }

        private void RenameConfirm_Click(object sender, RoutedEventArgs e)
            => DoRename();

        private void DoRename()
        {
            if (_meta == null) return;
            string newName = FileNameBox.Text.Trim();
            if (string.IsNullOrEmpty(newName)) return;

            string ext = Path.GetExtension(_meta.FileName);
            string newFull = newName + ext;

            if (newFull == _meta.FileName) return;

            RenameRequested?.Invoke(_meta.FullPath, newFull);
        }

        // ── Path actions ─────────────────────────────────────────
        private void PathLabel_Click(object sender, MouseButtonEventArgs e)
            => OpenInExplorer();

        private void OpenInExplorer_Click(object sender, RoutedEventArgs e)
            => OpenInExplorer();

        private void OpenInExplorer()
        {
            if (_meta == null) return;
            Process.Start("explorer.exe", $"/select,\"{_meta.FullPath}\"");
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (_meta == null) return;
            Clipboard.SetText(_meta.FullPath);
        }
    }
}