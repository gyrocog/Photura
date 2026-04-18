using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageMagick;
using Photura.Models;

namespace Photura.Services
{
    public static class ImageLoader
    {
        private static readonly string[] NativeFormats =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif",
            ".tif", ".tiff", ".ico"
        };

        // Formats where we need to tell Magick explicitly what format to use
        private static readonly Dictionary<string, MagickFormat> ExplicitFormats = new()
        {
            { ".tga",  MagickFormat.Tga  },
            { ".exr",  MagickFormat.Exr  },
            { ".hdr",  MagickFormat.Hdr  },
            { ".pcx",  MagickFormat.Pcx  },
            { ".j2c",  MagickFormat.J2c  },
            { ".j2k",  MagickFormat.J2k  },
            { ".jp2",  MagickFormat.Jp2  },
            { ".jpf",  MagickFormat.Jp2  },
            { ".jpx",  MagickFormat.Jp2  },
        };

        public static readonly string[] AllFormats =
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif",
            ".tif", ".tiff", ".ico", ".webp", ".psd",
            ".tga", ".exr", ".hdr", ".pcx", ".svg",
            ".j2c", ".j2k", ".jp2", ".jpf", ".jpx",
            ".dng", ".cr2", ".cr3", ".nef", ".arw",
            ".orf", ".rw2", ".raf", ".heic", ".heif", ".avif"
        };

        public static readonly string OpenFileFilter =
            "All Images|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.ico;" +
            "*.webp;*.psd;*.tga;*.exr;*.hdr;*.pcx;*.svg;" +
            "*.j2c;*.j2k;*.jp2;*.jpf;*.jpx;" +
            "*.dng;*.cr2;*.cr3;*.nef;*.arw;*.orf;*.rw2;*.raf;" +
            "*.heic;*.heif;*.avif|" +
            "JPEG|*.jpg;*.jpeg|" +
            "PNG|*.png|" +
            "BMP|*.bmp|" +
            "TIFF|*.tif;*.tiff|" +
            "WebP|*.webp|" +
            "PSD|*.psd|" +
            "TGA|*.tga|" +
            "JPEG 2000|*.j2c;*.j2k;*.jp2;*.jpf;*.jpx|" +
            "HDR|*.exr;*.hdr|" +
            "SVG|*.svg|" +
            "RAW|*.dng;*.cr2;*.cr3;*.nef;*.arw;*.orf;*.rw2;*.raf|" +
            "HEIC/HEIF|*.heic;*.heif|" +
            "ICO|*.ico|" +
            "All Files|*.*";

        // ── Cache ─────────────────────────────────────────────────
        private static readonly Dictionary<string, BitmapSource> _cache = new();
        private static CancellationTokenSource? _prefetchCts;

        public static void PrefetchAdjacent(string? prevPath, string? nextPath)
        {
            _prefetchCts?.Cancel();
            _prefetchCts = new CancellationTokenSource();
            var token = _prefetchCts.Token;

            Task.Run(() =>
            {
                foreach (var path in new[] { nextPath, prevPath })
                {
                    if (path == null || token.IsCancellationRequested) break;
                    if (_cache.ContainsKey(path)) continue;
                    try
                    {
                        var bmp = LoadInternal(path);
                        if (!token.IsCancellationRequested)
                            lock (_cache) { _cache[path] = bmp; }
                    }
                    catch { }
                }
            }, token);
        }

        public static void ClearCache() => _cache.Clear();

        public static BitmapSource Load(ImageFile file) => Load(file.FullPath);

        public static BitmapSource Load(string path)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(path, out var cached))
                    return cached;
            }
            var bmp = LoadInternal(path);
            lock (_cache) { _cache[path] = bmp; }
            return bmp;
        }

        public static async Task<BitmapSource> LoadAsync(string path)
        {
            lock (_cache)
            {
                if (_cache.TryGetValue(path, out var cached))
                    return cached;
            }
            var bmp = await Task.Run(() => LoadInternal(path));
            lock (_cache) { _cache[path] = bmp; }
            return bmp;
        }

        private static BitmapSource LoadInternal(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();

            BitmapSource bitmap;

            if (ext == ".svg")
                bitmap = LoadSvg(path);
            else if (ext == ".heic" || ext == ".heif")
                bitmap = LoadHeic(path);
            else if (Array.Exists(NativeFormats, e => e == ext))
                bitmap = LoadNative(path);
            else if (ExplicitFormats.TryGetValue(ext, out var magickFormat))
                bitmap = LoadViaMagickExplicit(path, magickFormat);
            else
                bitmap = LoadViaMagick(path);

            return NormalizeDpi(bitmap);
        }

        private static BitmapSource NormalizeDpi(BitmapSource source)
        {
            const double TargetDpi = 96.0;
            if (Math.Abs(source.DpiX - TargetDpi) < 0.5 &&
                Math.Abs(source.DpiY - TargetDpi) < 0.5)
                return source;

            var formatted = new FormatConvertedBitmap(
                source, PixelFormats.Bgra32, null, 0);
            int w = formatted.PixelWidth;
            int h = formatted.PixelHeight;
            int stride = w * 4;
            byte[] pixels = new byte[h * stride];
            formatted.CopyPixels(pixels, stride, 0);

            var normalized = BitmapSource.Create(
                w, h, TargetDpi, TargetDpi,
                PixelFormats.Bgra32, null, pixels, stride);
            normalized.Freeze();
            return normalized;
        }

        private static BitmapSource LoadNative(string path)
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(path);
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static BitmapSource LoadSvg(string path)
        {
            // WPF can render SVG natively via XamlReader for simple SVGs
            // but for full compatibility we use Magick.NET to rasterize
            using var image = new MagickImage();
            image.Read(path);
            image.Format = MagickFormat.Png;

            // Rasterize at a reasonable resolution
            if (image.Width < 512)
                image.Resize(1024, 1024);

            byte[] bytes = image.ToByteArray();
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static BitmapSource LoadHeic(string path)
        {
            try { return LoadViaMagick(path); }
            catch (Exception ex) when (
                ex.Message.Contains("HEIC") ||
                ex.Message.Contains("delegate") ||
                ex.Message.Contains("no decode delegate"))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show(
                        "HEIC/HEIF images require the HEIF Image Extensions codec.\n\n" +
                        "Would you like to open the Microsoft Store to install it?\n(It's free)",
                        "Codec Required",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                        System.Diagnostics.Process.Start(
                            new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = "ms-windows-store://pdp/?ProductId=9PMMSR1CGPWG",
                                UseShellExecute = true
                            });
                });
                throw new OperationCanceledException("HEIC codec not installed.");
            }
        }

        /// <summary>
        /// Load via Magick.NET with an explicit format hint.
        /// Used for formats that Magick can't reliably auto-detect (e.g. TGA).
        /// </summary>
        private static BitmapSource LoadViaMagickExplicit(string path,
            MagickFormat format)
        {
            var settings = new MagickReadSettings { Format = format };
            using var image = new MagickImage(path, settings);
            image.AutoOrient();
            image.Format = MagickFormat.Png;
            byte[] bytes = image.ToByteArray();

            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }

        private static BitmapSource LoadViaMagick(string path)
        {
            using var image = new MagickImage(path);
            image.AutoOrient();
            image.Format = MagickFormat.Png;
            byte[] bytes = image.ToByteArray();

            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.StreamSource = ms;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
    }
}