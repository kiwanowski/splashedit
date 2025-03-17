using UnityEngine;

namespace SplashEdit.RuntimeCode
{
    public class PSXObjectExporter : MonoBehaviour
    {
        public PSXBPP BitDepth = PSXBPP.TEX_8BIT; // Defines the bit depth of the texture (e.g., 4BPP, 8BPP)
        public bool MeshIsStatic = true; // Determines if the mesh is static, affecting how it's processed. Non-static meshes don't export correctly as of now.

        [HideInInspector]
        public PSXTexture2D Texture; // Stores the converted PlayStation-style texture

        [HideInInspector]
        public PSXMesh Mesh; // Stores the converted PlayStation-style mesh

        /// <summary>
        /// Converts the object's material texture into a PlayStation-compatible texture.
        /// </summary>
        public void CreatePSXTexture2D()
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture is Texture2D texture)
            {
                Texture = PSXTexture2D.CreateFromTexture2D(texture, BitDepth);
                Texture.OriginalTexture = texture; // Stores reference to the original texture
            }
        }

        /// <summary>
        /// Converts the object's mesh into a PlayStation-compatible mesh.
        /// </summary>
        public void CreatePSXMesh()
        {
            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                if (MeshIsStatic)
                {
                    // Static meshes take object transformation into account
                    Mesh = PSXMesh.CreateFromUnityMesh(meshFilter.sharedMesh, Texture.Width, Texture.Height, transform);
                }
                else
                {
                    // Dynamic meshes do not consider object transformation
                    Mesh = PSXMesh.CreateFromUnityMesh(meshFilter.sharedMesh, Texture.Width, Texture.Height);
                }
            }
        }
    }
}
