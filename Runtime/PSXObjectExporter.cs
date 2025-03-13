using UnityEngine;

namespace PSXSplash.RuntimeCode
{
    public class PSXObjectExporter : MonoBehaviour
    {
        public PSXBPP BitDepth;
        public bool MeshIsStatic = true;

        [HideInInspector]
        public PSXTexture2D Texture;

        [HideInInspector]
        public PSXMesh Mesh;

        public void CreatePSXTexture2D()
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.mainTexture is Texture2D texture)
            {
                Texture = PSXTexture2D.CreateFromTexture2D(texture, BitDepth);
                Texture.OriginalTexture = texture;
            }
        }

        public void CreatePSXMesh()
        {
            MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                if(MeshIsStatic) {
                    Mesh = PSXMesh.CreateFromUnityMesh(meshFilter.sharedMesh, Texture.Width, Texture.Height, transform);
                }
                else {
                    Mesh = PSXMesh.CreateFromUnityMesh(meshFilter.sharedMesh, Texture.Width, Texture.Height);
                }
            }
        }
    }
}

