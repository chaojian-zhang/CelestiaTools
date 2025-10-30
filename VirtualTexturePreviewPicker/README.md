# Virtual Texture Preview Picker

Version: 0.0.1

A compact tool for visualizing and slicing images into hierarchical grid tiles.

## Usage

Run the program, load an image, adjust split level, zoom in, and right-click a cell to save that tile.

## Features

* Image Load: `Ctrl + O` or double-click to open an image.
* Split Level Control:
  * Range **0–12**
  * Change using ↑/↓, `W`/`S`, PageUp/PageDown, or mouse wheel.
  * Window title shows current level.
* Grid Overlay:
  * Level *N* divides image into `2^(N+1)` × `2^N` cells.
  * Each cell labeled with its `(x, y)` coordinate.
  * 13 distinct label colors by level.
  * Control background dim using +/-.
* Zoom & Pan:
  * `Ctrl + Mouse Wheel` zooms toward cursor.
  * `F` recenters the image (resets zoom).
* Tile Export:
  * Right-click any grid cell to export that tile to PNG (512×512).
  * Uses SkiaSharp for precise cropping and resampling.

## Build

Requires:

* .NET 8+ SDK
* SkiaSharp (NuGet: `SkiaSharp`, `SkiaSharp.NativeAssets.Windows`)

Compile with:

```bash
dotnet build
```

## Output

Exports tile as:

```
level<Level>_tx_<X>_<Y>.png
```

Each corresponds to the exact cropped region from the source image into a `512x512` tile.

## Changelog

* v0.0.1: Initial implementation.