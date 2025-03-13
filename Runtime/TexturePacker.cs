using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;



namespace PSXSplash.RuntimeCode
{

    public class TextureAtlas
    {
        public PSXBPP BitDepth;
        public int PositionX;
        public int PositionY;
        public int Width;
        public const int Height = 256;
        public List<PSXTexture2D> ContainedTextures = new List<PSXTexture2D>();
    }

    public class VRAMPacker
    {
        private List<TextureAtlas> _textureAtlases = new List<TextureAtlas>();
        private List<Rect> _reservedAreas;
        private List<TextureAtlas> _finalizedAtlases = new List<TextureAtlas>();
        private List<Rect> _allocatedCLUTs = new List<Rect>();

        private const int VRAM_WIDTH = 1024;
        private const int VRAM_HEIGHT = 512;

        private VRAMPixel[,] _vramPixels;

        public VRAMPacker(List<Rect> framebuffers, List<ProhibitedArea> reservedAreas)
        {
            List<Rect> areasConvertedToRect = new List<Rect>();
            foreach (ProhibitedArea area in reservedAreas)
            {
                areasConvertedToRect.Add(new Rect(area.X, area.Y, area.Width, area.Height));
            }
            _reservedAreas = areasConvertedToRect;
            _reservedAreas.Add(framebuffers[0]);
            _reservedAreas.Add(framebuffers[1]);
            _vramPixels = new VRAMPixel[VRAM_WIDTH, VRAM_HEIGHT];
        }

        public (PSXObjectExporter[] processedObjects, VRAMPixel[,] _vramPixels) PackTexturesIntoVRAM(PSXObjectExporter[] objects)
        {
            List<PSXTexture2D> uniqueTextures = new List<PSXTexture2D>();
            var groupedObjects = objects.GroupBy(obj => obj.Texture.BitDepth).OrderByDescending(g => g.Key);

            foreach (var group in groupedObjects)
            {
                int atlasWidth = group.Key switch
                {
                    PSXBPP.TEX_16BIT => 256,
                    PSXBPP.TEX_8BIT => 128,
                    PSXBPP.TEX_4BIT => 64,
                    _ => 256
                };

                TextureAtlas atlas = new TextureAtlas { BitDepth = group.Key, Width = atlasWidth, PositionX = 0, PositionY = 0 };
                _textureAtlases.Add(atlas);

                foreach (var obj in group.OrderByDescending(obj => obj.Texture.QuantizedWidth * obj.Texture.Height))
                {
                    /*if (uniqueTextures.Any(tex => tex.OriginalTexture.GetInstanceID() == obj.Texture.OriginalTexture.GetInstanceID() && tex.BitDepth == obj.Texture.BitDepth))
                    {
                        obj.Texture = uniqueTextures.First(tex => tex.OriginalTexture.GetInstanceID() == obj.Texture.OriginalTexture.GetInstanceID());
                        continue;
                    }*/

                    if (!TryPlaceTextureInAtlas(atlas, obj.Texture))
                    {
                        atlas = new TextureAtlas { BitDepth = group.Key, Width = atlasWidth, PositionX = 0, PositionY = 0 };
                        _textureAtlases.Add(atlas);
                        if (!TryPlaceTextureInAtlas(atlas, obj.Texture))
                        {
                            Debug.LogError($"Failed to pack texture {obj.Texture}. It might not fit.");
                            break;
                        }
                    }
                    uniqueTextures.Add(obj.Texture);
                }
            }

            ArrangeAtlasesInVRAM();
            AllocateCLUTs();

            BuildVram();
            return (objects, _vramPixels);
        }

        private bool TryPlaceTextureInAtlas(TextureAtlas atlas, PSXTexture2D texture)
        {
            for (byte y = 0; y <= TextureAtlas.Height - texture.Height; y++)
            {
                for (byte x = 0; x <= atlas.Width - texture.QuantizedWidth; x++)
                {
                    var candidateRect = new Rect(x, y, texture.QuantizedWidth, texture.Height);
                    if (!atlas.ContainedTextures.Any(tex => new Rect(tex.PackingX, tex.PackingY, tex.QuantizedWidth, tex.Height).Overlaps(candidateRect)))
                    {
                        texture.PackingX = x;
                        texture.PackingY = y;
                        atlas.ContainedTextures.Add(texture);
                        return true;
                    }
                }
            }
            return false;
        }

        private void ArrangeAtlasesInVRAM()
        {
            foreach (var bitDepth in new[] { PSXBPP.TEX_16BIT, PSXBPP.TEX_8BIT, PSXBPP.TEX_4BIT })
            {
                foreach (var atlas in _textureAtlases.Where(a => a.BitDepth == bitDepth))
                {
                    bool placed = false;
                    for (int y = 0; y <= VRAM_HEIGHT - TextureAtlas.Height; y += 256)
                    {
                        for (int x = 0; x <= VRAM_WIDTH - atlas.Width; x += 64)
                        {
                            if (atlas.PositionX == 0 && atlas.PositionY == 0)
                            {
                                var candidateRect = new Rect(x, y, atlas.Width, TextureAtlas.Height);
                                if (IsPlacementValid(candidateRect))
                                {
                                    atlas.PositionX = x;
                                    atlas.PositionY = y;
                                    _finalizedAtlases.Add(atlas);
                                    placed = true;
                                    Debug.Log($"Placed an atlas at: {x},{y}");
                                    break;
                                }
                            }
                        }
                        if (placed)
                        {
                            foreach (PSXTexture2D texture in atlas.ContainedTextures)
                            {
                                int colIndex = atlas.PositionX / 64;
                                int rowIndex = atlas.PositionY / 256;

                                texture.TexpageX = (byte)colIndex;
                                texture.TexpageY = (byte)rowIndex;
                            }
                            break;
                        }
                    }
                    if (!placed)
                    {
                        Debug.LogError($"Atlas with BitDepth {atlas.BitDepth} and Width {atlas.Width} could not be placed in VRAM.");
                    }
                }
            }
        }

        private void AllocateCLUTs()
        {
            foreach (var texture in _finalizedAtlases.SelectMany(atlas => atlas.ContainedTextures))
            {
                if (texture.ColorPalette == null || texture.ColorPalette.Count == 0)
                    continue;

                int clutWidth = texture.ColorPalette.Count;
                int clutHeight = 1;
                bool placed = false;

                for (ushort x = 0; x < VRAM_WIDTH; x += 16)
                {
                    for (ushort y = 0; y <= VRAM_HEIGHT; y++)
                    {
                        var candidate = new Rect(x, y, clutWidth, clutHeight);
                        if (IsPlacementValid(candidate))
                        {
                            _allocatedCLUTs.Add(candidate);
                            texture.ClutPackingX = x;
                            texture.ClutPackingY = y;
                            placed = true;
                            break;
                        }
                    }
                    if (placed) break;
                }

                if (!placed)
                {
                    Debug.LogError($"Failed to allocate CLUT for texture at {texture.PackingX}, {texture.PackingY}");
                }
            }
        }

        private void BuildVram()
        {
            foreach (TextureAtlas atlas in _finalizedAtlases)
            {
                foreach (PSXTexture2D texture in atlas.ContainedTextures)
                {

                    for (int y = 0; y < texture.Height; y++)
                    {
                        for (int x = 0; x < texture.QuantizedWidth; x++)
                        {
                            _vramPixels[x + atlas.PositionX + texture.PackingX, y + atlas.PositionY + texture.PackingY] = texture.ImageData[x, y];
                        }
                    }

                    if (texture.BitDepth != PSXBPP.TEX_16BIT)
                    {
                        for (int x = 0; x < texture.ColorPalette.Count; x++)
                        {
                            _vramPixels[x + texture.ClutPackingX, texture.ClutPackingY] = texture.ColorPalette[x];
                        }
                    }
                }
            }
        }

        private bool IsPlacementValid(Rect rect)
        {

            if (rect.x + rect.width > VRAM_WIDTH) return false;
            if (rect.y + rect.height > VRAM_HEIGHT) return false;

            bool overlapsAtlas = _finalizedAtlases.Any(a => new Rect(a.PositionX, a.PositionY, a.Width, TextureAtlas.Height).Overlaps(rect));
            bool overlapsReserved = _reservedAreas.Any(r => r.Overlaps(rect));
            bool overlapsCLUT = _allocatedCLUTs.Any(c => c.Overlaps(rect));

            return !(overlapsAtlas || overlapsReserved || overlapsCLUT);
        }

        private int CalculateTexpage(int x, int y)
        {
            int columns = 16;
            int colIndex = x / 64;
            int rowIndex = y / 256;
            return (rowIndex * columns) + colIndex;
        }
    }
}