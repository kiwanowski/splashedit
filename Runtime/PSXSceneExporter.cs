using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SplashEdit.RuntimeCode
{

  [ExecuteInEditMode]
  public class PSXSceneExporter : MonoBehaviour
  {

    public float GTEScaling = 100.0f;

    private PSXObjectExporter[] _exporters;
    private TextureAtlas[] _atlases;

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
        exp.CreatePSXMesh(GTEScaling);
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
      _atlases = packed.atlases;

    }

    void ExportFile()
    {
      string path = EditorUtility.SaveFilePanel("Select Output File", "", "output", "bin");
      int totalFaces = 0;

      // Lists for mesh data offsets.
      List<long> offsetPlaceholderPositions = new List<long>();
      List<long> meshDataOffsets = new List<long>();

      // Lists for atlas data offsets.
      List<long> atlasOffsetPlaceholderPositions = new List<long>();
      List<long> atlasDataOffsets = new List<long>();


      using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
      {
        // Header
        writer.Write('S');
        writer.Write('P');
        writer.Write((ushort)1);
        writer.Write((ushort)_exporters.Length);
        writer.Write((ushort)_atlases.Length);
        // Start of Metadata section

        // GameObject section (exporters)
        foreach (PSXObjectExporter exporter in _exporters)
        {
          // Write object's position
          writer.Write((int)PSXTrig.ConvertCoordinateToPSX(transform.position.x));
          writer.Write((int)PSXTrig.ConvertCoordinateToPSX(transform.position.y));
          writer.Write((int)PSXTrig.ConvertCoordinateToPSX(transform.position.z));

          int[,] rotationMatrix = PSXTrig.ConvertRotationToPSXMatrix(exporter.transform.rotation);
          writer.Write((int)rotationMatrix[0, 0]);
          writer.Write((int)rotationMatrix[0, 1]);
          writer.Write((int)rotationMatrix[0, 2]);
          writer.Write((int)rotationMatrix[1, 0]);
          writer.Write((int)rotationMatrix[1, 1]);
          writer.Write((int)rotationMatrix[1, 2]);
          writer.Write((int)rotationMatrix[2, 0]);
          writer.Write((int)rotationMatrix[2, 1]);
          writer.Write((int)rotationMatrix[2, 2]);

          writer.Write((ushort)exporter.Mesh.Triangles.Count);

          // Set up texture page attributes
          TPageAttr tpage = new TPageAttr();
          tpage.SetPageX(exporter.Texture.TexpageX);
          tpage.SetPageY(exporter.Texture.TexpageY);
          switch (exporter.Texture.BitDepth)
          {
            case PSXBPP.TEX_4BIT:
              tpage.Set(TPageAttr.ColorMode.Mode4Bit);
              break;
            case PSXBPP.TEX_8BIT:
              tpage.Set(TPageAttr.ColorMode.Mode8Bit);
              break;
            case PSXBPP.TEX_16BIT:
              tpage.Set(TPageAttr.ColorMode.Mode16Bit);
              break;
          }
          tpage.SetDithering(true);
          writer.Write((ushort)tpage.info);
          writer.Write((ushort)exporter.Texture.ClutPackingX);
          writer.Write((ushort)exporter.Texture.ClutPackingY);
          if (exporter.Texture.BitDepth != PSXBPP.TEX_16BIT)
          {
            foreach (VRAMPixel color in exporter.Texture.ColorPalette)
            {
              writer.Write((ushort)color.Pack());
            }
            for (int i = exporter.Texture.ColorPalette.Count; i < 256; i++)
            {
              writer.Write((ushort)0);
            }
          }
          else
          {
            for (int i = 0; i < 256; i++)
            {
              writer.Write((ushort)0);
            }
          }


          // Write placeholder for mesh data offset and record its position.
          offsetPlaceholderPositions.Add(writer.BaseStream.Position);
          writer.Write((int)0); // 4-byte placeholder for mesh data offset.
        }

        // Atlas metadata section
        foreach (TextureAtlas atlas in _atlases)
        {
          // Write placeholder for texture atlas raw data offset.
          atlasOffsetPlaceholderPositions.Add(writer.BaseStream.Position);
          writer.Write((int)0); // 4-byte placeholder for atlas data offset.

          writer.Write((ushort)atlas.Width);
          writer.Write((ushort)TextureAtlas.Height);
          writer.Write((ushort)atlas.PositionX);
          writer.Write((ushort)atlas.PositionY);
        }

        // Start of data section

        // Mesh data section: Write mesh data for each exporter.
        foreach (PSXObjectExporter exporter in _exporters)
        {
          // Record the current offset for this exporter's mesh data.
          long meshDataOffset = writer.BaseStream.Position;
          meshDataOffsets.Add(meshDataOffset);

          totalFaces += exporter.Mesh.Triangles.Count;

          int expander = 16 / ((int)exporter.Texture.BitDepth);
          foreach (Tri tri in exporter.Mesh.Triangles)
          {
            // Write vertices coordinates
            writer.Write((short)tri.v0.vx);
            writer.Write((short)tri.v0.vy);
            writer.Write((short)tri.v0.vz);

            writer.Write((short)tri.v1.vx);
            writer.Write((short)tri.v1.vy);
            writer.Write((short)tri.v1.vz);

            writer.Write((short)tri.v2.vx);
            writer.Write((short)tri.v2.vy);
            writer.Write((short)tri.v2.vz);

            // Write vertex normals for v0 only
            writer.Write((short)tri.v0.nx);
            writer.Write((short)tri.v0.ny);
            writer.Write((short)tri.v0.nz);

            // Write UVs for each vertex, adjusting for texture packing
            writer.Write((byte)(tri.v0.u + exporter.Texture.PackingX * expander));
            writer.Write((byte)(tri.v0.v + exporter.Texture.PackingY));

            writer.Write((byte)(tri.v1.u + exporter.Texture.PackingX * expander));
            writer.Write((byte)(tri.v1.v + exporter.Texture.PackingY));

            writer.Write((byte)(tri.v2.u + exporter.Texture.PackingX * expander));
            writer.Write((byte)(tri.v2.v + exporter.Texture.PackingY));

            writer.Write((ushort)0); // padding

            // Write vertex colors with padding
            writer.Write((byte)tri.v0.r);
            writer.Write((byte)tri.v0.g);
            writer.Write((byte)tri.v0.b);
            writer.Write((byte)0); // padding

            writer.Write((byte)tri.v1.r);
            writer.Write((byte)tri.v1.g);
            writer.Write((byte)tri.v1.b);
            writer.Write((byte)0); // padding

            writer.Write((byte)tri.v2.r);
            writer.Write((byte)tri.v2.g);
            writer.Write((byte)tri.v2.b);
            writer.Write((byte)0); // padding
          }
        }

        // Atlas data section: Write raw texture data for each atlas.
        foreach (TextureAtlas atlas in _atlases)
        {
          AlignToFourBytes(writer);
          // Record the current offset for this atlas's data.
          long atlasDataOffset = writer.BaseStream.Position;
          atlasDataOffsets.Add(atlasDataOffset);

          // Write the atlas's raw texture data.
          for (int y = 0; y < atlas.vramPixels.GetLength(1); y++)
          {
            for (int x = 0; x < atlas.vramPixels.GetLength(0); x++)
            {
              writer.Write(atlas.vramPixels[x, y].Pack());
            }
          }
        }

        // Backfill the mesh data offsets into the metadata section.
        if (offsetPlaceholderPositions.Count == meshDataOffsets.Count)
        {
          for (int i = 0; i < offsetPlaceholderPositions.Count; i++)
          {
            writer.Seek((int)offsetPlaceholderPositions[i], SeekOrigin.Begin);
            writer.Write((int)meshDataOffsets[i]);
          }
        }
        else
        {
          Debug.LogError("Mismatch between metadata mesh offset placeholders and mesh data blocks!");
        }

        // Backfill the atlas data offsets into the metadata section.
        if (atlasOffsetPlaceholderPositions.Count == atlasDataOffsets.Count)
        {
          for (int i = 0; i < atlasOffsetPlaceholderPositions.Count; i++)
          {
            writer.Seek((int)atlasOffsetPlaceholderPositions[i], SeekOrigin.Begin);
            writer.Write((int)atlasDataOffsets[i]);
          }
        }
        else
        {
          Debug.LogError("Mismatch between atlas offset placeholders and atlas data blocks!");
        }
      }
      Debug.Log(totalFaces);
    }

    void AlignToFourBytes(BinaryWriter writer)
    {
      long position = writer.BaseStream.Position;
      int padding = (int)(4 - (position % 4)) % 4; // Compute needed padding
      Debug.Log($"aligned {padding} bytes");
      writer.Write(new byte[padding]); // Write zero padding
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
      Vector3 sceneOrigin = new Vector3(0, 0, 0);
      Vector3 cubeSize = new Vector3(8.0f * GTEScaling, 8.0f * GTEScaling, 8.0f * GTEScaling);
      Gizmos.color = Color.red;
      Gizmos.DrawWireCube(sceneOrigin, cubeSize);
    }
  }
}
