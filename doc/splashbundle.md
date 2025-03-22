# SPLASHPACK Binary File Format Specification

This specification describes the binary file layout for the SP exporter. All numeric values are stored in little‐endian format. All offsets are counted from the beginning of the file.

---

## 1. File Header

| Offset  | Size | Type   | Description                                                     |
| ------- | ---- | ------ | --------------------------------------------------------------- |
| 0x00    | 1    | char   | `'S'` – File identifier                                         |
| 0x01    | 1    | char   | `'P'` – File identifier                                         |
| 0x02    | 2    | uint16 | Version number (currently **1**)                                |
| 0x04    | 2    | uint16 | Number of GameObject (exporter) descriptors                     |
| 0x06    | 2    | uint16 | Number of Texture Atlas descriptors                             |

---

## 2. Metadata Section

The metadata section is split into two parts: **Object Descriptors** and **Atlas Descriptors**.

### 2.1 Object (Exporter) Descriptors

For each exporter, the following fields are stored sequentially:

| Offset (per entry) | Size     | Type    | Description                                                          |
| ------------------ | -------- | ------- | -------------------------------------------------------------------- |
| 0x00               | 4        | int     | X coordinate (GTE-converted)                                         |
| 0x04               | 4        | int     | Y coordinate (GTE-converted)                                         |
| 0x08               | 4        | int     | Z coordinate (GTE-converted)                                         |
| 0x0C               | 36       | int[9]  | Rotation matrix (3×3, row-major order)                               |
| 0x30               | 2        | uint16  | Texture page attributes (encoded from page X/Y, bit depth, dithering)  |
| 0x32               | 2        | uint16  | Number of triangles in the mesh                                    |
| 0x34               | 4        | int     | Mesh data offset placeholder                                         |

*Each object descriptor occupies **0x38** bytes.*

---

### 2.2 Texture Atlas Descriptors

For each texture atlas, the following fields are stored sequentially:

| Offset (per entry) | Size | Type   | Description                                                    |
| ------------------ | ---- | ------ | -------------------------------------------------------------- |
| 0x00               | 4    | int    | Atlas raw data offset placeholder                              |
| 0x04               | 2    | uint16 | Atlas width                                                    |
| 0x06               | 2    | uint16 | Atlas height (always 256, but defined for future-proofing)     |
| 0x08               | 2    | uint16 | Atlas position X – relative to VRAM origin                     |
| 0x0A               | 2    | uint16 | Atlas position Y – relative to VRAM origin                     |

*Each atlas descriptor occupies **0x0C** bytes.*

---

## 3. Data Section

The data section is composed of two distinct blocks: **Mesh Data Blocks** and **Atlas Data Blocks**.

### 3.1 Mesh Data Blocks

For each exporter, a mesh data block is written. The starting offset of each block (stored in its corresponding object descriptor) is counted from the beginning of the file. Within each mesh data block, data for every triangle is stored sequentially using the following layout:

| Field                         | Size per element        | Type     | Description                                                                              |
| ----------------------------- | ----------------------- | -------- | ---------------------------------------------------------------------------------------- |
| **Vertex Coordinates**        | 3 × 2 bytes per vertex  | int16    | For each vertex (v0, v1, v2): X, Y, Z coordinates                                          |
| **Vertex Normal**             | 3 × 2 bytes             | int16    | For vertex v0 only: Normal vector components (nx, ny, nz)                                |
| **Texture Coordinates (UVs)** | 1 + 1 bytes per vertex  | uint8    | For each vertex (v0, v1, v2): U and V coordinates (adjusted by texture packing factors)    |
| **UV Padding**                | 2 bytes                 | uint16   | Padding (set to zero)                                                                    |
| **Vertex Colors**             | 3 + 1 bytes per vertex  | uint8    | For each vertex (v0, v1, v2): Color channels (red, green, blue) and 1 byte of padding      |

*The overall size per triangle is calculated based on the fixed sizes above multiplied by the number of vertices and triangles.*

---

### 3.2 Atlas Data Blocks

For each texture atlas, the raw texture data is stored as a 2D array. Before writing each atlas data block, the file pointer is aligned to a 4-byte boundary. The starting offset of each atlas block (stored in its corresponding atlas descriptor) is counted from the beginning of the file.

| Field         | Description                                                                                         |
| ------------- | --------------------------------------------------------------------------------------------------- |
| **Raw Texture Data** | The atlas data is written pixel by pixel.

---