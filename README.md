Update: This readme was written by MrQuetch (Formally Quilt#6855 on Discord).

**Note: It does use some of Bandwidth's original readme plus my own additions.**

## SplashEdit

``SplashEdit`` is a Unity package written by Bandwidth that converts a Unity scene to a binary format called ``SplashPack`` which will then be built into a PS1 executable. You will need a few things for this to work but I first want to point out these things:

- Visual Studio Code
- PSYQo
- PSXSplash

Visual Studio Code is the IDE that will be used to write code for PS1 games (in C++) made with the PSYQo development kit and PSXSplash are the [base project files](https://github.com/psxsplash/psxsplash) that you will need to place in your project folder. Ultimately, the end goal is that what you see in Unity is exactly what you will see on the PS1.

## Installation:
Get the following:
- Unity 6 (For setting up your PS1 scene)
- Visual Studio Code (For writing your C++ code)
- Nicolas Noble's PSX Dev Extension for PS1 Development (Which includes a panel for PS1 templates and tools to build your project)
- A real PS1 or emulator (PCSX-Redux, Duckstation, etc.) to test your project

As far as models are concerned, any modeling program will do as long as you can import the file into Unity (and subsequently exported to the ``SplashPack`` binary format).
FBX is a very common model file format used for skinned meshes with animations. I do hope to implement this myself (unless someone else beats me to it).

Rather than having to git clone this repository to your machine, you can install it directly from within Unity:
1. **Open Unity's Package Manager:**

	Go to `Window` -> `Package Manager`.

2. **Add Package From Git URL:**

	Click the + button in the upper left corner and select **"Add Package From Git URL"**.

Enter the Git URL for ``SplashEdit``: `https://github.com/psxsplash/splashedit.git`. Alternatively, if you want to install a different branch of ``SplashEdit``, such as ``lua``, you can use: `https://github.com/psxsplash/splashedit.git#lua"`.

For more information on using Git dependencies, you can take a look at [Unity's official documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/upm-git.html).

## Helpful Videos:

Both Nicolas and Bandwidth have videos on setting up the PSX Dev Extension in Visual Studio Code and setting up a PS1 scene in Unity respectively. Some of the information is repeated from the above but is here for completeness. Here are the following links to those videos:

[Setting up the PS1 Development Extension.](https://www.youtube.com/watch?v=KbAv-Ao7lzU)

[Setting up a Scene in Unity 6 for SplashEdit.](https://youtu.be/1JJFYptDTk0)

## PSX Components:

There are currently four custom Unity components for exporting your scenes to the PS1. These include:

**PSX Object Exporter**: Attach this to any GameObject that you want to be included in your scene. The GameObject must have a mesh component and a material applied to it and must have only a texture as colored polygons are not yet supported. Finally, you can select the bit depth for the texture in the component settings. Ensure that the texture applied to the material is marked as ``Read/Write``. To check this, click on your texture, look at the Inspector on the right, and under the **Advanced** section, you will find the box for it.

**PSX Scene Exporter**: Attach this to an empty GameObject. Click the **Export** button in the **PSX Scene Exporter**. You will be prompted to choose the output file location for ``SplashPack``. It would be ideal to place this in your Visual Studio Code project and not your Unity project, as it will be built using Nicolas's PS1 Dev Extension.

**PSX Player**: Attach this to any GameObject that you want to act as the player. For now, it acts as a FPS / Free Camera template. The analogue does work for it if you have the option specified in your choice emulator.

**PSX Nav Mesh**: Attach this to any GameObject that you want to apply collisions to. These collisions are rudimentary and currently only work in a top-down view. There must be at least one existing GameObject that uses the **PSX Player** component for this to work properly.

## Before Exporting:

Check the following:

**Player and Nav Meshes**: In case it wasn't clear already, a **PSX Player** component and **PSX Nav Mesh** component must coexist in the same scene for the player to walk on the collisions properly - as both rely on eachother to work.

**Scaling**: Ensure that your Geometry Transformation Engine (or GTE) scaling is set to a reasonable value, which can be found in your GameObject holding the component. Ideally, each object in your scene should be within the GTE bounds (or red wireframe bounding box). Bandwidth does mention that there is a scaling / overflow bug with Nav Meshes being worked on and the way to circumvent this is by scaling up the GTE value to be higher.

**Texture Requirements**:
- There is no automatic downscaling so you must scale your textures in advance.
- All textures must be in powers of two ``(64x64, 128x128, 256x256)`` with a maximum size of ``256x256`` being a full PS1 texture sheet.
- Remember to mark your texture in Unity as ``Read/Write`` or you will get an error upon exporting.

## Current Features:
**Automatic Scene Exporting**:\
Export your scene with one click using the **PSX Scene Exporter** component. This will include all gameobjects that use: **PSX Object Exporter**, **PSX Player**, and **PSX Nav Mesh** components respectively. Unity will ask you where to specify the ``output.bin`` file. As mentioned earlier, you will want to put it in your PSYQo project folder so it can be built into the PS1 executable.

**Texture Packing & Quantization**:\
Convert and preview your textures in a PSX-compatible format.

**Light Baking**:\
Prebakes any lights in the scene into the vertex colors of the nearest triangles in the exported mesh data.

**Nav Mesh Generation**:\
Generates a new Nav Mesh for FPS movement in your scene.

**FPS / Free Camera Template**:\
Allows you to walk around in your scene or view it from different perspectives. It's really just the way **PSX Player** and **PSX Nav Mesh** components are setup right now.

## Additional Features:
**VRAM Editor**:
- Access the VRAM Editor with Unity's **Window** context menu.
- Setup the framebuffer locations and preview texture packing.
- Click on **Save Settings** in the VRAM Editor to inform the GameObject with **PSX Scene Exporter** component where to pack the textures.
- When you click **Pack Textures** in the VRAM Editor, a file selection dialog will appear.
- Selecting a file will save only the VRAM data. If you do not wish to save the VRAM, simply close the dialog. This is more for debugging the layout of graphics. For a complete scene export (including VRAM), use the **PSX Scene Exporter** component attached to your GameObject that contains it.

**Quantized Texture Preview**:
- Preview how your textures will look after quantization before exporting. ``(4-bit, 8-bit, and 16-bit)``

## Features in The Works:
- Templates: Both for scripting scenarios and game types (first person, third person, etc.).
- Luascript: To script said scenarios and open doors for newcomers who don't have experience with C++ (take a look at the ``lua`` branch).

## How can I contribute?

1. Fork the repository.
2. Submit a pull request with your changes.

Please git clone the branch you want to edit to your machine if possible. We don't want to upload major changes to the ``main`` branch as this can cause potential problems in the near future. If you do have any major changes, please open an issue first to discuss your ideas.

Alternatively, you can join our Discord server ["PSX.Dev"](https://discord.gg/QByKPpH) and join Bandwidth's thread in **#showcase -> "Not Just Looks: Genuine PSX in Unity"**, and we can discuss them there. This is a fairly new project so we would love the help we can get!

Personally, I'm learning a lot myself about how Unity and PSYQo can be used to make PS1 games and it's a very excruciating yet exciting endeavor!
