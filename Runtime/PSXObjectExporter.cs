using UnityEngine;

namespace PSXSplash.RuntimeCode
{
    public class PSXObjectExporter : MonoBehaviour
    {

        public PSXMesh Mesh;
        //ublic PSXTexture Texture;

        public void Export()
        {
            Debug.Log($"Export: {name}");
        }
    }
}

