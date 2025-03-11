using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using PSXSplash.RuntimeCode;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class VRAMEditorWindow : EditorWindow
{
    class ProhibitedArea
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;
    }

    private const int VramWidth = 1024;
    private const int VramHeight = 512;
    private List<ProhibitedArea> prohibitedAreas = new List<ProhibitedArea>();
    private Vector2 scrollPosition;
    private Texture2D vramImage;
    private Vector2 selectedResolution = new Vector2(320, 240);
    private bool dualBuffering = true;
    private bool verticalLayout = true;
    private Color bufferColor1 = new Color(1, 0, 0, 0.5f);
    private Color bufferColor2 = new Color(0, 1, 0, 0.5f);
    private Color prohibitedColor = new Color(1, 0, 0, 0.3f);

    private static readonly Vector2[] resolutions =
    {
        new Vector2(256, 240), new Vector2(256, 480),
        new Vector2(320, 240), new Vector2(320, 480),
        new Vector2(368, 240), new Vector2(368, 480),
        new Vector2(512, 240), new Vector2(512, 480),
        new Vector2(640, 240), new Vector2(640, 480)
    };

    [MenuItem("Window/VRAM Editor")]
    public static void ShowWindow()
    {
        GetWindow<VRAMEditorWindow>("VRAM Editor");
    }


    public static void PasteTexture(Texture2D baseTexture, Texture2D overlayTexture, int posX, int posY)
    {
        if (baseTexture == null || overlayTexture == null)
        {
            Debug.LogError("Textures cannot be null!");
            return;
        }

        // Get pixels from the overlay texture
        Color[] overlayPixels = overlayTexture.GetPixels();
        Color[] basePixels = baseTexture.GetPixels();

        int baseWidth = baseTexture.width;
        int baseHeight = baseTexture.height;
        int overlayWidth = overlayTexture.width;
        int overlayHeight = overlayTexture.height;

        // Loop through the overlay texture and paste it onto the base texture
        for (int y = 0; y < overlayHeight; y++)
        {
            for (int x = 0; x < overlayWidth; x++)
            {
                int baseX = posX + x;
                int baseY = posY + y;

                // Ensure we are within bounds of the base texture
                if (baseX >= 0 && baseX < baseWidth && baseY >= 0 && baseY < baseHeight)
                {
                    int baseIndex = baseY * baseWidth + baseX;
                    int overlayIndex = y * overlayWidth + x;

                    // Blend or replace pixel (simple overwrite in this case)
                    basePixels[baseIndex] = overlayPixels[overlayIndex];
                }
            }
        }

        // Apply the modified pixels back to the base texture
        baseTexture.SetPixels(basePixels);
        baseTexture.Apply();
    }

    private void PackTextures()
    {

        vramImage = new Texture2D(VramWidth, VramHeight);
        NativeArray<Color32> blackPixels = new NativeArray<Color32>(VramWidth * VramHeight, Allocator.Temp);
        vramImage.SetPixelData(blackPixels, 0);
        vramImage.Apply();
        blackPixels.Dispose();

        PSXObjectExporter[] objects = FindObjectsByType<PSXObjectExporter>(FindObjectsSortMode.None);
        foreach (PSXObjectExporter exp in objects)
        {
            exp.CreatePSXTexture2D();
        }

        List<Rect> dontPackAreas = new List<Rect>();
        foreach (ProhibitedArea area in prohibitedAreas)
        {
            dontPackAreas.Add(new Rect(area.X, area.Y, area.Width, area.Height));
        }

        Rect buffer1 = new Rect(0, 0, selectedResolution.x, selectedResolution.y);
        Rect buffer2 = verticalLayout ? new Rect(0, 256, selectedResolution.x, selectedResolution.y)
                                      : new Rect(selectedResolution.x, 0, selectedResolution.x, selectedResolution.y);


        dontPackAreas.Add(buffer1);
        dontPackAreas.Add(buffer2);

        VRAMPacker tp = new VRAMPacker(dontPackAreas);
        var packed = tp.PackTexturesIntoVRAM(objects);

        foreach (TextureAtlas ta in packed.atlases)
        {
            foreach (PSXTexture2D texture in ta.ContainedTextures)
            {
                Debug.Log($"Packing {texture} at: x:{ta.PositionX + texture.PackingX} y:{ta.PositionY + texture.PackingY}");
                PasteTexture(vramImage, texture.GenerateVramPreview(), ta.PositionX + texture.PackingX, ta.PositionY + texture.PackingY);
                Debug.Log($"Texpage: {texture.TexpageNum} Offset:({texture.PackingX},{texture.PackingY})");

                for(int i = 0; i < texture.ColorPalette.Count; i++) {
                    vramImage.SetPixel(texture.ClutPackingX+i, texture.ClutPackingY, texture.ColorPalette[i].GetUnityColor());
                }
                vramImage.Apply();
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label("VRAM Editor", EditorStyles.boldLabel);

        selectedResolution = resolutions[EditorGUILayout.Popup("Resolution", System.Array.IndexOf(resolutions, selectedResolution),
            new string[] { "256x240", "256x480", "320x240", "320x480", "368x240", "368x480", "512x240", "512x480", "640x240", "640x480" })];

        bool canDBHorizontal = selectedResolution[0] * 2 <= 1024;
        bool canDBVertical = selectedResolution[1] * 2 <= 512;

        if (canDBHorizontal || canDBVertical)
        {
            dualBuffering = EditorGUILayout.Toggle("Dual Buffering", dualBuffering);
        }
        else { dualBuffering = false; }

        if (canDBVertical && canDBHorizontal)
        {
            verticalLayout = EditorGUILayout.Toggle("Vertical", verticalLayout);
        }
        else if (canDBVertical) { verticalLayout = true; }
        else
        {
            verticalLayout = false;
        }

        GUILayout.Space(10);
        GUILayout.Label("Prohibited areas", EditorStyles.boldLabel);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.MaxHeight(150f));

        for (int i = 0; i < prohibitedAreas.Count; i++)
        {
            var area = prohibitedAreas[i];

            area.X = EditorGUILayout.IntField("X", area.X);
            area.Y = EditorGUILayout.IntField("Y", area.Y);
            area.Width = EditorGUILayout.IntField("Width", area.Width);
            area.Height = EditorGUILayout.IntField("Height", area.Height);

            if (GUILayout.Button("Remove"))
            {
                prohibitedAreas.RemoveAt(i);
                break; // Avoid out-of-bounds errors after removal
            }

            prohibitedAreas[i] = area; // Update the list with edited values
            GUILayout.Space(10);
        }

        GUILayout.EndScrollView();
        GUILayout.Space(10);
        if (GUILayout.Button("Add Prohibited Area"))
        {
            prohibitedAreas.Add(new ProhibitedArea());
        }

        // New "Pack Textures" Button
        if (GUILayout.Button("Pack Textures"))
        {
            PackTextures();
        }

        GUILayout.EndVertical();

        Rect vramRect = GUILayoutUtility.GetRect(VramWidth, VramHeight, GUILayout.ExpandWidth(false));
        EditorGUI.DrawPreviewTexture(vramRect, vramImage, null, ScaleMode.ScaleToFit, 0, 0, ColorWriteMask.All);

        Rect buffer1 = new Rect(vramRect.x, vramRect.y, selectedResolution.x, selectedResolution.y);
        Rect buffer2 = verticalLayout ? new Rect(vramRect.x, 256, selectedResolution.x, selectedResolution.y)
                                      : new Rect(vramRect.x + selectedResolution.x, vramRect.y, selectedResolution.x, selectedResolution.y);

        EditorGUI.DrawRect(buffer1, bufferColor1);
        GUI.Label(new Rect(buffer1.center.x - 40, buffer1.center.y - 10, 80, 20), "Framebuffer A", EditorStyles.boldLabel);
        GUILayout.Space(10);
        if (dualBuffering)
        {
            EditorGUI.DrawRect(buffer2, bufferColor2);
            GUI.Label(new Rect(buffer2.center.x - 40, buffer2.center.y - 10, 80, 20), "Framebuffer B", EditorStyles.boldLabel);
        }

        foreach (ProhibitedArea area in prohibitedAreas)
        {
            Rect areaRect = new Rect(vramRect.x + area.X, vramRect.y + area.Y, area.Width, area.Height);
            EditorGUI.DrawRect(areaRect, prohibitedColor);
        }

        GUILayout.EndHorizontal();
    }
}