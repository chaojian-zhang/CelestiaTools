# VirtualTextureReferenceSet

A command-line tool that generates **reference virtual texture tiles** using **SkiaSharp** for visualization and testing of texture paging systems.  
Each texture level contains a grid of numbered tiles with colored borders and a centered `<x>_<y>` label.  
The program also generates configuration files (`.ctx` and `.ssc`) describing the virtual texture set.

## Features

- Generates tiled reference images per level (`level0`, `level1`, …).
- Adjustable tile resolution (default 512×512).
- Supports up to 13 levels (`0–12`).
- Colored borders and level-based text colors.
- Outputs sidecar files:
  - `<FolderName>.ctx` – texture configuration.
  - `<FolderName>.ssc` – surface set configuration.

## Usage

```bash
VirtualTextureReferenceSet <outputFolder> [--tile <int>] [--levels <0–12>]
VirtualTextureReferenceSet --help
VirtualTextureReferenceSet --version
````

### Arguments

| Argument         | Description                                                 |
| ---------------- | ----------------------------------------------------------- |
| `<outputFolder>` | Root output directory; subfolders `levelN` will be created. |

### Options

| Option            | Description                               | Default |
| ----------------- | ----------------------------------------- | ------- |
| `--tile <int>`    | Tile resolution (square).                 | 512     |
| `--levels <0–12>` | Highest level to generate (creates 0..N). | 0       |
| `--help`, `-h`    | Show help information.                    |         |
| `--version`, `-v` | Display version.                          |         |

## Output Structure

```
<outputFolder>/
├── level0/
│   ├── tx_0_0.png
│   └── tx_1_0.png
├── level1/
│   ├── tx_0_0.png
│   ├── tx_0_1.png
│   └── ...
└── levelN/
```

* Each tile has a **white background**, **thick colored border**, and **centered coordinate label**.
* Level `N` contains:

  * `x ∈ [0, 2^(N+1)−1]`
  * `y ∈ [0, 2^N−1]`
  * Total tiles = `2^(2N+1)`

## Generated Sidecar Files

Located next to the output folder (same parent directory):

### `<FolderName>.ctx`

```text
VirtualTexture
{
        ImageDirectory "<FolderName>"
        BaseSplit 0
        TileSize <TileResolution>
        TileType "png"
}
```

### `<FolderName>.ssc`

```text
AltSurface "<FolderName>" "Parent/Child"
{
    Texture "<FolderName>.ctx"
}
```

## Build Instructions

Requires [.NET 8 SDK](https://dotnet.microsoft.com/download) and [SkiaSharp](https://www.nuget.org/packages/SkiaSharp).

```bash
dotnet new console -n VirtualTextureReferenceSet
cd VirtualTextureReferenceSet
dotnet add package SkiaSharp
# Replace Program.cs with this source
dotnet build
dotnet run -- ./Output --tile 512 --levels 3
```

## Technical Overview

* **Language:** C# (.NET 8)
* **Graphics:** SkiaSharp (`SKSurface`, `SKCanvas`)
* **Concurrency:** `Parallel.For` for parallel tile generation
* **Color Palette:** 13 distinct level colors
* **Output Format:** PNG (lossless)
* **Design:** No external dependencies beyond SkiaSharp

## License

MIT.

Free for any purpose. Attribution appreciated but not required.