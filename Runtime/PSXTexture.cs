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
                    if (Dithering)
                    {
                        newTexture = DitherTexture(newTexture);
                    }
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

        public static Texture2D DitherTexture(Texture2D sourceTexture, float threshold = 0.2f, float errorDiffusionStrength = 0.1f)
        {
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            Color[] pixels = sourceTexture.GetPixels();
            Color[] ditheredPixels = new Color[pixels.Length];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color pixel = pixels[index];

                    // Convert the pixel to grayscale
                    float gray = pixel.grayscale;

                    // Apply threshold to determine if it's black or white
                    int dithered = (gray > threshold) ? 1 : 0;

                    // Calculate the error as the difference between the grayscale value and the dithered result
                    float error = gray - dithered;

                    // Store the dithered pixel
                    ditheredPixels[index] = new Color(dithered, dithered, dithered);

                    // Spread the error to neighboring pixels with customizable error diffusion strength
                    if (x + 1 < width) pixels[(y * width) + (x + 1)] += new Color(error * 7f / 16f * errorDiffusionStrength, error * 7f / 16f * errorDiffusionStrength, error * 7f / 16f * errorDiffusionStrength);
                    if (y + 1 < height) pixels[((y + 1) * width) + x] += new Color(error * 3f / 16f * errorDiffusionStrength, error * 3f / 16f * errorDiffusionStrength, error * 3f / 16f * errorDiffusionStrength);
                    if (x - 1 >= 0 && y + 1 < height) pixels[((y + 1) * width) + (x - 1)] += new Color(error * 5f / 16f * errorDiffusionStrength, error * 5f / 16f * errorDiffusionStrength, error * 5f / 16f * errorDiffusionStrength);
                    if (x + 1 < width && y + 1 < height) pixels[((y + 1) * width) + (x + 1)] += new Color(error * 1f / 16f * errorDiffusionStrength, error * 1f / 16f * errorDiffusionStrength, error * 1f / 16f * errorDiffusionStrength);
                }
            }

            // Clamp the final pixel values to ensure they are valid colors
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].r = Mathf.Clamp01(pixels[i].r);
                pixels[i].g = Mathf.Clamp01(pixels[i].g);
                pixels[i].b = Mathf.Clamp01(pixels[i].b);
            }

            // Create the resulting dithered texture
            Texture2D ditheredTexture = new Texture2D(width, height);
            ditheredTexture.SetPixels(pixels);
            ditheredTexture.Apply();

            return ditheredTexture;
        }

    }
}
