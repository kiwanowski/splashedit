# SplashEdit

SplashEdit is a Unity Package that converts your Unity scenes into authentic PSX worlds by exporting binary data loadable in a PlayStation 1 as a binary fileformat called **SPLASHPACK**. It streamlines the export process for your scenes and offers additional tools for VRAM management and texture quantization.

## Important: Instructions in this readme are writen terribly (PR welcome, please I suck at writing these) REFER TO THE VIDEO DOWN BELOW FOR USAGE
https://youtu.be/1JJFYptDTk0

## Features

- **Automatic Scene Exporting:**  
  Export your scene with a single click using the PSX Scene Exporter component. This process automatically packs textures into the PSX's 2D VRAM.
- **Texture Packing & Quantization:**  
  Convert and preview your textures in a PSX-compatible format with built-in quantization tools.
- **Light baking**
  Prebakes any lights in the Scene into the output mesh data
- **Navmesh generation**
  Generates a navmesh for FPS movement in your scene

## Installation

Install SplashEdit directly from the Git repository using Unity's Package Manager:

1. **Open Unity's Package Manager:**  
   Go to `Window` → `Package Manager`.

2. **Add Package from Git URL:**  
   Click the **+** button in the upper left corner and select **"Add package from git URL..."**.  
   Enter the Git URL for SplashEdit: `https://github.com/psxsplash/splashedit.git`
   Click **Add** and wait for the package to install.

## Usage

### General Scene Exporting

If you only need to export the scene, follow these steps:

1. **PSX Object Exporter:**  
- Attach the **PSX Object Exporter** component to every GameObject you wish to export.
- This GameObject **must** have a mesh filter on it with a viable mesh
- The GameObject **must** have a texture on it. Colored polygons are not yet supported
- Set the desired bit depth for each object's texture in the component settings.

2. **PSX Scene Exporter:**  
- Add the **PSX Scene Exporter** component to a GameObject in your scene (using an empty GameObject is recommended for organization).
- Click the **Export** button in the PSX Scene Exporter. You will be prompted to choose an output file location for the splashpack.
- The exporter will automatically handle texture packing into the PSX's 2D VRAM.

3. **PSX Navmesh**  
- Add the **PSX Navmesh** component to a GameObject in your scene.
- A Navmesh surface will get automatically generated and exported into your splashpack upon scene export.
- **Important:** a PSX Player component is required within the scene for Navmesh exporting to work correctly.
- **Important:** due to a scaling and overflow bug which is currently being worked on, you may encounter some Navmesh issues. To mitigate these, turn up the GTE scaling on the scene exporter to a higher value.

### Additional Features

SplashEdit also includes extra tools to enhance your workflow:

1. **VRAM Editor:**  
- Access the VRAM Editor via Unity's **Window** context menu.
- Set framebuffer locations and preview texture packing.
- **Important:** Click on **Save Settings** in the VRAM Editor to inform the PSX Scene Exporter where to pack textures.
- When you click **Pack Textures** in the VRAM Editor, a file selection dialog will appear.  
  - Selecting a file will save only the VRAM data.
  - If you do not wish to save VRAM, simply close the dialog.  
  **Note:** This action only exports the VRAM. For a complete scene export (including VRAM), use the PSX Scene Exporter component.

2. **Quantized Texture Preview:**  
- Preview how your textures will look after quantization before exporting.

## Texture Requirements

- **Power of Two:**  
All textures must have dimensions that are a power of two (e.g., 64x64, 128x128, 256x256) with a maximum size of **256x256**.
- **No Automatic Downscaling:**  
SplashEdit does not automatically downscale textures that exceed these limits.

## Output Format
You can use the [Imhex project file](https://github.com/psxsplash/splashedit/blob/main/tools/imhex.hexproj) file to learn about the binary format

## I have a splashpack file. What now?
You can preview it using [psxsplash](https://github.com/psxsplash/psxsplash).

## Contributing

Contributions are welcome! To contribute:

1. Fork the repository.
3. Submit a pull request with your changes.

Please use branches.
For major changes, please open an issue first to discuss your ideas.


