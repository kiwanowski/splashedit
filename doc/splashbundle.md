# SPLASHPACK Binary File Format Specification

All numeric values are stored in little‐endian format. All offsets are counted from the beginning of the file.

---

## 1. File Header (16 bytes)

| Offset | Size | Type   | Description                         |
| ------ | ---- | ------ | ----------------------------------- |
| 0x00   | 2    | char   | `'SP'` – File magic                 |
| 0x02   | 2    | uint16 | Version number (currently **1**)    |
| 0x04   | 2    | uint16 | Number of Exporter descriptors      |
| 0x06   | 2    | uint16 | Number of Texture Atlas descriptors |
| 0x08   | 2    | uint16 | Number of CLUT descriptors          |
| 0x0A   | 3*2  | uint16 | Reserved (always 0)                 |

---

## 2. Metadata Section

The metadata section comprises three groups of descriptors.

### 2.1 GameObject Descriptors (56 bytes each)

Each gameobject descriptor stores the transform and mesh metadata for one GameObject.

| Offset (per entry) | Size | Type   | Description                       |
| ------------------ | ---- | ------ | --------------------------------- |
| 0x00               | 4    | int    | **Mesh Data Offset**              |
| 0x04               | 4    | int    | X coordinate (Fixed-point)        |
| 0x08               | 4    | int    | Y coordinate (Fixed-point)        |
| 0x0C               | 4    | int    | Z coordinate (Fixed-point)        |
| 0x10               | 36   | int[9] | 3×3 Rotation matrix (Fixed-point) |
| 0x28               | 4    | uint16 | Triangle count in the mesh        |
| 0x2A               | 2    | uint16 | Reserved                          |

### 2.2 Texture Atlas Descriptors (12 bytes each)

Each texture atlas descriptor holds atlas layout data and a placeholder for the atlas raw data offset.

| Offset (per entry) | Size | Type   | Description                                              |
| ------------------ | ---- | ------ | -------------------------------------------------------- |
| 0x00               | 4    | int    | **Atlas Data Offset Placeholder**                        |
| 0x04               | 2    | uint16 | Atlas width                                              |
| 0x06               | 2    | uint16 | Atlas height (currently always 256, for future-proofing) |
| 0x08               | 2    | uint16 | Atlas position X – relative to VRAM origin               |
| 0x0A               | 2    | uint16 | Atlas position Y – relative to VRAM origin               |

### 2.3 CLUT Descriptors (12 bytes each)

CLUTs are the only data which is stored in the Metadata section.
For each CLUT (Color Lookup Table) associated with an atlas texture that has a palette:

| Offset (per entry) | Size | Type   | Description                                           |
| ------------------ | ---- | ------ | ----------------------------------------------------- |
| 0x00               | 4    | int    | **Clut Data Offset Placeholder**                      |
| 0x04               | 2    | uint16 | CLUT packing X coordinate - already in 16 pixel steps |
| 0x06               | 2    | uint16 | CLUT packing Y coordinate                             |
| 0x08               | 2    | uint16 | Palette count (number of valid palette entries)       |
| 0x0A               | 2    | uint16 | Reserved (always 0)                                   |

---

## 3. Data Section

The data section contains the actual mesh and atlas raw data.

### 3.1 Mesh Data Blocks

For each exporter, a mesh data block is written at the offset specified in its descriptor. Each mesh block contains data for all triangles of the associated mesh.

#### **Triangle Data Layout (per triangle – 52 bytes total):**

| Field                         | Size                                                     | Description                                                                                    |
| ----------------------------- | -------------------------------------------------------- | ---------------------------------------------------------------------------------------------- |
| **Vertex Coordinates**        | 3 vertices × 3 × 2 bytes = 18 bytes                      | For each vertex (v0, v1, v2): X, Y, Z coordinates (int16)                                      |
| **Vertex Normal**             | 3 × 2 bytes = 6 bytes                                    | Normal vector for vertex v0 (int16: nx, ny, nz)                                                |
| **Vertex Colors**             | 3 vertices × (3 bytes color + 1 byte padding) = 12 bytes | For each vertex (v0, v1, v2): Red, Green, Blue (uint8) plus 1 byte padding                     |
| **Texture Coordinates (UVs)** | 3 vertices × 2 bytes = 6 bytes                           | For each vertex (v0, v1, v2): U and V coordinates (uint8), adjusted by texture packing factors |
| **UV Padding**                | 2 bytes                                                  | Padding (uint16, set to zero)                                                                  |
| **Texture Attributes**        | 2 bytes                                                  | The TPage attribute for the given polygon                                                      |
| **Clut X**                    | 2 bytes                                                  | Clut position within VRAM (already predivided by 16)                                           |
| **Clut Y**                    | 2 bytes                                                  | Clut position within VRAM                                                                      |
| **Padding**                   | 2 bytes                                                  |                                                                                                |




### 3.2 Atlas Data Blocks

For each atlas, a raw texture data block is written at the offset specified in its descriptor

- **Raw Texture Data:**  
  The atlas data is written pixel by pixel as returned by the pixel packing function. The total size equals  
  *(Atlas Width × Atlas Height)* The data is prepared for a DMA transfer to the VRAM.


### 3.3 Clut Data Blocks

For each clut, a raw pixel data block is written at the offset specified in its descriptor as an array of `uint16` colors already formatted for the VRAM.

---
