using System.Collections.Generic;
using UnityEngine;

namespace PSXSplash.RuntimeCode
{

    public struct PSXVertex
    {
        public short vx, vy, vz;
        public byte u, v;
    }

    public struct Tri
    {
        public PSXVertex v0;
        public PSXVertex v1;
        public PSXVertex v2;
    }

    [System.Serializable]
    public class PSXMesh
    {
        public List<Tri> Triangles;

        public static PSXMesh CreateFromUnityMesh(Mesh mesh, int textureWidth, int textureHeight)
        {
            PSXMesh psxMesh = new PSXMesh { Triangles = new List<Tri>() };

            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            int[] indices = mesh.triangles;

            for (int i = 0; i < indices.Length; i += 3)
            {
                int vid0 = indices[i];
                int vid1 = indices[i + 1];
                int vid2 = indices[i + 2];

                PSXVertex v0 = ConvertToPSXVertex(vertices[vid0], uv[vid0], textureWidth, textureHeight);
                PSXVertex v1 = ConvertToPSXVertex(vertices[vid1], uv[vid1], textureWidth, textureHeight);
                PSXVertex v2 = ConvertToPSXVertex(vertices[vid2], uv[vid2], textureWidth, textureHeight);

                psxMesh.Triangles.Add(new Tri { v0 = v0, v1 = v1, v2 = v2 });
            }

            return psxMesh;
        }

        private static PSXVertex ConvertToPSXVertex(Vector3 vertex, Vector2 uv, int textureWidth, int textureHeight)
        {
            PSXVertex psxVertex = new PSXVertex
            {
                vx = (short)(Mathf.Clamp(vertex.x, -4f, 3.999f) * 4096),
                vy = (short)(Mathf.Clamp(vertex.y, -4f, 3.999f) * 4096),
                vz = (short)(Mathf.Clamp(vertex.z, -4f, 3.999f) * 4096),
                u = (byte)(Mathf.Clamp(uv.x * textureWidth, 0, 255)),
                v = (byte)(Mathf.Clamp((1.0f - uv.y) * textureHeight, 0, 255))
            };
            return psxVertex;
        }
    }
}