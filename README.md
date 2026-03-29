# Photura

A lightweight, fast image viewer and editor for Windows — built as a personal replacement for the Windows Photos app.

## Features

### Viewer
- Opens all common image formats: JPEG, PNG, BMP, GIF, TIFF, WebP, ICO, PSD
- RAW support: DNG, CR2, CR3, NEF, ARW, ORF, RW2, RAF
- HEIC/HEIF support (requires HEIF Image Extensions from Microsoft Store)
- Mousewheel navigation through images in the same folder
- Ctrl+Scroll to zoom, right-click or middle-click to pan
- Copy image to clipboard
- Delete to Recycle Bin
- Remembers window size, position and theme between sessions

### Editor
- **Crop** — always-visible crop area with handles, aspect ratio presets (Free, 1:1, 4:3, 16:9, 9:16, and more), rule-of-thirds grid
- **Rotate & Flip** — 90° CW/CCW, flip horizontal/vertical
- **Straighten** — fine rotation slider (-45° to +45°)
- **Image resize** — resample the working image up or down while keeping crop area fixed
- **Adjustments** — Brightness, Exposure, Contrast, Highlights, Shadows, Vignette, Saturation, Warmth, Tint, Sharpness
- **Filters** — 24 filters including Vivid, Warm, Cool, Grayscale, Sepia, Kodachrome, Fuji, Polaroid, Lomo, Cross Process, Golden Hour, Moonlight, Cinematic, Bleach Bypass, Duotone, Infrared, Halation, and more
- **Filter intensity** — blend any filter from 0–100%
- **Compare** — toggle to preview original image without edits
- Mousewheel support on all sliders
- Double-click any slider to reset to default
- Dark and Light theme, remembered between sessions

## Requirements

- Windows 10 or later (64-bit)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- For HEIC/HEIF: [HEIF Image Extensions](https://www.microsoft.com/store/productId/9PMMSR1CGPWG) (free, from Microsoft Store)

## Installation

Download `PhotouraSetup.msi` from the [Releases](https://github.com/gyrocog/Photura/releases) page and run it.

The installer will:
- Install Photura to `C:\Program Files\Photura\`
- Create a Start Menu shortcut
- Register Photura in the "Open with" menu for all supported image formats

You can then set Photura as your default image viewer in **Windows Settings → Apps → Default Apps**.

## Building from source

1. Install [Visual Studio 2026](https://visualstudio.microsoft.com/) with the **.NET desktop development** workload
2. Clone the repository
3. Open `Photura.sln` in Visual Studio
4. Build and run with **F5**

To build the installer:
1. Install [WiX Toolset v6](https://wixtoolset.org/): `dotnet tool install --global wix`
2. Install extensions: `wix extension add --global WixToolset.UI.wixext/6.0.2`
3. Build in Release mode in Visual Studio
4. Run: `wix build PhotouraSetup.wxs -o PhotouraSetup.msi -ext WixToolset.UI.wixext -arch x64`

## Tech stack

- C# / WPF / .NET 10
- [Magick.NET](https://github.com/dlemstra/Magick.NET) for extended format support
- [WiX Toolset v6](https://wixtoolset.org/) for the installer

## License

See [LICENSE](LICENSE) for details.