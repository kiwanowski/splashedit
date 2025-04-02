using System;
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
            _psxData = DataStorage.LoadData(out selectedResolution, out dualBuffering, out verticalLayout, out prohibitedAreas);

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
            (Rect buffer1, Rect buffer2) = Utils.BufferForResolution(selectedResolution, verticalLayout);

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

                    void writeVertexPosition(PSXVertex v)
                    {
                        writer.Write((short)v.vx);
                        writer.Write((short)v.vy);
                        writer.Write((short)v.vz);
                    }
                    void writeVertexNormals(PSXVertex v)
                    {
                        writer.Write((short)v.nx);
                        writer.Write((short)v.ny);
                        writer.Write((short)v.nz);
                    }
                    void writeVertexColor(PSXVertex v)
                    {
                        writer.Write((byte)v.r);
                        writer.Write((byte)v.g);
                        writer.Write((byte)v.b);
                        writer.Write((byte)0); // padding
                    }
                    void writeVertexUV(PSXVertex v, PSXTexture2D t ,int expander)
                    {
                        writer.Write((byte)(v.u + t.PackingX * expander));
                        writer.Write((byte)(v.v + t.PackingY));
                    }
                    void foreachVertexDo(Tri tri, Action<PSXVertex> action)
                    {
                        for (int i = 0; i < tri.Vertexes.Length; i++)
                        {
                            action(tri.Vertexes[i]);
                        }
                    }
                    foreach (Tri tri in exporter.Mesh.Triangles)
                    {
                        int expander = 16 / ((int)tri.Texture.BitDepth);
                        // Write vertices coordinates
                        foreachVertexDo(tri, (v) => writeVertexPosition(v));

                        // Write vertex normals for v0 only
                        writeVertexNormals(tri.v0);

                        // Write vertex colors with padding
                        foreachVertexDo(tri, (v) => writeVertexColor(v));

                        // Write UVs for each vertex, adjusting for texture packing
                        foreachVertexDo(tri, (v) => writeVertexUV(v, tri.Texture, expander));

                        writer.Write((ushort)0); // padding


                        TPageAttr tpage = new TPageAttr();
                        tpage.SetPageX(tri.Texture.TexpageX);
                        tpage.SetPageY(tri.Texture.TexpageY);
                        tpage.Set(tri.Texture.BitDepth.ToColorMode());
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
