namespace Photura.Models
{
    public class ImageMetadata
    {
        // File info
        public string FileName { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string FileSizeText { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }

        // Image info
        public string PixelSize { get; set; } = string.Empty;
        public string Dpi { get; set; } = string.Empty;
        public string ColorDepth { get; set; } = string.Empty;

        // Date & Time
        public string DateTaken { get; set; } = string.Empty;
        public string TimeTaken { get; set; } = string.Empty;

        // Location
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string Coordinates { get; set; } = string.Empty;
        public string PlaceName { get; set; } = string.Empty;

        // Camera
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Lens { get; set; } = string.Empty;
        public string FocalLength { get; set; } = string.Empty;
        public string FStop { get; set; } = string.Empty;
        public string ShutterSpeed { get; set; } = string.Empty;
        public string Iso { get; set; } = string.Empty;
        public string Exposure { get; set; } = string.Empty;
        public string Flash { get; set; } = string.Empty;
        public string WhiteBalance { get; set; } = string.Empty;

        // Description
        public string Description { get; set; } = string.Empty;
        public string Copyright { get; set; } = string.Empty;
    }
}