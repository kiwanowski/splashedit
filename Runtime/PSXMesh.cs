using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Diagnostics;

namespace SplashEdit.RuntimeCode
{
    /// <summary>
    /// Represents a vertex formatted for the PSX (PlayStation) style rendering.
    /// </summary>
    public struct PSXVertex
    {
        // Position components in fixed-point format.
        public short vx, vy, vz;
        // Normal vector components in fixed-point format.
        public short nx, ny, nz;
        // Texture coordinates.
        public byte u, v;
        // Vertex color components.
        public byte r, g, b;
    }

    /// <summary>
    /// Represents a triangle defined by three PSX vertices.
    /// </summary>
    public struct Tri
    {
        public PSXVertex v0;
        public PSXVertex v1;
        public PSXVertex v2;

        public PSXTexture2D Texture;
    }

    /// <summary>
    /// A mesh structure that holds a list of triangles converted from a Unity mesh into the PSX format.
    /// </summary>
    [System.Serializable]
    public class PSXMesh
    {
        public List<Tri> Triangles;

        private static Vector3[] RecalculateSmoothNormals(Mesh mesh)
        {
            Vector3[] normals = new Vector3[mesh.vertexCount];
            Dictionary<Vector3, List<int>> vertexMap = new Dictionary<Vector3, List<int>>();

            for (int i = 0; i < mesh.vertexCount; i++)
            {
                Vector3 vertex = mesh.vertices[i];
                if (!vertexMap.ContainsKey(vertex))
                {
                    vertexMap[vertex] = new List<int>();
                }
                vertexMap[vertex].Add(i);
            }

            foreach (var kvp in vertexMap)
            {
                Vector3 smoothNormal = Vector3.zero;
                foreach (int index in kvp.Value)
                {
                    smoothNormal += mesh.normals[index];
                }
                smoothNormal.Normalize();

                foreach (int index in kvp.Value)
                {
                    normals[index] = smoothNormal;
                }
            }

            return normals;
        }


        /// <summary>
        /// Creates a PSXMesh from a Unity Mesh by converting its vertices, normals, UVs, and applying shading.
        /// </summary>
        /// <param name="mesh">The Unity mesh to convert.</param>
        /// <param name="textureWidth">Width of the texture (default is 256).</param>
        /// <param name="textureHeight">Height of the texture (default is 256).</param>
        /// <param name="transform">Optional transform to convert vertices to world space.</param>
        /// <returns>A new PSXMesh containing the converted triangles.</returns>
        public static PSXMesh CreateFromUnityRenderer(Renderer renderer, float GTEScaling, Transform transform, List<PSXTexture2D> textures)
        {
            PSXMesh psxMesh = new PSXMesh { Triangles = new List<Tri>() };

            // Get materials and mesh.
            Material[] materials = renderer.sharedMaterials;
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;

            // Iterate over each submesh.
            for (int submeshIndex = 0; submeshIndex < materials.Length; submeshIndex++)
            {
                // Get the triangles for this submesh.
                int[] submeshTriangles = mesh.GetTriangles(submeshIndex);

                // Get the material for this submesh.
                Material material = materials[submeshIndex];

                // Get the corresponding texture for this material (assume mainTexture).
                Texture2D texture = material.mainTexture as Texture2D;
                PSXTexture2D psxTexture = null;

                if (texture != null)
                {
                    // Find the corresponding PSX texture based on the Unity texture.
                    psxTexture = textures.FirstOrDefault(t => t.OriginalTexture == texture);
                }

                if (psxTexture == null)
                {
                    continue;
                }

                // Get mesh data arrays.
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals =  mesh.normals;// Assuming this function recalculates normals
                Vector3[] smoothNormals = RecalculateSmoothNormals(mesh);
                Vector2[] uv = mesh.uv;

                // Iterate through the triangles of the submesh.
                for (int i = 0; i < submeshTriangles.Length; i += 3)
                {
                    int vid0 = submeshTriangles[i];
                    int vid1 = submeshTriangles[i + 1];
                    int vid2 = submeshTriangles[i + 2];

                    Vector3 faceNormal = Vector3.Cross(vertices[vid1] - vertices[vid0], vertices[vid2] - vertices[vid0]).normalized;

                    if (Vector3.Dot(faceNormal, normals[vid0]) < 0)
                    {
                        (vid1, vid2) = (vid2, vid1);
                    }

                    // Scale the vertices based on world scale.
                    Vector3 v0 = Vector3.Scale(vertices[vid0], transform.lossyScale);
                    Vector3 v1 = Vector3.Scale(vertices[vid1], transform.lossyScale);
                    Vector3 v2 = Vector3.Scale(vertices[vid2], transform.lossyScale);

                    // Transform the vertices to world space.
                    Vector3 wv0 = transform.TransformPoint(vertices[vid0]);
                    Vector3 wv1 = transform.TransformPoint(vertices[vid1]);
                    Vector3 wv2 = transform.TransformPoint(vertices[vid2]);

                    // Transform the normals to world space.
                    Vector3 wn0 = transform.TransformDirection(smoothNormals[vid0]).normalized;
                    Vector3 wn1 = transform.TransformDirection(smoothNormals[vid1]).normalized;
                    Vector3 wn2 = transform.TransformDirection(smoothNormals[vid2]).normalized;

                    // Compute lighting for each vertex (this can be a custom function).
                    Color cv0 = PSXLightingBaker.ComputeLighting(wv0, wn0);
                    Color cv1 = PSXLightingBaker.ComputeLighting(wv1, wn1);
                    Color cv2 = PSXLightingBaker.ComputeLighting(wv2, wn2);

                    // Convert vertices to PSX format, including fixed-point conversion and shading.
                    PSXVertex psxV0 = ConvertToPSXVertex(v0, GTEScaling, normals[vid0], uv[vid0], psxTexture?.Width ?? 0, psxTexture?.Height ?? 0);
                    PSXVertex psxV1 = ConvertToPSXVertex(v1, GTEScaling, normals[vid1], uv[vid1], psxTexture?.Width ?? 0, psxTexture?.Height ?? 0);
                    PSXVertex psxV2 = ConvertToPSXVertex(v2, GTEScaling, normals[vid2], uv[vid2], psxTexture?.Width ?? 0, psxTexture?.Height ?? 0);

                    // Apply lighting to the colors.
                    psxV0.r = (byte)Mathf.Clamp(cv0.r * 255, 0, 255);
                    psxV0.g = (byte)Mathf.Clamp(cv0.g * 255, 0, 255);
                    psxV0.b = (byte)Mathf.Clamp(cv0.b * 255, 0, 255);

                    psxV1.r = (byte)Mathf.Clamp(cv1.r * 255, 0, 255);
                    psxV1.g = (byte)Mathf.Clamp(cv1.g * 255, 0, 255);
                    psxV1.b = (byte)Mathf.Clamp(cv1.b * 255, 0, 255);

                    psxV2.r = (byte)Mathf.Clamp(cv2.r * 255, 0, 255);
                    psxV2.g = (byte)Mathf.Clamp(cv2.g * 255, 0, 255);
                    psxV2.b = (byte)Mathf.Clamp(cv2.b * 255, 0, 255);

                    // Add the constructed triangle to the mesh.
                    psxMesh.Triangles.Add(new Tri { v0 = psxV0, v1 = psxV1, v2 = psxV2, Texture = psxTexture });
                }
            }

            return psxMesh;
        }

        /// <summary>
        /// Converts a Unity vertex into a PSXVertex by applying fixed-point conversion, shading, and UV mapping.
        /// </summary>
        /// <param name="vertex">The position of the vertex.</param>
        /// <param name="normal">The normal vector at the vertex.</param>
        /// <param name="uv">Texture coordinates for the vertex.</param>
        /// <param name="lightDir">The light direction used for shading calculations.</param>
        /// <param name="lightColor">The color of the light affecting the vertex.</param>
        /// <param name="textureWidth">Width of the texture for UV scaling.</param>
        /// <param name="textureHeight">Height of the texture for UV scaling.</param>
        /// <returns>A PSXVertex with converted coordinates, normals, UVs, and color.</returns>
        private static PSXVertex ConvertToPSXVertex(Vector3 vertex, float GTEScaling, Vector3 normal, Vector2 uv, int textureWidth, int textureHeight)
        {


            PSXVertex psxVertex = new PSXVertex
            {
                // Convert position to fixed-point, clamping values to a defined range.
                vx = (short)PSXTrig.ConvertCoordinateToPSX(vertex.x, GTEScaling),
                vy = (short)PSXTrig.ConvertCoordinateToPSX(-vertex.y, GTEScaling),
                vz = (short)PSXTrig.ConvertCoordinateToPSX(vertex.z, GTEScaling),

                // Convert normals to fixed-point.
                nx = (short)PSXTrig.ConvertCoordinateToPSX(normal.x),
                ny = (short)PSXTrig.ConvertCoordinateToPSX(-normal.y),
                nz = (short)PSXTrig.ConvertCoordinateToPSX(normal.z),

                // Map UV coordinates to a byte range after scaling based on texture dimensions.
                u = (byte)Mathf.Clamp(uv.x * (textureWidth - 1), 0, 255),
                v = (byte)Mathf.Clamp((1.0f - uv.y) * (textureHeight - 1), 0, 255),

                // Convert the computed color to a byte range.

            };

            return psxVertex;
        }
    }
}
