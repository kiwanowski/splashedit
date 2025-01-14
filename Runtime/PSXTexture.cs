using System.IO;
using UnityEditor;
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

        public int MaxKMeans = 50;



        public ushort[] ExportTexture(GameObject gameObject)
        {
            Debug.Log($"Export: {this}");

            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                Texture texture = meshRenderer.material.mainTexture;
                if (texture is Texture2D)
                {
                    Texture2D originalTexture = (Texture2D)texture;

                    Texture2D newTexture = ResizeTexture(originalTexture, Width, Height);
                    if (TextureType == PSXTextureType.TEX16_BPP)
                    {
                        ushort[] converted = ConvertTo16Bpp(newTexture);
                        return converted;
                    }
                    else
                    {
                        var (indexedPixels, _) = ImageQuantizer.Quantize(newTexture, (int)TextureType, MaxKMeans);
                        return indexedPixels;
                    }

                }
            }
            return null;
        }

        public ushort[] ExportClut(GameObject gameObject)
        {
            Debug.Log($"Export: {this}");

            MeshRenderer meshRenderer = gameObject.GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
                Texture texture = meshRenderer.material.mainTexture;
                if (texture is Texture2D)
                {
                    Texture2D originalTexture = (Texture2D)texture;

                    Texture2D newTexture = ResizeTexture(originalTexture, Width, Height);
                    if (TextureType == PSXTextureType.TEX16_BPP)
                    {
                        return null;
                    }
                    else
                    {
                        var (_, generatedClut) = ImageQuantizer.Quantize(newTexture, (int)TextureType, MaxKMeans);
                        return generatedClut;
                    }

                }
            }
            return null;
        }

        public static Texture2D ResizeTexture(Texture2D source, int newWidth, int newHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight);
            rt.antiAliasing = 1;
            Graphics.Blit(source, rt);

            Texture2D resizedTexture = new Texture2D(newWidth, newHeight);
            RenderTexture.active = rt;
            resizedTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            resizedTexture.Apply();

            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);

            return resizedTexture;
        }

        public static ushort[] ConvertTo16Bpp(Texture2D source)
        {
            int width = source.width;
            int height = source.height;
            ushort[] packedData = new ushort[width * height];

            Color[] originalPixels = source.GetPixels();

            // Flip the image on the Y-axis
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int flippedY = height - y - 1;

                    int index = flippedY * width + x;

                    // Retrieve the pixel color
                    Color pixel = originalPixels[index];

                    // Convert to 5-bit components
                    int r = Mathf.Clamp(Mathf.RoundToInt(pixel.r * 31), 0, 31); // 5 bits for red
                    int g = Mathf.Clamp(Mathf.RoundToInt(pixel.g * 31), 0, 31); // 5 bits for green
                    int b = Mathf.Clamp(Mathf.RoundToInt(pixel.b * 31), 0, 31); // 5 bits for blue

                    // Pack into a ushort: R(0..4), G(5..9), B(10..14), Padding(15)
                    packedData[y * width + x] = (ushort)((b << 10) | (g << 5) | r);
                }
            }

            return packedData;
        }


    }
}
