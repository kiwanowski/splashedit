using UnityEditor;
using UnityEngine;
using PSXSplash.RuntimeCode;
using UnityEngine.Rendering;
using System.Threading.Tasks;
using UnityEditor.PackageManager.UI;

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
        QuantizedPreviewWindow win = GetWindow<QuantizedPreviewWindow>("Quantized Preview");
        win.minSize = new Vector2(800, 700);
    }

    private void OnGUI()
    {
        GUILayout.Label("Quantized Preview", EditorStyles.boldLabel);

        originalTexture = (Texture2D)EditorGUILayout.ObjectField("Original Texture", originalTexture, typeof(Texture2D), false);

        targetWidth = EditorGUILayout.IntField("Target Width", targetWidth);
        targetHeight = EditorGUILayout.IntField("Target Height", targetHeight);

        bpp = EditorGUILayout.IntPopup("Bits Per Pixel", bpp, new[] { "4 bpp", "8 bpp", "15 bpp" }, new[] { 4, 8, 15 });

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

        if (resizedTexture != null)
        {
            GUILayout.BeginVertical();
            GUILayout.Label("Resized Texture");
            DrawTexturePreview(resizedTexture, previewSize);
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
        resizedTexture = ResizeTexture(originalTexture, targetWidth, targetHeight);

        if (bpp == 15) // Handle 15bpp (R5G5B5) without CLUT
        {
            quantizedTexture = ConvertTo15Bpp(resizedTexture);
            clut = null; // No CLUT for 15bpp
        }
        else
        {
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
    }

    private Texture2D ConvertTo15Bpp(Texture2D source)
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

            convertedPixels[i] = new Color(r, g, b, pixel.a); // Maintain alpha channel
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
