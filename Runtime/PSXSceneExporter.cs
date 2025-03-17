using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SplashEdit.RuntimeCode
{

  [ExecuteInEditMode]
  public class PSXSceneExporter : MonoBehaviour
  {
    private PSXObjectExporter[] _exporters;

    private PSXData _psxData;
    private readonly string _psxDataPath = "Assets/PSXData.asset";

    private Vector2 selectedResolution;
    private bool dualBuffering;
    private bool verticalLayout;
    private List<ProhibitedArea> prohibitedAreas;
    private VRAMPixel[,] vramPixels;



    public void Export()
    {
      LoadData();
      _exporters = FindObjectsByType<PSXObjectExporter>(FindObjectsSortMode.None);
      foreach (PSXObjectExporter exp in _exporters)
      {
        exp.CreatePSXTexture2D();
        exp.CreatePSXMesh();
      }
      PackTextures();
      ExportFile();
    }

    void PackTextures()
    {

      Rect buffer1 = new Rect(0, 0, selectedResolution.x, selectedResolution.y);
      Rect buffer2 = verticalLayout ? new Rect(0, 256, selectedResolution.x, selectedResolution.y)
                                    : new Rect(selectedResolution.x, 0, selectedResolution.x, selectedResolution.y);

      List<Rect> framebuffers = new List<Rect> { buffer1 };
      if (dualBuffering)
      {
        framebuffers.Add(buffer2);
      }

      VRAMPacker tp = new VRAMPacker(framebuffers, prohibitedAreas);
      var packed = tp.PackTexturesIntoVRAM(_exporters);
      _exporters = packed.processedObjects;
      vramPixels = packed._vramPixels;

    }

    void ExportFile()
    {
      string path = EditorUtility.SaveFilePanel("Select Output File", "", "output", "bin");
      int totalFaces = 0;
      using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
      {
        // VramPixels are always 1MB
        for (int y = 0; y < vramPixels.GetLength(1); y++)
        {
          for (int x = 0; x < vramPixels.GetLength(0); x++)
          {
            writer.Write(vramPixels[x, y].Pack());
          }
        }
        writer.Write((ushort)_exporters.Length);
        foreach (PSXObjectExporter exporter in _exporters)
        {

          int expander = 16 / ((int)exporter.Texture.BitDepth);

          totalFaces += exporter.Mesh.Triangles.Count;
          writer.Write((ushort)exporter.Mesh.Triangles.Count);
          writer.Write((byte)exporter.Texture.BitDepth);
          writer.Write((byte)exporter.Texture.TexpageX);
          writer.Write((byte)exporter.Texture.TexpageY);
          writer.Write((ushort)exporter.Texture.ClutPackingX);
          writer.Write((ushort)exporter.Texture.ClutPackingY);
          writer.Write((byte)0);
          foreach (Tri tri in exporter.Mesh.Triangles)
          {
            writer.Write((short)tri.v0.vx);
            writer.Write((short)tri.v0.vy);
            writer.Write((short)tri.v0.vz);
            writer.Write((short)tri.v0.nx);
            writer.Write((short)tri.v0.ny);
            writer.Write((short)tri.v0.nz);
            writer.Write((byte)(tri.v0.u + exporter.Texture.PackingX * expander));
            writer.Write((byte)(tri.v0.v + exporter.Texture.PackingY));
            writer.Write((byte) tri.v0.r);
            writer.Write((byte) tri.v0.g);
            writer.Write((byte) tri.v0.b);
            for(int i = 0; i < 7; i ++) writer.Write((byte) 0);

            writer.Write((short)tri.v1.vx);
            writer.Write((short)tri.v1.vy);
            writer.Write((short)tri.v1.vz);
            writer.Write((short)tri.v1.nx);
            writer.Write((short)tri.v1.ny);
            writer.Write((short)tri.v1.nz);
            writer.Write((byte)(tri.v1.u + exporter.Texture.PackingX * expander));
            writer.Write((byte)(tri.v1.v + exporter.Texture.PackingY));
            writer.Write((byte) tri.v1.r);
            writer.Write((byte) tri.v1.g);
            writer.Write((byte) tri.v1.b);
            for(int i = 0; i < 7; i ++) writer.Write((byte) 0);

            writer.Write((short)tri.v2.vx);
            writer.Write((short)tri.v2.vy);
            writer.Write((short)tri.v2.vz);
            writer.Write((short)tri.v2.nx);
            writer.Write((short)tri.v2.ny);
            writer.Write((short)tri.v2.nz);
            writer.Write((byte)(tri.v2.u + exporter.Texture.PackingX * expander));
            writer.Write((byte)(tri.v2.v + exporter.Texture.PackingY));
            writer.Write((byte) tri.v2.r);
            writer.Write((byte) tri.v2.g);
            writer.Write((byte) tri.v2.b);
            for(int i = 0; i < 7; i ++) writer.Write((byte) 0);

          }
        }
      }
      Debug.Log(totalFaces);
    }

    public void LoadData()
    {
      _psxData = AssetDatabase.LoadAssetAtPath<PSXData>(_psxDataPath);

      if (!_psxData)
      {
        _psxData = ScriptableObject.CreateInstance<PSXData>();
        AssetDatabase.CreateAsset(_psxData, _psxDataPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
      }

      selectedResolution = _psxData.OutputResolution;
      dualBuffering = _psxData.DualBuffering;
      verticalLayout = _psxData.VerticalBuffering;
      prohibitedAreas = _psxData.ProhibitedAreas;
    }

    void OnDrawGizmos()
    {
      Gizmos.DrawIcon(transform.position, "Packages/net.psxsplash.splashedit/Icons/PSXSceneExporter.png", true);
    }
  }
}
