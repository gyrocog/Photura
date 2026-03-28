using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Photura.Models;

namespace Photura.Services
{
    public static class ImageProcessor
    {
        public static BitmapSource ApplyAdjustments(BitmapSource source, EditState state)
        {
            if (!state.HasAnyAdjustment) return source;

            var formatted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            int w = formatted.PixelWidth;
            int h = formatted.PixelHeight;
            int stride = w * 4;
            byte[] pixels = new byte[h * stride];
            formatted.CopyPixels(pixels, stride, 0);

            // Keep original pixels for filter intensity blending
            byte[] original = new byte[pixels.Length];
            Array.Copy(pixels, original, pixels.Length);

            byte[] lutR = new byte[256];
            byte[] lutG = new byte[256];
            byte[] lutB = new byte[256];

            double brightness = state.Brightness / 100.0;
            double exposure = state.Exposure / 100.0;
            double contrast = state.Contrast / 100.0;
            double warmth = state.Warmth / 100.0;
            double tint = state.Tint / 100.0;

            double contrastFactor = contrast >= 0
                ? 1.0 + contrast * 3.0
                : 1.0 + contrast;

            for (int i = 0; i < 256; i++)
            {
                double v = i / 255.0;
                v *= Math.Pow(2, exposure * 2);
                v += brightness * 0.5;
                v = (v - 0.5) * contrastFactor + 0.5;
                v = Math.Clamp(v, 0, 1);

                double r = v, g = v, b = v;

                if (warmth > 0)
                {
                    r = Math.Clamp(v + warmth * 0.15, 0, 1);
                    g = Math.Clamp(v + warmth * 0.05, 0, 1);
                    b = Math.Clamp(v - warmth * 0.10, 0, 1);
                }
                else if (warmth < 0)
                {
                    double t = -warmth;
                    r = Math.Clamp(v - t * 0.10, 0, 1);
                    g = Math.Clamp(v - t * 0.02, 0, 1);
                    b = Math.Clamp(v + t * 0.15, 0, 1);
                }

                g = Math.Clamp(g - tint * 0.10, 0, 1);

                lutR[i] = (byte)(r * 255);
                lutG[i] = (byte)(g * 255);
                lutB[i] = (byte)(b * 255);
            }

            double saturation = state.Saturation / 100.0;
            double shadows = state.Shadows / 100.0;
            double highlights = state.Highlights / 100.0;
            double intensity = state.FilterIntensity / 100.0;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                double b0 = pixels[i] / 255.0;
                double g0 = pixels[i + 1] / 255.0;
                double r0 = pixels[i + 2] / 255.0;

                r0 = lutR[(int)(r0 * 255)] / 255.0;
                g0 = lutG[(int)(g0 * 255)] / 255.0;
                b0 = lutB[(int)(b0 * 255)] / 255.0;

                double lum = 0.2126 * r0 + 0.7152 * g0 + 0.0722 * b0;

                if (saturation >= 0)
                {
                    r0 = lum + (r0 - lum) * (1 + saturation);
                    g0 = lum + (g0 - lum) * (1 + saturation);
                    b0 = lum + (b0 - lum) * (1 + saturation);
                }
                else
                {
                    r0 = r0 + (lum - r0) * (-saturation);
                    g0 = g0 + (lum - g0) * (-saturation);
                    b0 = b0 + (lum - b0) * (-saturation);
                }

                if (shadows != 0)
                {
                    double mask = 1.0 - lum;
                    r0 += mask * shadows * 0.5;
                    g0 += mask * shadows * 0.5;
                    b0 += mask * shadows * 0.5;
                }

                if (highlights != 0)
                {
                    r0 += lum * highlights * 0.5;
                    g0 += lum * highlights * 0.5;
                    b0 += lum * highlights * 0.5;
                }

                // Apply filter
                double fr = r0, fg = g0, fb = b0;
                (fr, fg, fb) = ApplyFilter(r0, g0, b0, lum, state.ActiveFilter);

                // Blend filter with pre-filter pixel based on intensity
                if (intensity < 1.0)
                {
                    fr = r0 + (fr - r0) * intensity;
                    fg = g0 + (fg - g0) * intensity;
                    fb = b0 + (fb - b0) * intensity;
                }

                pixels[i] = (byte)(Math.Clamp(fb, 0, 1) * 255);
                pixels[i + 1] = (byte)(Math.Clamp(fg, 0, 1) * 255);
                pixels[i + 2] = (byte)(Math.Clamp(fr, 0, 1) * 255);
            }

            if (state.Vignette > 0)
                ApplyVignette(pixels, w, h, stride, state.Vignette / 100.0);

            var result = BitmapSource.Create(w, h,
                source.DpiX, source.DpiY,
                PixelFormats.Bgra32, null, pixels, stride);

            if (state.Sharpness > 0)
                return Sharpen(result, pixels, w, h, stride, state.Sharpness / 100.0);

            result.Freeze();
            return result;
        }

        private static (double r, double g, double b) ApplyFilter(
            double r, double g, double b, double lum, string filter)
        {
            return filter switch
            {
                // ── Existing ────────────────────────────────────
                "Grayscale" => (lum, lum, lum),

                "Sepia" => (
                    Math.Clamp(r * 0.393 + g * 0.769 + b * 0.189, 0, 1),
                    Math.Clamp(r * 0.349 + g * 0.686 + b * 0.168, 0, 1),
                    Math.Clamp(r * 0.272 + g * 0.534 + b * 0.131, 0, 1)),

                "Vivid" => (
                    Math.Clamp((r - 0.5) * 1.4 + 0.5, 0, 1),
                    Math.Clamp((g - 0.5) * 1.4 + 0.5, 0, 1),
                    Math.Clamp((b - 0.5) * 1.4 + 0.5, 0, 1)),

                "Warm" => (
                    Math.Clamp(r + 0.10, 0, 1),
                    Math.Clamp(g + 0.04, 0, 1),
                    Math.Clamp(b - 0.08, 0, 1)),

                "Cool" => (
                    Math.Clamp(r - 0.08, 0, 1),
                    Math.Clamp(g + 0.02, 0, 1),
                    Math.Clamp(b + 0.10, 0, 1)),

                "Fade" => (r * 0.85 + 0.10, g * 0.85 + 0.10, b * 0.85 + 0.10),
                "Matte" => (r * 0.80 + 0.08, g * 0.78 + 0.07, b * 0.75 + 0.10),

                "Noir" => (
                    Math.Clamp((lum - 0.5) * 1.6 + 0.5, 0, 1),
                    Math.Clamp((lum - 0.5) * 1.6 + 0.5, 0, 1),
                    Math.Clamp((lum - 0.5) * 1.6 + 0.5, 0, 1)),

                "Chrome" => (
                    Math.Clamp(lum + (r - lum) * 0.5 + 0.05, 0, 1),
                    Math.Clamp(lum + (g - lum) * 0.5 + 0.05, 0, 1),
                    Math.Clamp(lum + (b - lum) * 0.5, 0, 1)),

                // ── Vintage / Film ───────────────────────────────
                // Kodachrome: warm reds, rich greens, deep blues
                "Kodachrome" => (
                    Math.Clamp(r * 1.20 + g * 0.05 - b * 0.05, 0, 1),
                    Math.Clamp(r * 0.00 + g * 1.10 + b * 0.00 - 0.02, 0, 1),
                    Math.Clamp(r * 0.00 + g * 0.00 + b * 0.90 - 0.05, 0, 1)),

                // Fuji: cooler greens, clean, slightly desaturated
                "Fuji" => (
                    Math.Clamp(r * 0.95 - 0.01, 0, 1),
                    Math.Clamp(g * 1.05 + 0.02, 0, 1),
                    Math.Clamp(b * 1.00 + 0.03, 0, 1)),

                // Polaroid: faded whites, blue shadows, overexposed
                "Polaroid" => (
                    Math.Clamp(r * 0.90 + 0.08, 0, 1),
                    Math.Clamp(g * 0.88 + 0.07, 0, 1),
                    Math.Clamp(b * 0.85 + 0.12, 0, 1)),

                // Lomo: high contrast, color shift, crushed blacks
                "Lomo" => (
                    Math.Clamp((r - 0.5) * 1.5 + 0.5 + 0.05, 0, 1),
                    Math.Clamp((g - 0.5) * 1.3 + 0.5 - 0.03, 0, 1),
                    Math.Clamp((b - 0.5) * 1.4 + 0.5 + 0.08, 0, 1)),

                // Cross Process: pushed colors, high contrast, shifted hues
                "Cross Process" => (
                    Math.Clamp(r * 1.3 - 0.05, 0, 1),
                    Math.Clamp(g * 0.8 + 0.05, 0, 1),
                    Math.Clamp(b * 1.2 - 0.10, 0, 1)),

                // ── Mood ─────────────────────────────────────────
                // Golden Hour: warm orange/yellow, soft highlights
                "Golden Hour" => (
                    Math.Clamp(r + 0.15, 0, 1),
                    Math.Clamp(g + 0.07, 0, 1),
                    Math.Clamp(b - 0.12, 0, 1)),

                // Moonlight: cool blue, lifted shadows
                "Moonlight" => (
                    Math.Clamp(r * 0.85 + 0.02, 0, 1),
                    Math.Clamp(g * 0.92 + 0.03, 0, 1),
                    Math.Clamp(b * 1.10 + 0.08, 0, 1)),

                // Haze: faded, low contrast, lifted blacks
                "Haze" => (
                    r * 0.75 + 0.15,
                    g * 0.75 + 0.15,
                    b * 0.75 + 0.18),

                // Cinematic: teal shadows, orange highlights (Hollywood look)
                "Cinematic" => (
                    Math.Clamp(r + lum * 0.12 - 0.02, 0, 1),
                    Math.Clamp(g + (lum - 0.5) * 0.05, 0, 1),
                    Math.Clamp(b - lum * 0.10 + 0.08, 0, 1)),

                // Bleach Bypass: desaturated, high contrast, silver retention
                "Bleach Bypass" => BlendGray(
                    Math.Clamp((r - 0.5) * 1.4 + 0.5, 0, 1),
                    Math.Clamp((g - 0.5) * 1.4 + 0.5, 0, 1),
                    Math.Clamp((b - 0.5) * 1.4 + 0.5, 0, 1),
                    lum, 0.5),

                // ── Creative ─────────────────────────────────────
                // Duotone: map shadows to deep blue, highlights to warm gold
                "Duotone" => (
                    Math.Clamp(lum * 1.0 + 0.05, 0, 1),
                    Math.Clamp(lum * 0.75 + 0.05, 0, 1),
                    Math.Clamp(lum * 0.3 + 0.10, 0, 1)),

                // Infrared: foliage white, skies dark, dreamlike
                "Infrared" => (
                    Math.Clamp(lum + (g - b) * 0.5, 0, 1),
                    Math.Clamp(lum + (g - r) * 0.3, 0, 1),
                    Math.Clamp(lum - g * 0.4, 0, 1)),

                // Halation: bloom around highlights, film light leak
                "Halation" => (
                    Math.Clamp(r + Math.Pow(lum, 3) * 0.4, 0, 1),
                    Math.Clamp(g + Math.Pow(lum, 3) * 0.1, 0, 1),
                    Math.Clamp(b - Math.Pow(lum, 3) * 0.1, 0, 1)),

                // Faded Kodak: warm fade, slightly green shadows
                "Faded Kodak" => (
                    Math.Clamp(r * 0.88 + 0.09, 0, 1),
                    Math.Clamp(g * 0.90 + 0.06, 0, 1),
                    Math.Clamp(b * 0.82 + 0.10, 0, 1)),

                _ => (r, g, b)
            };
        }

        /// <summary>Blend a color result toward grayscale by amount 0-1.</summary>
        private static (double r, double g, double b) BlendGray(
            double r, double g, double b, double lum, double amount)
        {
            return (
                r + (lum - r) * amount,
                g + (lum - g) * amount,
                b + (lum - b) * amount);
        }

        private static void ApplyVignette(byte[] pixels, int w, int h,
            int stride, double strength)
        {
            double cx = w / 2.0;
            double cy = h / 2.0;
            double maxDist = Math.Sqrt(cx * cx + cy * cy);

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double dx = x - cx;
                    double dy = y - cy;
                    double dist = Math.Sqrt(dx * dx + dy * dy) / maxDist;
                    double fade = Math.Clamp(1.0 - strength * dist * dist, 0, 1);
                    int idx = y * stride + x * 4;
                    pixels[idx] = (byte)(pixels[idx] * fade);
                    pixels[idx + 1] = (byte)(pixels[idx + 1] * fade);
                    pixels[idx + 2] = (byte)(pixels[idx + 2] * fade);
                }
        }

        private static BitmapSource Sharpen(BitmapSource src, byte[] pixels,
            int w, int h, int stride, double amount)
        {
            byte[] blurred = new byte[pixels.Length];
            Array.Copy(pixels, blurred, pixels.Length);

            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    int idx = y * stride + x * 4;
                    for (int c = 0; c < 3; c++)
                    {
                        int sum = 0;
                        for (int dy = -1; dy <= 1; dy++)
                            for (int dx = -1; dx <= 1; dx++)
                                sum += pixels[(y + dy) * stride + (x + dx) * 4 + c];
                        blurred[idx + c] = (byte)(sum / 9);
                    }
                }

            byte[] sharpened = new byte[pixels.Length];
            Array.Copy(pixels, sharpened, pixels.Length);

            for (int i = 0; i < pixels.Length - 3; i += 4)
                for (int c = 0; c < 3; c++)
                {
                    double v = pixels[i + c] + amount * 2.0 * (pixels[i + c] - blurred[i + c]);
                    sharpened[i + c] = (byte)Math.Clamp(v, 0, 255);
                }

            var result = BitmapSource.Create(w, h,
                src.DpiX, src.DpiY,
                PixelFormats.Bgra32, null, sharpened, stride);
            result.Freeze();
            return result;
        }

        public static BitmapSource Resample(BitmapSource source, int newWidth, int newHeight)
        {
            var formatted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(formatted));

            byte[] pngBytes;
            using (var ms = new System.IO.MemoryStream())
            {
                encoder.Save(ms);
                pngBytes = ms.ToArray();
            }

            using var magick = new MagickImage(pngBytes);
            magick.FilterType = FilterType.Lanczos;
            magick.Resize((uint)newWidth, (uint)newHeight);
            magick.Format = MagickFormat.Png;

            byte[] outBytes = magick.ToByteArray();
            using var outMs = new System.IO.MemoryStream(outBytes);

            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = outMs;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        public static BitmapSource Crop(BitmapSource source, Int32Rect cropRect)
        {
            var cropped = new CroppedBitmap(source, cropRect);
            cropped.Freeze();
            return cropped;
        }

        public static BitmapSource Rotate(BitmapSource source, double angleDegrees)
        {
            if (angleDegrees == 0) return source;
            var transform = new RotateTransform(angleDegrees);
            var transformed = new TransformedBitmap(source, transform);
            var result = new WriteableBitmap(transformed);
            result.Freeze();
            return result;
        }

        public static BitmapSource Flip(BitmapSource source, bool horizontal)
        {
            double sx = horizontal ? -1 : 1;
            double sy = horizontal ? 1 : -1;
            var transform = new ScaleTransform(sx, sy);
            var transformed = new TransformedBitmap(source, transform);
            var result = new WriteableBitmap(transformed);
            result.Freeze();
            return result;
        }
    }
}