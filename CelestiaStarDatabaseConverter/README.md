# Star Database Converter for Celestia

Author: Charles Zhang  
Last Update: 2025-10-28  
Version: 0.0.1  
Target Ceelstai Version: 1.6.4  
Platform: .Net 8

CLI utility to convert **Celestia star database files (`stars.dat`, version 0x0100)** to and from a human-readable **YAML** format.  
Implements the binary specification for *Celestia/Star Database Format* using `YamlDotNet` and `BinaryReader/BinaryWriter`.

**Features**

* Converts between Celestia’s `stars.dat` and YAML.
* Supports coordinate derivation from RA/Dec/Distance via obliquity matrix.
* Handles packed spectral class encoding (K, T, S, L).
* Fully little-endian compliant (v0x0100 only).

## Install & Build

Run:

1. Clone this repo
2. Build and run from Visual Studio
1. Alternatively, run directly using `dotnet` CLI

Publish:

* Use Visual Studio/dotnet with single file option.
* Download from [Release](https://github.com/chaojian-zhang/CelestiaStarDatabaseConverter/releases) page.

## Usage

```bash
StarDataBaseConverter [command] [input] [output]
```

**Commands**

| Command       | Description                       |
| ------------- | --------------------------------- |
| `--to-yaml`   | Convert binary `stars.dat` → YAML |
| `--to-binary` | Convert YAML → binary `stars.dat` |
| `--print`     | Prints to console                 |
| `--help`      | Show usage                        |
| `--version`   | Display tool version              |

**Examples (using `dotnet`)**

```bash
# Convert binary to YAML
dotnet StarDataBaseConverter.dll --to-yaml stars.dat stars.yaml

# Convert YAML to binary
dotnet StarDataBaseConverter.dll --to-binary stars.yaml stars.dat

# Print
dotnet StarDataBaseConverter.dll --print stars.dat
dotnet StarDataBaseConverter.dll --print stars.yaml

# Show version or help
dotnet StarDataBaseConverter.dll --version
dotnet StarDataBaseConverter.dll --help
```

## Format Notes

* Binary header: `"CELSTARS"`, `0x0100`, record count.
* Record = HIP (u32), X/Y/Z (f32), AbsMag×256 (i16), Spectral (u16).
* Spectral packing: `(K<<12)|(T<<8)|(S<<4)|L`.
* Coordinate transform per matrix using ε = 23.4392911°.

## YAML Schema

```yaml
version: 0x0100
stars:
  - hip: 0 # uint
    x: 0.0 # float, ly
    y: 0.0
    z: 0.0
    # or supply ra/dec/distance_ly; ra/dec in deg
    abs_mag: 4.83 # real; stored as int16(abs_mag*256)
    spectral:
      packed: "0426"  # (hex K T S L)
      # or
      kind: 0 # 0=normal,1=WD,2=NS,3=BH
      type: 4 # see table
      subtype: 2 # 0..9,a=unknown(10)
      lum: 6 # 0..7, 8=unknown
```

**Spectral Type (T digit) summary**

| Value (hex) | Normal Star Type | White Dwarf Type |
| ----------- | ---------------- | ---------------- |
| 0           | O                | DA               |
| 1           | B                | DB               |
| 2           | A                | DC               |
| 3           | F                | DO               |
| 4           | G                | DQ               |
| 5           | K                | DZ               |
| 6           | M                | D (unknown)      |
| 7           | R                | DX               |
| 8           | S                | —                |
| 9           | N                | —                |
| a           | WC               | —                |
| b           | WN               | —                |
| c           | Unknown          | —                |
| d           | L                | —                |
| e           | T                | —                |
| f           | C                | —                |

Usage note:

For `kind = 0` (normal star), the `type` digit represents the spectral class from O to C.
For `kind = 1` (white dwarf), it represents DA–DX types; ignored for neutron stars or black holes (`kind = 2` or `3`).

## Changelog

* v0.0.1: Initial draft.

## References

* [Github doc](https://github.com/CelestiaProject/Celestia/wiki/Star-Database-binary-format)
* [Wikibook doc](https://en.wikibooks.org/wiki/Celestia/Star_Database_Format)

## License

MIT