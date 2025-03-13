using System.Collections.Generic;
using System.IO;
using PSXSplash.RuntimeCode;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class VRAMEditorWindow : EditorWindow
{

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

    private static string _psxDataPath = "Assets/PSXData.asset";
    private PSXData _psxData;

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

    private void OnEnable()
    {
        vramImage = new Texture2D(VramWidth, VramHeight);
        NativeArray<Color32> blackPixels = new NativeArray<Color32>(VramWidth * VramHeight, Allocator.Temp);
        vramImage.SetPixelData(blackPixels, 0);
        vramImage.Apply();
        blackPixels.Dispose();

        LoadData();
    }

    public static void PasteTexture(Texture2D baseTexture, Texture2D overlayTexture, int posX, int posY)
    {
        if (baseTexture == null || overlayTexture == null)
        {
            Debug.LogError("Textures cannot be null!");
            return;
        }

        Color[] overlayPixels = overlayTexture.GetPixels();
        Color[] basePixels = baseTexture.GetPixels();

        int baseWidth = baseTexture.width;
        int baseHeight = baseTexture.height;
        int overlayWidth = overlayTexture.width;
        int overlayHeight = overlayTexture.height;

        for (int y = 0; y < overlayHeight; y++)
        {
            for (int x = 0; x < overlayWidth; x++)
            {
                int baseX = posX + x;
                int baseY = posY + y;
                if (baseX >= 0 && baseX < baseWidth && baseY >= 0 && baseY < baseHeight)
                {
                    int baseIndex = baseY * baseWidth + baseX;
                    int overlayIndex = y * overlayWidth + x;

                    basePixels[baseIndex] = overlayPixels[overlayIndex];
                }
            }
        }

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

        Rect buffer1 = new Rect(0, 0, selectedResolution.x, selectedResolution.y);
        Rect buffer2 = verticalLayout ? new Rect(0, 256, selectedResolution.x, selectedResolution.y)
                                      : new Rect(selectedResolution.x, 0, selectedResolution.x, selectedResolution.y);


        List<Rect> framebuffers = new List<Rect> { buffer1 };
        if (dualBuffering)
        {
            framebuffers.Add(buffer2);
        }

        VRAMPacker tp = new VRAMPacker(framebuffers, prohibitedAreas);
        var packed = tp.PackTexturesIntoVRAM(objects);


        for (int y = 0; y < VramHeight; y++)
        {
            for (int x = 0; x < VramWidth; x++)
            {
                vramImage.SetPixel(x, VramHeight - y - 1, packed._vramPixels[x, y].GetUnityColor());
            }
        }
        vramImage.Apply();

        string path = EditorUtility.SaveFilePanel("Select Output File", "", "output", "bin");

        using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
        {
            for (int y = 0; y < VramHeight; y++)
            {
                for (int x = 0; x < VramWidth; x++)
                {
                    writer.Write(packed._vramPixels[x, y].Pack());
                }
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
                break;
            }

            prohibitedAreas[i] = area;
            GUILayout.Space(10);
        }

        GUILayout.EndScrollView();
        GUILayout.Space(10);
        if (GUILayout.Button("Add Prohibited Area"))
        {
            prohibitedAreas.Add(new ProhibitedArea());
        }

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
        GUI.Label(new Rect(buffer1.center.x - 40, buffer1.center.y - 10, 120, 20), "Framebuffer A", EditorStyles.boldLabel);
        GUILayout.Space(10);
        if (dualBuffering)
        {
            EditorGUI.DrawRect(buffer2, bufferColor2);
            GUI.Label(new Rect(buffer2.center.x - 40, buffer2.center.y - 10, 120, 20), "Framebuffer B", EditorStyles.boldLabel);
        }

        foreach (ProhibitedArea area in prohibitedAreas)
        {
            Rect areaRect = new Rect(vramRect.x + area.X, vramRect.y + area.Y, area.Width, area.Height);
            EditorGUI.DrawRect(areaRect, prohibitedColor);
        }

        GUILayout.EndHorizontal();
        StoreData();
    }

    private void LoadData()
    {
        _psxData = AssetDatabase.LoadAssetAtPath<PSXData>(_psxDataPath);

        if (!_psxData)
        {
            _psxData = CreateInstance<PSXData>();
            AssetDatabase.CreateAsset(_psxData, _psxDataPath);
            AssetDatabase.SaveAssets();
        }

        selectedResolution = _psxData.OutputResolution;
        dualBuffering = _psxData.DualBuffering;
        verticalLayout = _psxData.VerticalBuffering;
        prohibitedAreas = _psxData.ProhibitedAreas;
    }

    private void StoreData()
    {
        if (_psxData != null)
        {
            _psxData.OutputResolution = selectedResolution;
            _psxData.DualBuffering = dualBuffering;
            _psxData.VerticalBuffering = verticalLayout;
            _psxData.ProhibitedAreas = prohibitedAreas;

            EditorUtility.SetDirty(_psxData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}