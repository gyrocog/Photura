using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ImageMagick;
using Photura.Models;

namespace Photura.Services
{
    public static class MetadataService
    {
        private static readonly HttpClient _http = new()
        {
            DefaultRequestHeaders =
            {
                { "User-Agent", "Photura/1.0 (Windows image viewer)" }
            },
            Timeout = TimeSpan.FromSeconds(4)
        };

        public static async Task<ImageMetadata> ReadAsync(string path)
        {
            var meta = new ImageMetadata
            {
                FullPath = path,
                FileName = Path.GetFileName(path)
            };

            // File size — fast, do synchronously
            try
            {
                var fi = new FileInfo(path);
                meta.FileSizeBytes = fi.Length;
                meta.FileSizeText = FormatFileSize(fi.Length);
            }
            catch { }

            // EXIF — run on background thread so UI never blocks
            await Task.Run(() => ReadExif(meta, path));

            // GPS reverse geocode — only if we have coordinates
            if (meta.Latitude.HasValue && meta.Longitude.HasValue)
            {
                meta.PlaceName = await ReverseGeocodeAsync(
                    meta.Latitude.Value, meta.Longitude.Value);
            }

            return meta;
        }

        private static void ReadExif(ImageMetadata meta, string path)
        {
            try
            {
                using var image = new MagickImage(path);

                // Pixel size
                meta.PixelSize = $"{image.Width} × {image.Height} px";

                // DPI — use a single value, default to 96 if missing or zero
                double dpiX = image.Density.X;
                double dpiY = image.Density.Y;
                double dpi = dpiX > 0 ? dpiX : (dpiY > 0 ? dpiY : 96);
                meta.Dpi = $"{(int)Math.Round(dpi)} DPI";

                // Color depth
                meta.ColorDepth = $"{image.Depth * (int)image.ChannelCount} bit";

                var profile = image.GetExifProfile();
                if (profile == null) return;

                // Date Taken (shutter click)
                var dt = GetExifString(profile, ExifTag.DateTimeOriginal)
                      ?? GetExifString(profile, ExifTag.DateTime);
                if (!string.IsNullOrEmpty(dt))
                {
                    if (DateTime.TryParseExact(dt, "yyyy:MM:dd HH:mm:ss",
                        null, System.Globalization.DateTimeStyles.None,
                        out var parsed))
                    {
                        meta.DateTaken = parsed.ToString("MMMM d, yyyy");
                        meta.TimeTaken = parsed.ToString("HH:mm:ss");
                    }
                }

                // Camera
                meta.Make = GetExifString(profile, ExifTag.Make) ?? string.Empty;
                meta.Model = GetExifString(profile, ExifTag.Model) ?? string.Empty;
                meta.Lens = GetExifString(profile, ExifTag.LensModel) ?? string.Empty;

                // Focal length
                var focal = GetExifRational(profile, ExifTag.FocalLength);
                if (focal.HasValue)
                    meta.FocalLength = $"{focal.Value:0.#} mm";

                // F-stop
                var fstop = GetExifRational(profile, ExifTag.FNumber);
                if (fstop.HasValue)
                    meta.FStop = $"f/{fstop.Value:0.#}";

                // Shutter speed
                var exp = GetExifRational(profile, ExifTag.ExposureTime);
                if (exp.HasValue)
                {
                    meta.ShutterSpeed = exp.Value < 1
                        ? $"1/{(int)Math.Round(1 / exp.Value)}s"
                        : $"{exp.Value:0.##}s";
                }

                // ISO
                var isoRaw = profile.GetValue(ExifTag.ISOSpeedRatings);
                if (isoRaw?.Value != null && isoRaw.Value.Length > 0)
                    meta.Iso = isoRaw.Value[0].ToString();

                // Exposure bias
                var bias = GetExifRational(profile, ExifTag.ExposureBiasValue);
                if (bias.HasValue)
                    meta.Exposure = $"{bias.Value:+0.#;-0.#;0} EV";

                // Flash
                var flashRaw = profile.GetValue(ExifTag.Flash);
                if (flashRaw?.Value != null)
                    meta.Flash = (flashRaw.Value & 1) == 1 ? "Flash fired" : "No flash";

                // White balance
                var wb = GetExifValue<ushort>(profile, ExifTag.WhiteBalance);
                if (wb.HasValue)
                    meta.WhiteBalance = wb.Value == 0 ? "Auto" : "Manual";

                // Description
                meta.Description = GetExifString(profile,
                    ExifTag.ImageDescription) ?? string.Empty;
                meta.Copyright = GetExifString(profile,
                    ExifTag.Copyright) ?? string.Empty;

                // GPS
                var gpsLat = profile.GetValue(ExifTag.GPSLatitude);
                var gpsLatR = profile.GetValue(ExifTag.GPSLatitudeRef);
                var gpsLon = profile.GetValue(ExifTag.GPSLongitude);
                var gpsLonR = profile.GetValue(ExifTag.GPSLongitudeRef);

                if (gpsLat?.Value != null && gpsLon?.Value != null)
                {
                    double lat = ConvertGps(gpsLat.Value);
                    double lon = ConvertGps(gpsLon.Value);
                    if (gpsLatR?.Value?.ToString() == "S") lat = -lat;
                    if (gpsLonR?.Value?.ToString() == "W") lon = -lon;

                    meta.Latitude = lat;
                    meta.Longitude = lon;
                    meta.Coordinates = $"{lat:0.0000}, {lon:0.0000}";
                }
            }
            catch { }
        }

        private static async Task<string> ReverseGeocodeAsync(double lat, double lon)
        {
            try
            {
                string url = $"https://nominatim.openstreetmap.org/reverse" +
                             $"?lat={lat:0.000000}&lon={lon:0.000000}" +
                             $"&format=json&zoom=10&accept-language=en";

                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("address", out var addr))
                {
                    // Build a clean place string: City, Country
                    string city = GetJsonString(addr, "city")
                                  ?? GetJsonString(addr, "town")
                                  ?? GetJsonString(addr, "village")
                                  ?? string.Empty;
                    string country = GetJsonString(addr, "country") ?? string.Empty;

                    if (!string.IsNullOrEmpty(city) && !string.IsNullOrEmpty(country))
                        return $"{city}, {country}";
                    if (!string.IsNullOrEmpty(country))
                        return country;
                }

                // Fallback to display_name if address parsing fails
                if (doc.RootElement.TryGetProperty("display_name", out var name))
                    return name.GetString() ?? string.Empty;
            }
            catch { }

            return string.Empty;
        }

        private static string? GetJsonString(JsonElement el, string key)
        {
            if (el.TryGetProperty(key, out var val))
            {
                var s = val.GetString();
                return string.IsNullOrEmpty(s) ? null : s;
            }
            return null;
        }

        // ── EXIF helpers ─────────────────────────────────────────
        private static string? GetExifString(IExifProfile profile, ExifTag<string> tag)
        {
            var val = profile.GetValue(tag);
            var str = val?.Value?.Trim();
            return string.IsNullOrEmpty(str) ? null : str;
        }

        private static double? GetExifRational(IExifProfile profile,
            ExifTag<Rational> tag)
        {
            var val = profile.GetValue(tag);
            if (val?.Value == null) return null;
            var r = val.Value;
            return r.Denominator == 0 ? null : (double)r.Numerator / r.Denominator;
        }

        private static double? GetExifRational(IExifProfile profile,
            ExifTag<SignedRational> tag)
        {
            var val = profile.GetValue(tag);
            if (val?.Value == null) return null;
            var r = val.Value;
            return r.Denominator == 0 ? null : (double)r.Numerator / r.Denominator;
        }

        private static T? GetExifValue<T>(IExifProfile profile, ExifTag<T> tag)
            where T : struct
        {
            var val = profile.GetValue(tag);
            return val?.Value;
        }

        private static double ConvertGps(Rational[] rationals)
        {
            if (rationals.Length < 3) return 0;
            double deg = rationals[0].Denominator == 0 ? 0 :
                (double)rationals[0].Numerator / rationals[0].Denominator;
            double min = rationals[1].Denominator == 0 ? 0 :
                (double)rationals[1].Numerator / rationals[1].Denominator;
            double sec = rationals[2].Denominator == 0 ? 0 :
                (double)rationals[2].Numerator / rationals[2].Denominator;
            return deg + min / 60 + sec / 3600;
        }

        public static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
            return $"{bytes / (1024.0 * 1024):0.##} MB";
        }
    }
}