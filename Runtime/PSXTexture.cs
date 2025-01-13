using UnityEngine;

namespace PSXSplash.RuntimeCode {
    public enum PSXTextureType {
        TEX_4BPP,

        TEX_8BPP,

        TEX16_BPP
    }

    [System.Serializable]
    public class PSXTexture
    {
        public PSXTextureType TextureType;
        public bool Dithering = true;

        public void Export() {
            Debug.Log($"Export: {this}");
        }
    }
}
