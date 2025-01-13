using UnityEditor;
using UnityEngine;
using PSXSplash.RuntimeCode;
using UnityEngine.Rendering;

public class QuantizedPreviewWindow : EditorWindow
{
    private Texture2D originalTexture;
    private Texture2D resizedTexture;
    private Texture2D quantizedTexture;
    private Vector3[,] clut;
    private int bpp = 4;
    private int targetWidth = 128;
    private int targetHeight = 128;
    private int previewSize = 256;

    [MenuItem("Window/Quantized Preview")]
    public static void ShowWindow()
    {
        GetWindow<QuantizedPreviewWindow>("Quantized Preview");
    }

    private void OnGUI()
    {
        GUILayout.Label("Quantized Preview", EditorStyles.boldLabel);

        originalTexture = (Texture2D)EditorGUILayout.ObjectField("Original Texture", originalTexture, typeof(Texture2D), false);

        targetWidth = EditorGUILayout.IntField("Target Width", targetWidth);
        targetHeight = EditorGUILayout.IntField("Target Height", targetHeight);

        bpp = EditorGUILayout.IntPopup("Bits Per Pixel", bpp, new[] { "4 bpp", "8 bpp" }, new[] { 4, 8 });

        if (GUILayout.Button("Generate Quantized Preview") && originalTexture != null)
        {
            GenerateQuantizedPreview();
        }

        if (originalTexture != null)
        {
            GUILayout.Label("Original Texture");
            DrawTexturePreview(originalTexture, previewSize);
        }

        if (resizedTexture != null)
        {
            GUILayout.Label("Resized Texture");
            DrawTexturePreview(resizedTexture, previewSize);
        }

        if (quantizedTexture != null)
        {
            GUILayout.Label("Quantized Texture");
            DrawTexturePreview(quantizedTexture, previewSize);
        }

        if (clut != null)
        {
            GUILayout.Label("Color Lookup Table (CLUT)");
            DrawCLUT();
        }
    }

    private void GenerateQuantizedPreview()
    {
        resizedTexture = ResizeTexture(originalTexture, targetWidth, targetHeight);

        int maxColors = (int)Mathf.Pow(2, bpp);

        var (quantizedPixels, generatedClut) = ImageQuantizer.Quantize(resizedTexture, maxColors);

        quantizedTexture = new Texture2D(resizedTexture.width, resizedTexture.height);
        Color[] quantizedColors = new Color[resizedTexture.width * resizedTexture.height];

        for (int y = 0; y < resizedTexture.height; y++)
        {
            for (int x = 0; x < resizedTexture.width; x++)
            {
                quantizedColors[y * resizedTexture.width + x] = new Color(
                    quantizedPixels[x, y, 0],
                    quantizedPixels[x, y, 1],
                    quantizedPixels[x, y, 2]
                );
            }
        }

        quantizedTexture.SetPixels(quantizedColors);
        quantizedTexture.Apply();

        clut = generatedClut;
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

        int totalColors = clut.GetLength(0);
        int totalRows = Mathf.CeilToInt((float)totalColors / maxColorsPerRow);

        for (int row = 0; row < totalRows; row++)
        {
            GUILayout.BeginHorizontal();

            int colorsInRow = Mathf.Min(maxColorsPerRow, totalColors - row * maxColorsPerRow);

            for (int col = 0; col < colorsInRow; col++)
            {
                int index = row * maxColorsPerRow + col;
                Vector3 color = clut[index, 0];
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
