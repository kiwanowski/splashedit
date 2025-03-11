using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PSXSplash.RuntimeCode;

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

    public VRAMPacker(List<Rect> reservedAreas)
    {
        _reservedAreas = reservedAreas;
    }

    public (PSXObjectExporter[] processedObjects, List<TextureAtlas> atlases) PackTexturesIntoVRAM(PSXObjectExporter[] objects)
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
                if (uniqueTextures.Any(tex => tex.OriginalTexture.GetInstanceID() == obj.Texture.OriginalTexture.GetInstanceID() && tex.BitDepth == obj.Texture.BitDepth))
                {
                    obj.Texture = uniqueTextures.First(tex => tex.OriginalTexture.GetInstanceID() == obj.Texture.OriginalTexture.GetInstanceID());
                    continue;
                }

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
        return (objects, _finalizedAtlases);
    }

    private bool TryPlaceTextureInAtlas(TextureAtlas atlas, PSXTexture2D texture)
    {
        for (int y = 0; y <= TextureAtlas.Height - texture.Height; y++)
        {
            for (int x = 0; x <= atlas.Width - texture.QuantizedWidth; x++)
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
                for (int y = VRAM_HEIGHT - TextureAtlas.Height; y >= 0; y -= 256)
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
                                break;
                            }
                        }
                    }
                    if (placed)
                    {
                        foreach (PSXTexture2D texture in atlas.ContainedTextures)
                        {
                            texture.TexpageNum = CalculateTexpage(atlas.PositionX, atlas.PositionY);
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

            for (int y = 0; y < VRAM_HEIGHT; y++)
            {
                for (int x = 0; x <= VRAM_WIDTH - clutWidth; x++)
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

    private bool IsPlacementValid(Rect rect)
    {
        Rect adjustedRect = new Rect(rect.x, VRAM_HEIGHT - rect.y - rect.height, rect.width, rect.height);
        return !_finalizedAtlases.Any(a => AtlasOverlaps(a, rect)) && !_reservedAreas.Any(b => b.Overlaps(adjustedRect));
    }

    private bool AtlasOverlaps(TextureAtlas atlas, Rect rect)
    {
        Rect atlasRect = new Rect(atlas.PositionX, atlas.PositionY, atlas.Width, TextureAtlas.Height);
        return atlasRect.Overlaps(rect);
    }

    private int CalculateTexpage(int x, int y)
    {
        int columns = 16;
        int rows = 2;
        int colIndex = x / 64;
        int rowIndex = (rows - 1) - (y / 256);
        return (rowIndex * columns) + colIndex;
    }
}
