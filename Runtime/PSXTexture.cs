using UnityEngine;

namespace PSXSplash.RuntimeCode
{
    public enum PSXTextureType
    {
        TEX_4BPP = 4,

        TEX_8BPP = 8,

        TEX16_BPP = 16
    }



    [System.Serializable]
    public class PSXTexture
    {
        public PSXTextureType TextureType = PSXTextureType.TEX_8BPP;
        public bool Dithering = true;

        [Range(1, 256)]
        public int Width = 128;

        [Range(1, 256)]
        public int Height = 128;


        // TODO: This just uses the quantization and doesn't store the result anywhere
        // Maybe it should return the image and the clut back to the SceneExporter / The Editor code for only-texture export?
        public void Export(GameObject gameObject)
        {
            Debug.Log($"Export: {this}");

            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                Texture texture = meshRenderer.material.mainTexture;
                if (texture is Texture2D)
                {
                    Texture2D originalTexture = (Texture2D)texture;

                    Texture2D newTexture = new Texture2D(originalTexture.width, originalTexture.height, originalTexture.format, false);
                    newTexture.SetPixels(originalTexture.GetPixels());
                    newTexture.Apply();
                    Debug.Log((int)TextureType);
                    newTexture.Reinitialize(Width, Height, UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8_UInt, false);
                    var (quantizedPixels, clut) = ImageQuantizer.Quantize(originalTexture, (int)TextureType);

                }
            }
        }
    }
}
