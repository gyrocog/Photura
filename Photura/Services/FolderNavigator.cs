using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photura.Models;

namespace Photura.Services
{
    public class FolderNavigator
    {
        private List<string> _files = new();
        private int _currentIndex = -1;

        public ImageFile? Current =>
            _currentIndex >= 0 && _currentIndex < _files.Count
                ? new ImageFile(_files[_currentIndex])
                : null;

        public int CurrentIndex => _currentIndex;
        public int TotalCount => _files.Count;
        public bool HasNext => _currentIndex < _files.Count - 1;
        public bool HasPrev => _currentIndex > 0;

        public string? GetPath(int index) =>
            index >= 0 && index < _files.Count ? _files[index] : null;

        public void LoadFolder(string filePath)
        {
            string folder = Path.GetDirectoryName(filePath) ?? string.Empty;

            _files = Directory
                .EnumerateFiles(folder)
                .Where(f => ImageLoader.AllFormats.Contains(
                    Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _currentIndex = _files.FindIndex(f =>
                string.Equals(f, filePath, StringComparison.OrdinalIgnoreCase));

            if (_currentIndex < 0 && _files.Count > 0)
                _currentIndex = 0;
        }

        public ImageFile? GoNext()
        {
            if (!HasNext) return Current;
            _currentIndex++;
            return Current;
        }

        public ImageFile? GoPrev()
        {
            if (!HasPrev) return Current;
            _currentIndex--;
            return Current;
        }

        public void RemoveCurrent()
        {
            if (_currentIndex < 0 || _currentIndex >= _files.Count) return;
            _files.RemoveAt(_currentIndex);
            if (_currentIndex >= _files.Count)
                _currentIndex = _files.Count - 1;
        }

        public string GetPositionText() =>
            _files.Count == 0 ? string.Empty : $"{_currentIndex + 1} / {_files.Count}";
    }
}