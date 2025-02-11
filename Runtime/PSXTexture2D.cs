using System.Collections.Generic;
using UnityEngine;
using static PSXSplash.RuntimeCode.TextureQuantizer;


namespace PSXSplash.RuntimeCode
{

    public enum PSXBPP
    {
        TEX_4BIT = 4,
        TEX_8BIT = 8,
        TEX_16BIT = 15
    }

    public struct VRAMPixel
    {
        private ushort r; // 0-4 bits
        private ushort g; // 5-9 bits
        private ushort b; // 10-14 bits

        public ushort R
        {
            get => r;
            set => r = (ushort)(value & 0b11111);
        }

        public ushort G
        {
            get => g;
            set => g = (ushort)(value & 0b11111);
        }

        public ushort B
        {
            get => b;
            set => b = (ushort)(value & 0b11111);
        }

        public bool SemiTransparent { get; set; } // 15th bit


        public ushort Pack()
        {
            return (ushort)((r << 11) | (g << 6) | (b << 1) | (SemiTransparent ? 1 : 0));
        }

        public void Unpack(ushort packedValue)
        {
            r = (ushort)((packedValue >> 11) & 0b11111);
            g = (ushort)((packedValue >> 6) & 0b11111);
            b = (ushort)((packedValue >> 1) & 0b11111);
            SemiTransparent = (packedValue & 0b1) != 0;
        }
    }


    public class PSXTexture2D
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public int[] Pixels { get; set; }
        public List<VRAMPixel> ColorPalette = new List<VRAMPixel>();
        public PSXBPP BitDepth { get; set; }
        private int _maxColors;

        // Used only for 16bpp
        public ushort[] ImageData { get; set; }


        public static PSXTexture2D CreateFromTexture2D(Texture2D inputTexture, PSXBPP bitDepth)
        {
            PSXTexture2D psxTex = new PSXTexture2D();

            psxTex.Width = inputTexture.width;
            psxTex.Height = inputTexture.height;
            psxTex.BitDepth = bitDepth;

            if (bitDepth == PSXBPP.TEX_16BIT)
            {
                psxTex.ImageData = new ushort[inputTexture.width * inputTexture.height];
                int i = 0;
                foreach (Color pixel in inputTexture.GetPixels())
                {
                    VRAMPixel vramPixel = new VRAMPixel { R = (ushort)(pixel.r * 31), G = (ushort)(pixel.g * 31), B = (ushort)(pixel.b * 31) };
                    psxTex.ImageData[i] = vramPixel.Pack();
                    i++;
                }
                return psxTex;
            }

            psxTex._maxColors = (int)Mathf.Pow((int)bitDepth, 2);

            QuantizedResult result = Quantize(inputTexture, psxTex._maxColors);

            foreach (Vector3 color in result.Palette)
            {
                Color pixel = new Color(color.x, color.y, color.z);
                VRAMPixel vramPixel = new VRAMPixel { R = (ushort)(pixel.r * 31), G = (ushort)(pixel.g * 31), B = (ushort)(pixel.b * 31) };
                psxTex.ColorPalette.Add(vramPixel);
            }


            psxTex.Pixels = new int[psxTex.Width * psxTex.Height];
            for (int x = 0; x < psxTex.Width; x++)
            {
                for (int y = 0; y < psxTex.Height; y++)
                {
                    psxTex.Pixels[x + y * psxTex.Width] = result.Indices[x, y];
                }
            }


            return psxTex;
        }

        public Texture2D GeneratePreview()
        {
            Texture2D tex = new Texture2D(Width, Height);
            if (BitDepth == PSXBPP.TEX_16BIT)
            {
                Color[] colors16 = new Color[Width * Height];
                // An instance for the Unpack method
                VRAMPixel pixel = new VRAMPixel();

                for (int i = 0; i < ImageData.Length; i++)
                {
                    ushort packedValue = ImageData[i];
                    pixel.Unpack(packedValue);
                    float r = pixel.R / 31f;
                    float g = pixel.G / 31f;
                    float b = pixel.B / 31f;

                    colors16[i] = new Color(r, g, b);
                }
                tex.SetPixels(colors16);
                tex.Apply();
                return tex;
            }


            List<Color> colors = new List<Color>();
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int pixel = Pixels[y * Width + x];
                    VRAMPixel color = ColorPalette[pixel];

                    float r = color.R / 31f;
                    float g = color.G / 31f;
                    float b = color.B / 31f;

                    colors.Add(new Color(r, g, b));
                }
            }
            tex.SetPixels(colors.ToArray());
            tex.Apply();
            return tex;
        }

        public Texture2D GenerateVramPreview()
        {

            if (BitDepth == PSXBPP.TEX_16BIT)
            {
                return GeneratePreview();
            }

            int adjustedWidth = Width;

            if (BitDepth == PSXBPP.TEX_4BIT)
            {
                adjustedWidth = Mathf.CeilToInt(Width / 4f);
            }
            else if (BitDepth == PSXBPP.TEX_8BIT)
            {
                adjustedWidth = Mathf.CeilToInt(Width / 2f);
            }

            Texture2D vramTexture = new Texture2D(adjustedWidth, Height);

            List<ushort> packedValues = new List<ushort>();

            if (BitDepth == PSXBPP.TEX_4BIT)
            {
                for (int i = 0; i < Pixels.Length; i += 4)
                {
                    ushort packed = (ushort)((Pixels[i] << 12) | (Pixels[i + 1] << 8) | (Pixels[i + 2] << 4) | Pixels[i + 3]);
                    packedValues.Add(packed);
                }
            }
            else if (BitDepth == PSXBPP.TEX_8BIT)
            {
                for (int i = 0; i < Pixels.Length; i += 2)
                {
                    ushort packed = (ushort)((Pixels[i] << 8) | Pixels[i + 1]);
                    packedValues.Add(packed);
                }
            }


            List<Color> colors = new List<Color>();
            for (int i = 0; i < packedValues.Count; i++)
            {
                int index = packedValues[i];

                float r = (index & 31) / 31.0f;
                float g = ((index >> 5) & 31) / 31.0f;
                float b = ((index >> 10) & 31) / 31.0f;

                colors.Add(new Color(r, g, b));
            }
            vramTexture.SetPixels(colors.ToArray());
            vramTexture.Apply();

            return vramTexture;

        }
    }
}