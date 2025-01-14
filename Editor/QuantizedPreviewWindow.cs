using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class QuantizedPreviewWindow : EditorWindow
{
    private Texture2D originalTexture;
    private Texture2D quantizedTexture;
    private Texture2D vramTexture; // New VRAM Texture
    private float[] clut; // Changed to 1D array
    private ushort[] indexedPixelData; // New field for indexed pixel data
    private int bpp = 4;
    private int targetWidth = 128;
    private int targetHeight = 128;
    private int maxKMeans = 100;
    private int previewSize = 256;

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

        targetWidth = EditorGUILayout.IntField("Target Width", targetWidth);
        targetHeight = EditorGUILayout.IntField("Target Height", targetHeight);

        bpp = EditorGUILayout.IntPopup("Bits Per Pixel", bpp, new[] { "4 bpp", "8 bpp", "16 bpp" }, new[] { 4, 8, 16 });
        maxKMeans = EditorGUILayout.IntField("Max K-Means", maxKMeans);

        if (GUILayout.Button("Generate Quantized Preview") && originalTexture != null)
        {
            GenerateQuantizedPreview();
        }

        GUILayout.BeginHorizontal();

        if (originalTexture != null)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Original Texture");
            DrawTexturePreview(originalTexture, previewSize);
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
    }

    private void GenerateQuantizedPreview()
    {
        Texture2D resizedTexture = ResizeTexture(originalTexture, targetWidth, targetHeight);

        if (bpp == 16) 
        {
            quantizedTexture = ConvertTo16Bpp(resizedTexture);
            clut = null; 
            vramTexture = resizedTexture;
        }
        else
        {
            var (indexedPixels, generatedClut) = ImageQuantizer.Quantize(resizedTexture, bpp, maxKMeans);

            indexedPixelData = indexedPixels; 
            clut = generatedClut;

            int pixelSize = bpp == 4 ? 4 : bpp == 8 ? 2 : 1; 
            quantizedTexture = new Texture2D(resizedTexture.width, resizedTexture.height);
            Color[] quantizedColors = new Color[resizedTexture.width * resizedTexture.height];

            int pixelIndex = 0;
            for (int y = 0; y < resizedTexture.height; y++)
            {
                for (int x = 0; x < resizedTexture.width; x++)
                {
                    int index = 0;

                    if (pixelSize == 4)
                    {
                        int packedValue = indexedPixelData[pixelIndex];
                        index = (packedValue >> ((x % 4) * 4)) & 0xF;
                    }
                    else if (pixelSize == 2)
                    {
                        int packedValue = indexedPixelData[pixelIndex];
                        index = (packedValue >> ((x % 2) * 8)) & 0xFF; 
                    }
                    else
                    {
                        
                        index = indexedPixelData[pixelIndex];
                    }

                    
                    Vector3 color = new Vector3(clut[index * 3], clut[index * 3 + 1], clut[index * 3 + 2]);
                    quantizedColors[y * resizedTexture.width + x] = new Color(color.x, color.y, color.z);

                    if ((x % pixelSize) == (pixelSize - 1))
                    {
                        pixelIndex++;
                    }
                }
            }

            quantizedTexture.SetPixels(quantizedColors);
            quantizedTexture.Apply();

            vramTexture = CreateVramTexture(resizedTexture.width, resizedTexture.height, indexedPixelData);
        }
    }




    private Texture2D CreateVramTexture(int width, int height, ushort[] indexedData)
    {
        int adjustedWidth = width;

        if (bpp == 4)
        {
            adjustedWidth = Mathf.CeilToInt(width / 4f); 
        }
        else if (bpp == 8)
        {
            adjustedWidth = Mathf.CeilToInt(width / 2f); 
        }

        Texture2D vramTexture = new Texture2D(adjustedWidth, height);

        Color[] vramColors = new Color[adjustedWidth * height];

        for (int i = 0; i < indexedData.Length; i++)
        {
            int index = indexedData[i];

            float r = Mathf.Floor((index >> 11) & 31) / 31.0f; 
            float g = Mathf.Floor((index >> 5) & 31) / 31.0f; 
            float b = Mathf.Floor(index & 31) / 31.0f;

            vramColors[i] = new Color(r, g, b);
        }

        vramTexture.SetPixels(vramColors);
        vramTexture.Apply();

        return vramTexture;
    }


    private Texture2D ConvertTo16Bpp(Texture2D source)
    {
        int width = source.width;
        int height = source.height;
        Texture2D convertedTexture = new Texture2D(width, height);

        Color[] originalPixels = source.GetPixels();
        Color[] convertedPixels = new Color[originalPixels.Length];

        for (int i = 0; i < originalPixels.Length; i++)
        {
            Color pixel = originalPixels[i];

            // Convert to 5 bits per channel (R5G5B5)
            float r = Mathf.Floor(pixel.r * 31) / 31.0f; // 5 bits for red
            float g = Mathf.Floor(pixel.g * 31) / 31.0f; // 5 bits for green
            float b = Mathf.Floor(pixel.b * 31) / 31.0f; // 5 bits for blue

            convertedPixels[i] = new Color(r, g, b, pixel.a); 
        }

        convertedTexture.SetPixels(convertedPixels);
        convertedTexture.Apply();

        return convertedTexture;
    }

    private void DrawTexturePreview(Texture2D texture, int size)
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

        int totalColors = clut.Length / 3;
        int totalRows = Mathf.CeilToInt((float)totalColors / maxColorsPerRow);

        for (int row = 0; row < totalRows; row++)
        {
            GUILayout.BeginHorizontal();

            int colorsInRow = Mathf.Min(maxColorsPerRow, totalColors - row * maxColorsPerRow);

            for (int col = 0; col < colorsInRow; col++)
            {
                int index = row * maxColorsPerRow + col;
                Vector3 color = new Vector3(clut[index * 3], clut[index * 3 + 1], clut[index * 3 + 2]);
                Rect rect = GUILayoutUtility.GetRect(swatchSize, swatchSize, GUILayout.ExpandWidth(false));
                EditorGUI.DrawRect(rect, new Color(color.x, color.y, color.z));
            }

            GUILayout.EndHorizontal();
        }
    }

    private Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
    {
        RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
        rt.antiAliasing = 1;
        Graphics.Blit(source, rt);

        Texture2D resizedTexture = new Texture2D(newWidth, newHeight);
        RenderTexture.active = rt;
        resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        resizedTexture.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);

        return resizedTexture;
    }
}
