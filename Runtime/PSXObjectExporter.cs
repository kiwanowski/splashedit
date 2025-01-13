using UnityEngine;

namespace PSXSplash.RuntimeCode
{
    public class PSXObjectExporter : MonoBehaviour
    {

        public PSXMesh Mesh;
        public PSXTexture Texture;

        public void Export()
        {
            Debug.Log($"Export: {name}");
        }
    }
}

