using System.Collections.Generic;
using UnityEngine;

namespace Domo.PixelArtFont
{
    /// <summary>
    /// Responsible for building the glyph atlas for the font asset.
    /// </summary>
    public static class PixelArtFontAtlasBuilder
    {
        public static Texture2D BuildAtlas(PixelArtFontAsset asset)
        {
            if (asset == null) return null;

            CalcAtlasProperties(asset, out List<int> glyphIndexOrder, out int atlasStartingSize);
            List<(RectInt from, RectInt to)> packedRects = new();

            if (glyphIndexOrder.Count == 0) return null;

            int curAtlasSize = atlasStartingSize;
            bool halfHeight = false;
            bool foundPacking = false;

            const int kMaxSize = 4096;
            for (int s = atlasStartingSize; s <= kMaxSize; s *= 2)
            {
                if (TryPack(s, asset, glyphIndexOrder, packedRects, out halfHeight))
                {
                    curAtlasSize = s;
                    foundPacking = true;
                    break;
                }
            }

            if (!foundPacking)
            {
                Debug.LogError($"Couldn't pack glyphs into atlas; texture exceeds size of {kMaxSize}x{kMaxSize}!");
                return null;
            }

            //Assign packed rects back to glyphData so that the font asset can have them later
            //Also, expand the texture rects if we use atlas padding greater than one in case we're using a blit
            //material that grows / offsets the text
            List<GlyphData> glyphData = asset.GlyphData;
            Vector2Int expansion = Mathf.Max(0, asset.AtlasPadding - 1) * Vector2Int.one;
            for (int i = 0; i < glyphIndexOrder.Count; i++)
            {
                RectInt rect = packedRects[i].to;
                GlyphData glyph = glyphData[glyphIndexOrder[i]];
                glyph.AtlasRect = Expand(rect, expansion);
            }

            static RectInt Expand(RectInt rect, Vector2Int expansion)
            {
                rect.position -= expansion;
                rect.size += 2 * expansion;
                return rect;
            }

            Vector2Int finalAtlasSize = new(curAtlasSize, halfHeight ? curAtlasSize / 2 : curAtlasSize);
            Texture2D atlas = CreateAtlasTexture(asset, finalAtlasSize, packedRects);

            return atlas;
        }

        public static bool CalcAtlasProperties(PixelArtFontAsset asset, out List<int> glyphIndexOrder, 
            out int atlasStartingSize)
        {
            var glyphData = asset.GlyphData;

            glyphIndexOrder = new();

            Vector2Int totalSize = Vector2Int.zero;
            int padding = asset.AtlasPadding;
            padding = Mathf.Max(1, padding);

            for (int i = 0; i < glyphData.Count; i++)
            {
                var glyph = glyphData[i];
                Vector2Int size = glyph.TextureRect.size;
                if (size.magnitude > 0)
                {
                    glyphIndexOrder.Add(i);
                    totalSize += size;
                }
            }

            int countedRects = glyphIndexOrder.Count;
            Vector2 averageSize = (Vector2)totalSize / countedRects;
            float minArea = (averageSize.x + padding) * (averageSize.y + padding) * countedRects;
            atlasStartingSize = Mathf.NextPowerOfTwo((int)Mathf.Sqrt(minArea));

            if (glyphIndexOrder.Count == 0)
            {
                return false;
            }

            static int Compare(Vector2 a, Vector2 b)
            {
                //We want the largest first.
                //It'd be a pain to iterate on the collection if we sorted it by smallest first.
                int compare = -a.y.CompareTo(b.y);
                if (compare == 0) compare = -a.x.CompareTo(b.x);
                return compare;
            }

            glyphIndexOrder.Sort((a, b) =>
            {
                GlyphData glyphA = glyphData[a];
                GlyphData glyphB = glyphData[b];
                return Compare(glyphA.TextureRect.size, glyphB.TextureRect.size);
            });

            return true;
        }

        static bool TryPack(int size, PixelArtFontAsset asset, List<int> glyphIndexOrder,
            List<(RectInt from, RectInt to)> packedRects, out bool halfHeight)
        {
            halfHeight = false;

            int padding = asset.AtlasPadding;
            padding = Mathf.Max(1, padding);
            var glyphData = asset.GlyphData;

            packedRects.Clear();

            int x = padding, y = padding;
            int rowHeight = glyphData[glyphIndexOrder[0]].TextureRect.size.y;

            for (int i = 0; i < glyphIndexOrder.Count; i++)
            {
                int i2 = glyphIndexOrder[i];
                GlyphData glyph = glyphData[i2];
                Vector2Int glyphSize = glyph.TextureRect.size;
                RectInt from = glyph.TextureRect;

                //Move to next row
                if (x + glyphSize.x + padding > size)
                {
                    x = padding;
                    y += (2 * padding) + rowHeight;
                    rowHeight = glyphSize.y;
                }

                //Does not fit in current image; go increase size instead
                if (y + glyphSize.y + padding > size)
                {
                    return false;
                }

                RectInt to = new(x, y, glyphSize.x, glyphSize.y);
                packedRects.Add((from, to));
                x += (padding * 2) + glyphSize.x;
            }

            if (packedRects.Count == 0) return false;

            RectInt finalRect = packedRects[^1].to;
            if ((finalRect.position.y + finalRect.height) < size / 2)
            {
                halfHeight = true;
            }

            return true;
        }

        public static Texture2D CreateAtlasTexture(PixelArtFontAsset asset, Vector2Int atlasSize, 
            List<(RectInt from, RectInt to)> packedRects)
        {
            Texture2D texture = asset.Texture;
            //Create a new atlas texture since the previous atlas might be null, or if not null: we cannot
            //reinitalize it as we deliberately set isReadable to false.

            Texture2D atlas = new(atlasSize.x, atlasSize.y, texture.format, false)
            {
                name = $"{asset.FontFamilyName}_Atlas",
                filterMode = FilterMode.Point,
            };

            bool isSingleChannel = texture.format == TextureFormat.R8;
            RenderTextureFormat format = isSingleChannel ? RenderTextureFormat.R8 : RenderTextureFormat.Default;
            RenderTexture tmp = RenderTexture.GetTemporary(atlasSize.x, atlasSize.y, 0, format, 0);
            tmp.Create();

            RenderTexture prevRenderTexture = RenderTexture.active;
            RenderTexture.active = tmp;
            GL.Clear(true, true, new(0, 0, 0, 0));

            for (int i = 0; i < packedRects.Count; i++)
            {
                (RectInt from, RectInt to) = packedRects[i];
                Graphics.CopyTexture(texture, 0, 0, from.x, from.y, from.width, from.height, tmp, 0, 0, to.x, to.y);
            }

            bool hasBlitMaterials = asset.BlitMaterials.Count > 0;
            RenderTexture tmp2 = null;
            if (hasBlitMaterials)
            {
                tmp2 = RenderTexture.GetTemporary(atlasSize.x, atlasSize.y, 0, format, 0);
                for (int i = 0; i < asset.BlitMaterials.Count; i++)
                {
                    Material blitMaterial = new(asset.BlitMaterials[i]);
                    blitMaterial.SetTexture(Shader.PropertyToID("_MainTex"), tmp);
                    if (blitMaterial.HasProperty(Shader.PropertyToID("_SingleChannel")))
                    {
                        blitMaterial.SetFloat(Shader.PropertyToID("_SingleChannel"), isSingleChannel ? 1 : 0);
                    }
                    RenderTexture.active = tmp2;
                    Graphics.Blit(tmp, blitMaterial);
                    (tmp, tmp2) = (tmp2, tmp);
                }
            }

            atlas.ReadPixels(new(Vector2.zero, atlasSize), 0, 0, false);
            atlas.Apply(false, true);
            atlas.imageContentsHash = texture.imageContentsHash;

            RenderTexture.active = prevRenderTexture;

            tmp.Release();
            if (tmp2 != null) tmp2.Release();

            return atlas;
        }
    }
}