using System.Windows;

namespace Photura.Models
{
    public class EditState
    {
        // ── Crop ────────────────────────────────────────────────
        public Rect CropRect { get; set; }
        public bool IsCropModified { get; set; } = false;
        public double RotationAngle { get; set; } = 0;

        // ── Light ───────────────────────────────────────────────
        public double Brightness { get; set; } = 0;
        public double Exposure { get; set; } = 0;
        public double Contrast { get; set; } = 0;
        public double Highlights { get; set; } = 0;
        public double Shadows { get; set; } = 0;
        public double Vignette { get; set; } = 0;

        // ── Color ───────────────────────────────────────────────
        public double Saturation { get; set; } = 0;
        public double Warmth { get; set; } = 0;
        public double Tint { get; set; } = 0;
        public double Sharpness { get; set; } = 0;

        // ── Filter ──────────────────────────────────────────────
        public string ActiveFilter { get; set; } = "None";
        public double FilterIntensity { get; set; } = 100; // 0-100

        // ── Helpers ─────────────────────────────────────────────
        public bool HasAnyAdjustment =>
            Brightness != 0 || Exposure != 0 || Contrast != 0 ||
            Highlights != 0 || Shadows != 0 || Vignette != 0 ||
            Saturation != 0 || Warmth != 0 || Tint != 0 ||
            Sharpness != 0 || ActiveFilter != "None";

        public void Reset()
        {
            RotationAngle = 0;
            Brightness = Exposure = Contrast = Highlights = Shadows = Vignette = 0;
            Saturation = Warmth = Tint = Sharpness = 0;
            ActiveFilter = "None";
            FilterIntensity = 100;
            IsCropModified = false;
        }
    }
}