using UnityEngine;

namespace PSXSplash.RuntimeCode {

    [System.Serializable]
    public class PSXMesh {
        public bool TriangulateMesh = true;

        public void Export() {
            Debug.Log($"Export: {this}");
        }
    }
}