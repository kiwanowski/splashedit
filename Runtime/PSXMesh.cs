using UnityEngine;

namespace PSXSplash.RuntimeCode
{

    [System.Serializable]
    public class PSXMesh
    {
        public bool TriangulateMesh = true;

        public void Export(GameObject gameObject)
        {
            Debug.Log($"Export: {this}");
        }
    }
}