using System.Collections.Generic;
using System.IO;
using PSXSplash.RuntimeCode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class QuantizedPreviewWindow : EditorWindow
{
    private Texture2D originalTexture;
    private Texture2D quantizedTexture;
    private Texture2D vramTexture; // New VRAM Texture
    private List<VRAMPixel> clut; // Changed to 1D array
    private ushort[] indexedPixelData; // New field for indexed pixel data
    private PSXBPP bpp;
    private readonly int previewSize = 256;

    [MenuItem("Window/Quantized Preview")]
    public static void ShowWindow()
    {
        QuantizedPreviewWindow win = GetWindow<QuantizedPreviewWindow>("Quantized Preview");
        win.minSize = new Vector2(800, 700);
    }

    private void OnGUI()
    {
        GUILayout.Label("Quantized Preview", EditorStyles.boldLabel);

        originalTexture = (Texture2D)EditorGUILayout.ObjectField("Original Texture", originalTexture, typeof(Texture2D), false);


        bpp = (PSXBPP)EditorGUILayout.EnumPopup("Bit Depth", bpp);


        if (GUILayout.Button("Generate Quantized Preview") && originalTexture != null)
        {
            GenerateQuantizedPreview();
        }

        GUILayout.BeginHorizontal();

        if (originalTexture != null)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Original Texture");
            DrawTexturePreview(originalTexture, previewSize, false);
            GUILayout.EndVertical();
        }

        if (vramTexture != null)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("VRAM View (Indexed Data as 16bpp)");
            DrawTexturePreview(vramTexture, previewSize);
            GUILayout.EndVertical();
        }

        if (quantizedTexture != null)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Quantized Texture");
            DrawTexturePreview(quantizedTexture, previewSize);
            GUILayout.EndVertical();
        }

        GUILayout.EndHorizontal();

        if (clut != null)
        {
            GUILayout.Label("Color Lookup Table (CLUT)");
            DrawCLUT();
        }

        GUILayout.Space(10);

        if (indexedPixelData != null)
        {
            if (GUILayout.Button("Export texute data"))
            {
                string path = EditorUtility.SaveFilePanel(
                    "Save texture data",
                    "",
                    "pixel_data",
                    "bin"
                );

                if (!string.IsNullOrEmpty(path))
                {
                    using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    using (BinaryWriter writer = new BinaryWriter(fileStream))
                    {
                        foreach (ushort value in indexedPixelData)
                        {
                            writer.Write(value);
                        }
                    }
                }
            }
        }

        if (clut != null)
        {
            if (GUILayout.Button("Export clut data"))
            {
                string path = EditorUtility.SaveFilePanel(
                    "Save clut data",
                    "",
                    "clut_data",
                    "bin"
                );

                if (!string.IsNullOrEmpty(path))
                {
                    using (FileStream fileStream = new FileStream(path, FileMode.Create, FileAccess.Write))
                    using (BinaryWriter writer = new BinaryWriter(fileStream))
                    {
                        foreach (VRAMPixel value in clut)
                        {
                            writer.Write(value.Pack());
                        }
                    }
                }
            }
        }
    }

    private void GenerateQuantizedPreview()
    {

        PSXTexture2D psxTex = PSXTexture2D.CreateFromTexture2D(originalTexture, bpp);

        quantizedTexture = psxTex.GeneratePreview();
        vramTexture = psxTex.GenerateVramPreview();
        clut = psxTex.ColorPalette;

    }





    private void DrawTexturePreview(Texture2D texture, int size, bool flipY = true)
    {
        Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.ExpandWidth(false));
        EditorGUI.DrawPreviewTexture(rect, texture, null, ScaleMode.ScaleToFit, 0, 0, ColorWriteMask.All);
    }


    private void DrawCLUT()
    {
        if (clut == null) return;

        int swatchSize = 20;
        int maxColorsPerRow = 40;

        GUILayout.Space(10);

        int totalColors = clut.Count;
        int totalRows = Mathf.CeilToInt((float)totalColors / maxColorsPerRow);

        for (int row = 0; row < totalRows; row++)
        {
            GUILayout.BeginHorizontal();

            int colorsInRow = Mathf.Min(maxColorsPerRow, totalColors - row * maxColorsPerRow);

            for (int col = 0; col < colorsInRow; col++)
            {
                int index = row * maxColorsPerRow + col;

                Vector3 color = new Vector3(
                    clut[index].R / 31.0f,  // Red: bits 0–4
                    clut[index].G / 31.0f, // Green: bits 5–9
                    clut[index].B / 31.0f // Blue: bits 10–14
                );

                Rect rect = GUILayoutUtility.GetRect(swatchSize, swatchSize, GUILayout.ExpandWidth(false));
                EditorGUI.DrawRect(rect, new Color(color.x, color.y, color.z));
            }

            GUILayout.EndHorizontal();
        }
    }

}
