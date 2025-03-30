using System.Collections.Generic;
using System.IO;
using System.Linq;
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

    private Vector2 selectedResolution;
    private bool dualBuffering;
    private bool verticalLayout;
    private List<ProhibitedArea> prohibitedAreas;

    public void Export()
    {
      _psxData = DataStorage.LoadData();
      selectedResolution = _psxData.OutputResolution;
      dualBuffering = _psxData.DualBuffering;
      verticalLayout = _psxData.VerticalBuffering;
      prohibitedAreas = _psxData.ProhibitedAreas;

      _exporters = FindObjectsByType<PSXObjectExporter>(FindObjectsSortMode.None);
      foreach (PSXObjectExporter exp in _exporters)
      {
        exp.CreatePSXTextures2D();
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

      int clutCount = 0;

      // Cluts
      foreach (TextureAtlas atlas in _atlases)
      {
        foreach (var texture in atlas.ContainedTextures)
        {
          if (texture.ColorPalette != null)
          {
            clutCount++;
          }
        }
      }

      using (BinaryWriter writer = new BinaryWriter(File.Open(path, FileMode.Create)))
      {
        // Header
        writer.Write('S');
        writer.Write('P');
        writer.Write((ushort)1);
        writer.Write((ushort)_exporters.Length);
        writer.Write((ushort)_atlases.Length);
        writer.Write((ushort)clutCount);
        writer.Write((ushort)0);
        // Start of Metadata section

        // GameObject section (exporters)
        foreach (PSXObjectExporter exporter in _exporters)
        {
          // Write object's transform
          writer.Write((int)PSXTrig.ConvertCoordinateToPSX(exporter.transform.localToWorldMatrix.GetPosition().x, GTEScaling));
          writer.Write((int)PSXTrig.ConvertCoordinateToPSX(-exporter.transform.localToWorldMatrix.GetPosition().y, GTEScaling));
          writer.Write((int)PSXTrig.ConvertCoordinateToPSX(exporter.transform.localToWorldMatrix.GetPosition().z, GTEScaling));
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


          // Write placeholder for mesh data offset and record its position.
          offsetPlaceholderPositions.Add(writer.BaseStream.Position);
          writer.Write((int)0); // 4-byte placeholder for mesh data offset.

          writer.Write((int)exporter.Mesh.Triangles.Count);
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

        // Cluts
        foreach (TextureAtlas atlas in _atlases)
        {
          foreach (var texture in atlas.ContainedTextures)
          {
            if (texture.ColorPalette != null)
            {
              foreach (VRAMPixel clutPixel in texture.ColorPalette)
              {
                writer.Write((ushort)clutPixel.Pack());
              }
              for (int i = texture.ColorPalette.Count; i < 256; i++)
              {
                writer.Write((ushort)0);
              }
              writer.Write((ushort)texture.ClutPackingX);
              writer.Write((ushort)texture.ClutPackingY);
              writer.Write((ushort)texture.ColorPalette.Count);
              writer.Write((ushort)0);
            }
          }
        }

        // Start of data section

        // Mesh data section: Write mesh data for each exporter.
        foreach (PSXObjectExporter exporter in _exporters)
        {
          AlignToFourBytes(writer);
          // Record the current offset for this exporter's mesh data.
          long meshDataOffset = writer.BaseStream.Position;
          meshDataOffsets.Add(meshDataOffset);

          totalFaces += exporter.Mesh.Triangles.Count;


          foreach (Tri tri in exporter.Mesh.Triangles)
          {
            int expander = 16 / ((int)tri.Texture.BitDepth);
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

            // Write UVs for each vertex, adjusting for texture packing
            writer.Write((byte)(tri.v0.u + tri.Texture.PackingX * expander));
            writer.Write((byte)(tri.v0.v + tri.Texture.PackingY));

            writer.Write((byte)(tri.v1.u + tri.Texture.PackingX * expander));
            writer.Write((byte)(tri.v1.v + tri.Texture.PackingY));

            writer.Write((byte)(tri.v2.u + tri.Texture.PackingX * expander));
            writer.Write((byte)(tri.v2.v + tri.Texture.PackingY));

            writer.Write((ushort)0); // padding


            TPageAttr tpage = new TPageAttr();
            tpage.SetPageX(tri.Texture.TexpageX);
            tpage.SetPageY(tri.Texture.TexpageY);
            switch (tri.Texture.BitDepth)
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
            writer.Write((ushort)tri.Texture.ClutPackingX);
            writer.Write((ushort)tri.Texture.ClutPackingY);
            writer.Write((ushort)0);
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
      writer.Write(new byte[padding]); // Write zero padding
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
