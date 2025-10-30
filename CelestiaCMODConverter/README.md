# Celestia CMOD Converter

Author: Charles Zhang  
Dependency: [AssimpNet](https://bitbucket.org/Starnick/assimpnet/src/master/)
Version: 0.0.1  
Target Celestia Version: 1.6.4

A command-line utility for converting standard 3D model formats (`.obj`, `.fbx`, `.dae`, etc.) into **Celestia CMOD** files (ASCII or binary).  
Built on [AssimpNet](https://www.nuget.org/packages/AssimpNet) for model import.

Technical notes:

* AssImp has lots of versions for .Net, none seems very "official". E.g. [this fork](https://github.com/StirlingLabs/Assimp.Net) and [this](https://github.com/assimp/assimp-net). Based on previous experience, we chose [AssimpNet](https://www.nuget.org/packages/AssimpNet/4.1.0).

Features:

* Inspect common model types (.obj, .cmod, etc.)
* Load models via **AssimpNet**
* Output **CMOD ASCII** (`#celmodel__ascii`) or **CMOD binary** (`#celmodel_binary`)
* Auto-detects vertex attributes: Position, normal, tangent, color0, texcoord0–3
* Converts materials (diffuse, specular, emissive, specpower, opacity)
* Writes textures and blend modes
* Strips away absolute paths of textures
* CLI options for inspection, scaling, and help/version display
* Compatible with Celestia v1.5.0 and later.

Limitations:

* Currently supports only `trilist` primitive type.

## Installation, Build & Publish

```bash
dotnet add package AssimpNet
dotnet build -c Release
```

## CLI Options

| Option                | Description                         |
| --------------------- | ----------------------------------- |
| `--input <file>`      | Input model file (.obj, .fbx, etc.) |
| `--output <file>`     | Output .cmod file                   |
| `--format <ascii/bin>`| Output CMOD type                    |
| `--scale <float>`     | Optional scale multiplier           |
| `--version, -v`       | Show tool version                   |
| `--help, -h`          | Show usage summary                  |

## Usage

```bash
# Display help
CelestiaCMODConverter --help

# Inspect a model
CelestiaCMODConverter inspect --input model.obj

# Convert to ASCII CMOD
CelestiaCMODConverter convert --input model.fbx --output model_ascii.cmod --format ascii

# Convert to Binary CMOD
CelestiaCMODConverter convert --input model.obj --output model_bin.cmod --format bin

# Apply a scale factor
CelestiaCMODConverter convert --input model.obj --output scaled.cmod --format ascii --scale 0.01
```

Notes:

* ASCII header is `#celmodel__ascii`. Binary header is `#celmodel_binary` and both are exactly 16 bytes.
* Binary CMODs use little-endian encoding for all numeric fields; Binary strings use 16-bit length + ASCII bytes (no null terminator).
* Materials precede meshes.
* Meshes are exported as **triangle lists** (`trilist`).
* Outputs one primitive group per mesh as a triangle list using the mesh’s `MaterialIndex`.
* Colors in `color0` are written as `ub4` in binary and as four floats in ASCII to match attribute `ub4` vs textual dump.

## CMOD File Format Specification

Celestia supports **two CMOD variants**: a human-readable ASCII format and a compact binary format. Both encode identical logical data: materials first, then meshes with vertex attributes and primitives.

### ASCII CMOD (`#celmodel__ascii`)

Header line:

```
#celmodel__ascii
```

Each section uses text keywords and blocks:

| Section                   | Description                                                     |
| ------------------------- | --------------------------------------------------------------- |
| `material … end_material` | Defines colors, specular power, opacity, and texture paths.     |
| `mesh … end_mesh`         | Contains vertex declaration, vertex data, and primitive groups. |

**Example:**

```
#celmodel__ascii
material
diffuse 1.000000 1.000000 1.000000 1.000000
texture0 "diffuse.png"
end_material

mesh
vertexdesc
position f3
normal f3
texcoord0 f2
end_vertexdesc
vertices 3
0.0 0.0 0.0  0.0 0.0 1.0  0.0 0.0
1.0 0.0 0.0  0.0 0.0 1.0  1.0 0.0
0.0 1.0 0.0  0.0 0.0 1.0  0.0 1.0
trilist 0 3
0 1 2
end_mesh
```

* Vertex attributes are space-separated.
* Indices are unsigned integers;
* `trilist` defines triangles per material.

### Binary CMOD (`#celmodel_binary`)

Header: exactly **16 bytes** (`#celmodel_binary` in ASCII):

```
23 63 65 6C 6D 6F 64 65 6C 5F 62 69 6E 61 72 79
```

All numbers are **little-endian**. Strings use:

```
uint16 length
[length bytes ASCII]
```

**Structure**

| Element                               | Description                                                         |
| ------------------------------------- | ------------------------------------------------------------------- |
| `TK_material` … `TK_end_material`     | Blocks defining materials and texture bindings                      |
| `TK_mesh` … `TK_end_mesh`             | Each mesh with vertex layout and trilist indices                    |
| `TK_vertexdesc` … `TK_end_vertexdesc` | Pairs of `(semantic, format)` identifiers                           |
| `TK_vertices`                         | `uint32 count` followed by packed vertex data                       |
| `PR_trilist`                          | `uint32 materialIndex`, `uint32 indexCount`, then that many indices |

**Common Token IDs (ushort)**

| Token               | Value  | Meaning                 |
| ------------------- | ------ | ----------------------- |
| `TK_material`       | 0x0001 | Start material          |
| `TK_end_material`   | 0x0002 | End material            |
| `TK_mesh`           | 0x0010 | Start mesh              |
| `TK_end_mesh`       | 0x0011 | End mesh                |
| `TK_vertexdesc`     | 0x0020 | Vertex descriptor start |
| `TK_end_vertexdesc` | 0x0021 | Vertex descriptor end   |
| `TK_vertices`       | 0x0030 | Vertex array            |
| `PR_trilist`        | 0x0040 | Triangle list primitive |

Vertex attributes are defined using 16-bit unsigned integer tokens:

```
Semantic IDs: VS_position, VS_normal, VS_tangent, VS_color0, VS_texcoord0–3
Format IDs: VF_f3, VF_ub4, VF_f2, etc.
```

Color attributes use 4 unsigned bytes (0–255). All other attributes use 32-bit floats.

**Binary example layout (pseudo):**

```
#celmodel_binary
[material blocks...]
[mesh blocks...]
TK_mesh
  TK_vertexdesc
    VS_position VF_f3
    VS_normal   VF_f3
    VS_texcoord0 VF_f2
  TK_end_vertexdesc
  TK_vertices
    uint32 3
    [float32*3 position][float32*3 normal][float32*2 uv] × 3 vertices
  PR_trilist
    uint32 materialIndex
    uint32 indexCount
    uint32[indexCount]
TK_end_mesh
```

**Notes**

* Binary and ASCII represent the same scene graph; Binary uses an additional "count" field for variable arrays.
* Both omit hierarchy and animations; only static meshes and materials are written.
* Binary size ≈ one third of ASCII size.
* Endianness is **little-endian only**.
* Strings (uint16 length + ASCII, not null-terminated) and numeric constants (little-endian 32-bit floats or integers) match Celestia 1.6.4’s loader expectations.

## License

MIT

## Changelog

* v0.0.1: Initial implementation.