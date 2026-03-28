using System.IO;

namespace Photura.Models
{
    /// <summary>
    /// Represents a single image file on disk.
    /// </summary>
    public class ImageFile
    {
        public string FullPath { get; }
        public string FileName => Path.GetFileName(FullPath);
        public string Extension => Path.GetExtension(FullPath).ToLowerInvariant();
        public string FolderPath => Path.GetDirectoryName(FullPath) ?? string.Empty;

        public bool IsPsd => Extension == ".psd";

        public ImageFile(string fullPath)
        {
            FullPath = fullPath;
        }

        public override string ToString() => FileName;
    }
}