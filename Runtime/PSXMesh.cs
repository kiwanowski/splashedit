using System.Collections.Generic;
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
    }

    /// <summary>
    /// A mesh structure that holds a list of triangles converted from a Unity mesh into the PSX format.
    /// </summary>
    [System.Serializable]
    public class PSXMesh
    {
        public List<Tri> Triangles;

        /// <summary>
        /// Creates a PSXMesh from a Unity Mesh by converting its vertices, normals, UVs, and applying shading.
        /// </summary>
        /// <param name="mesh">The Unity mesh to convert.</param>
        /// <param name="textureWidth">Width of the texture (default is 256).</param>
        /// <param name="textureHeight">Height of the texture (default is 256).</param>
        /// <param name="transform">Optional transform to convert vertices to world space.</param>
        /// <returns>A new PSXMesh containing the converted triangles.</returns>
        public static PSXMesh CreateFromUnityMesh(Mesh mesh, float GTEScaling, Transform transform, bool isStatic, int textureWidth = 256, int textureHeight = 256)
        {
            PSXMesh psxMesh = new PSXMesh { Triangles = new List<Tri>() };

            // Get mesh data arrays.
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uv = mesh.uv;
            int[] indices = mesh.triangles;

            // Determine the primary light's direction and color for shading.
            Light mainLight = RenderSettings.sun;
            Vector3 lightDir = mainLight ? mainLight.transform.forward : Vector3.down; // Fixed: Removed negation.
            Color lightColor = mainLight ? mainLight.color * mainLight.intensity : Color.white;

            // Iterate over each triangle (group of 3 indices).
            for (int i = 0; i < indices.Length; i += 3)
            {
                int vid0 = indices[i];
                int vid1 = indices[i + 1];
                int vid2 = indices[i + 2];

                Vector3 v0, v1, v2;

                // Transform vertices to world space if a transform is provided.

                if (isStatic)
                {
                    v0 = transform.TransformPoint(vertices[vid0]);
                    v1 = transform.TransformPoint(vertices[vid1]);
                    v2 = transform.TransformPoint(vertices[vid2]);
                }
                else
                {
                    // Extract ONLY world scale
                    Vector3 worldScale = transform.lossyScale;

                    // Apply scale *before* transformation, ensuring rotation isn’t affected
                    v0 =  Vector3.Scale(vertices[vid0], worldScale);
                    v1 = Vector3.Scale(vertices[vid1], worldScale);
                    v2 = Vector3.Scale(vertices[vid2], worldScale);

                }

                // Convert vertices to PSX format including fixed-point conversion and shading.
                PSXVertex psxV0 = ConvertToPSXVertex(v0, GTEScaling, normals[vid0], uv[vid0], lightDir, lightColor, textureWidth, textureHeight);
                PSXVertex psxV1 = ConvertToPSXVertex(v1, GTEScaling, normals[vid1], uv[vid1], lightDir, lightColor, textureWidth, textureHeight);
                PSXVertex psxV2 = ConvertToPSXVertex(v2, GTEScaling, normals[vid2], uv[vid2], lightDir, lightColor, textureWidth, textureHeight);

                // Add the constructed triangle to the mesh.
                psxMesh.Triangles.Add(new Tri { v0 = psxV0, v1 = psxV1, v2 = psxV2 });
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
        private static PSXVertex ConvertToPSXVertex(Vector3 vertex, float GTEScaling, Vector3 normal, Vector2 uv, Vector3 lightDir, Color lightColor, int textureWidth, int textureHeight)
        {
            // Calculate light intensity based on the angle between the normalized normal and light direction.
            float lightIntensity = Mathf.Clamp01(Vector3.Dot(normal.normalized, lightDir));

            // Compute the final shaded color by multiplying the light color by the intensity.
            Color shadedColor = lightColor * lightIntensity;

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
                r = (byte)Mathf.Clamp(shadedColor.r * 255, 0, 255),
                g = (byte)Mathf.Clamp(shadedColor.g * 255, 0, 255),
                b = (byte)Mathf.Clamp(shadedColor.b * 255, 0, 255)
            };

            return psxVertex;
        }
    }
}
