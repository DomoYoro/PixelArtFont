using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Domo.PixelArtFont
{
    public static class PixelArtFontTextureProcessor
    {
        public static void UpdateGlyphData(PixelArtFontAsset asset)
        {
            Texture2D tex = asset.Texture;

            if (tex == null) return;

            string characters = asset.CharacterList;

            if (characters.Length == 0) return;

            Vector2Int texSize = new(tex.width, tex.height);
            Vector2Int cellSize = asset.CellSize;

            if (cellSize.x <= 0 || cellSize.y <= 0)
            {
                Debug.LogError("Invalid cell size for Pixel Art Font Asset! Aborting glyph data update.");
                return;
            } 

            Vector2Int cellAdvance = cellSize;
            cellAdvance.y *= -1;
            
            bool singleChannel = tex.format == TextureFormat.R8;
            asset.GlyphData.Clear();

            if (SystemInfo.supportsComputeShaders)
            {
                ProcessImageDataFast(asset, tex, texSize, cellSize, characters, singleChannel);
            }
            else
            {
                ProcessImageDataSlow(asset, tex, texSize, cellSize, characters, singleChannel);
            }

            CalcFontAssetProperties(asset, 
                out int ascentLine, out int capHeight, out int baseline, out int meanLine, out int descentLine);

            int lineGap = asset.LineGap;
            asset.SetAscentLine(ascentLine + lineGap);
            asset.SetDescentLine(descentLine + lineGap);
            asset.SetCapHeight(capHeight + lineGap);
            asset.SetMeanLine(meanLine + lineGap);

            EditorUtility.SetDirty(asset);
        }

        static Vector2Int CeilToInt(Vector2 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));
        static Vector2Int FloorToInt(Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        static Vector2Int Mod(Vector2Int a, Vector2Int b) => new(a.x % b.x, a.y % b.y);
        static Vector2Int Max(Vector2Int a, Vector2Int b) => new(Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));

        #region Compute Shader

        private static void ProcessImageDataFast(PixelArtFontAsset asset, Texture2D tex, Vector2Int texSize, 
            Vector2Int cellSize, string characters, bool singleChannel)
        {
            RunComputeShader(tex, texSize, cellSize, singleChannel, out var minMaxData);
            int i = 0;

            Vector2Int cellCounts = FloorToInt(texSize / (Vector2)cellSize);
            for (int r = cellCounts.y - 1; r >= 0; r--)
            {
                for (int c = 0; c < cellCounts.x; c++)
                {
                    int j = (r * cellCounts.x) + c;
                    if (j > minMaxData.Count || j < 0)
                    {
                        Debug.LogError("Exceeded image bounds somehow!");
                        break;
                    }

                    for (; i < characters.Length && char.IsWhiteSpace(characters[i]); i++) ;

                    if (i >= characters.Length) break;

                    GlyphMinMax minMax = minMaxData[j];
                    Vector2Int position = minMax.Min;
                    Vector2Int glyphSize = minMax.Max - minMax.Min;
                    Vector2Int offset = Mod(position, cellSize);

                    if (glyphSize.x <= 0 || glyphSize.y <= 0)
                    {
                        continue;
                    }

                    GlyphData g = Iterate(position, glyphSize, characters, i);

                    if (g == null) continue;
                    g.Advance = glyphSize.x + asset.GlyphSpacing;
                    g.Bearing = offset;
                    g.Bearing.y += glyphSize.y;

                    asset.GlyphData.Add(g);
                    i++;
                }
            }
        }

        private static readonly int ResultID = Shader.PropertyToID("_Result");
        private static readonly int DimensionsID = Shader.PropertyToID("_Dimensions");
        private static readonly int CellSizeID = Shader.PropertyToID("_CellSize");
        private static readonly int MainTexID = Shader.PropertyToID("_MainTex");
        private static readonly int BoundsID = Shader.PropertyToID("_Bounds");
        private static readonly int CellsPerRowID = Shader.PropertyToID("_CellsPerRow");

        private const string SingleChannelMapperKernal = "AnalyzerSingleChannel";
        private const string MultiChannelMapperKernal = "Analyzer";

        private static void RunComputeShader(Texture2D tex, Vector2Int imageSize, Vector2Int cellSize, 
            bool singleChannel, out List<GlyphMinMax> glyphData)
        {
            glyphData = new();

            string kernalName;
            if (singleChannel)
            {
                kernalName = SingleChannelMapperKernal;
            }
            else
            {
                kernalName = MultiChannelMapperKernal;
            }

            if (PixelArtFontDirectory.Instance == null ||
                PixelArtFontDirectory.Instance.AnalyzerComputeShader == null)
            {
                Debug.Log("Unable to locate the pixel art font texture analyzer!");
                return;
            }

            ComputeShader analyzer = PixelArtFontDirectory.Instance.AnalyzerComputeShader;
            int kernelHandle = analyzer.FindKernel(kernalName);

            Vector2Int mod = Mod(imageSize, cellSize);
            Vector4 bounds = new(0, mod.y, imageSize.x - mod.x, imageSize.y);
            Vector2Int cellCount = FloorToInt((Vector2)imageSize / (Vector2)cellSize);

            analyzer.SetTexture(kernelHandle, MainTexID, tex);
            analyzer.SetVector(DimensionsID, (Vector2)imageSize);
            analyzer.SetVector(CellSizeID, (Vector2)cellSize);
            analyzer.SetVector(BoundsID, bounds);
            analyzer.SetFloat(CellsPerRowID, cellCount.x);
            Vector2Int threadCount = GetThreadCount(imageSize);


            ComputeBuffer resultBuffer = new(4 * cellCount.x * cellCount.y, sizeof(uint));
            uint[] data = new uint[resultBuffer.count];
            uint sizeX = (uint)imageSize.x;
            uint sizeY = (uint)imageSize.y;
            for (int i = 0; i < data.Length; i += 4)
            {
                data[i + 0] = sizeX;
                data[i + 1] = sizeY;
            }
            resultBuffer.SetData(data);

            analyzer.SetBuffer(kernelHandle, ResultID, resultBuffer);
            analyzer.Dispatch(kernelHandle, threadCount.x, threadCount.y, 1);

            resultBuffer.GetData(data);
            resultBuffer.Release();

            for (int i = 0; i < data.Length; i += 4)
            {
                glyphData.Add(new(data[i + 0], data[i + 1], data[i + 2], data[i + 3]));
            }
        }

        public struct GlyphMinMax
        {
            public GlyphMinMax(uint minX, uint minY, uint maxX, uint maxY)
            {
                Min = new((int)minX, (int)minY);
                Max = new((int)maxX, (int)maxY);
            }
            public Vector2Int Min;
            public Vector2Int Max;
        }

        public static Vector2Int GetThreadCount(Vector2Int texSize)
        {
            const float kPixelsPerThread = 8;
            Vector2Int count = CeilToInt((Vector2)texSize / (Vector2.one * kPixelsPerThread));
            return Max(count, Vector2Int.one);
        }
        #endregion Compute Shader

        #region CPU Processing
        private static void ProcessImageDataSlow(PixelArtFontAsset asset, Texture2D tex, Vector2Int texSize, 
            Vector2Int cellSize, string characters, bool singleChannel)
        {
            Color32[] imageData = tex.GetPixels32(0);
            var glyphData = asset.GlyphData;
            int i = 0;
            Vector2Int cellIndex = Vector2Int.zero;

            //Top to bottom
            for (int y = texSize.y - cellSize.y; y >= 0; y -= cellSize.y, cellIndex.y++)
            {
                //Left to right
                for (int x = 0; x < texSize.x; x += cellSize.x, cellIndex.x++)
                {
                    for (; i < characters.Length && char.IsWhiteSpace(characters[i]); i++) ;

                    if (i >= characters.Length) break;

                    RectInt cellRect = new(new(x, y), cellSize);
                    InterpretColor32Data(imageData, singleChannel, cellRect, texSize, cellSize, 
                        out Vector2Int glyphSize, out Vector2Int offset);
                    if (glyphSize.x <= 0 || glyphSize.y <= 0) continue;
                    Vector2Int position = new Vector2Int(x, y) + offset;
                    GlyphData g = Iterate(position, glyphSize, characters, i);

                    if (g == null) continue;
                    g.Advance = glyphSize.x + asset.GlyphSpacing;
                    g.Bearing = offset;
                    g.Bearing.y += glyphSize.y - asset.LineHeight;

                    glyphData.Add(g);
                    i++;
                }
            }
        }

        private static void InterpretColor32Data(Span<Color32> imageData, bool singleChannel, RectInt rect, 
            Vector2Int imageSize, Vector2Int cellSize, out Vector2Int charSize, out Vector2Int charOffset)
        {
            int minFilledX = cellSize.x, maxFilledX = 0;
            int minFilledY = cellSize.y, maxFilledY = 0;

            Vector2Int bI = Vector2Int.zero;

            for (int y = rect.min.y; y < rect.max.y; y++, bI.y++)
            {
                int i0 = y * imageSize.x;
                bI.x = 0;

                bool hasPixels = false;
                for (int x = rect.min.x; x < rect.max.x; x++, bI.x++)
                {
                    int i = i0 + x;
                    if (i >= imageData.Length)
                    {
                        Debug.LogError("Went beyond image bounds somehow");
                        break;
                    }

                    Color32 color = imageData[i];

                    if (singleChannel && color.r == 0) continue;
                    if (!singleChannel && color.a == 0) continue;

                    minFilledX = Mathf.Min(minFilledX, bI.x);
                    maxFilledX = Mathf.Max(maxFilledX, bI.x);
                    hasPixels = true;
                }

                if (!hasPixels) continue;

                minFilledY = Mathf.Min(minFilledY, bI.y);
                maxFilledY = Mathf.Max(maxFilledY, bI.y);
            }

            charSize = new Vector2Int(maxFilledX - minFilledX, maxFilledY - minFilledY) + Vector2Int.one;
            charOffset = new(minFilledX, minFilledY);
        }

        #endregion CPU Processing

        private static GlyphData Iterate(Vector2Int position, Vector2Int size, string characters, int i)
        {
            RectInt rect = new(position, size);

            char c = characters[i++];

            GlyphData g = new()
            {
                Character = c,
                TextureRect = rect,
            };

            if (!char.IsSurrogate(c))
            {
                g.Unicode = c;
            }
            else
            {
                //We've already incremented i, so we're just checking if it's still in bounds
                if (i < characters.Length) return null;

                char c2 = characters[i];
                if (char.IsHighSurrogate(c) && char.IsLowSurrogate(c2))
                {
                    g.Unicode = (uint)char.ConvertToUtf32(c, c2);
                    return g;
                }

                return null;
            }

            return g;
        }

        #region Calculate Font Asset Properties

        private static void CalcFontAssetProperties(PixelArtFontAsset asset,
            out int ascentLine, out int capHeight, out int baseline, out int meanLine, out int descentLine)
        {
            int totalWidth = 0;
            ascentLine = asset.LineHeight;
            baseline = asset.LineHeight;
            descentLine = asset.LineHeight;
            capHeight = 0;

            //Iterate over all glyphs
            var glyphData = asset.GlyphData;
            for (int i = 0; i < glyphData.Count; i++)
            {
                GlyphData glyph = glyphData[i];
                uint c = glyph.Unicode;
                Vector2Int size = glyph.TextureRect.size;
                totalWidth += size.x;

                if (SitsOnBaseline(c))
                {
                    baseline = Mathf.Min(glyph.Bearing.y, baseline);
                }

                if (SitsAtCapHeight(c))
                {
                    capHeight = Mathf.Max(glyph.Bearing.y + size.y, capHeight);
                }

                if (SitsAtDescentLine(c))
                {
                    descentLine = Mathf.Min(glyph.Bearing.y, descentLine);
                }
            }

            float averageWidth = (float)totalWidth / glyphData.Count;
            int spaceWidth = Mathf.RoundToInt(averageWidth / 2);

            if (asset.SpaceWidth > 0)
            {
                spaceWidth = asset.SpaceWidth;
            }

            //We must add a space character to avoid TMP generating its own with a non-int advance value
            asset.SetSpaceWidth(spaceWidth);
            asset.GlyphData.Add(new()
            {
                Character = ' ',
                Unicode = ' ',
                Advance = spaceWidth,
                TextureRect = new(),
                AtlasRect = new(),
                Bearing = new(),
            });

            meanLine = (capHeight + baseline) / 2;
        }

        public static bool SitsOnBaseline(uint unicode)
        {
            return unicode switch
            {
                >= '0' and <= '9' => true,
                'Q' or 'g' or 'j' or 'p' or 'q' or 'y' => false,
                >= 'A' and <= 'Z' => true,
                _ => false,
            };
        }

        public static bool SitsAtCapHeight(uint unicode)
        {
            return unicode switch
            {
                >= '0' and <= '9' => true,
                'Q' or 'g' or 'j' or 'p' or 'q' or 'y' => false,
                >= 'A' and <= 'Z' => true,
                'b' or 'd' or 'f' or 'h' or 'k' or 'l' or 't' => true,
                _ => false,
            };
        }

        public static bool SitsAtDescentLine(uint unicode)
        {
            return unicode switch
            {
                'Q' or 'g' or 'j' or 'p' or 'q' or 'y' => true,
                _ => false,
            };
        }

        private static int GetSpaceWidth(PixelArtFontAsset asset)
        {
            var glyphData = asset.GlyphData;
            int totalWidth = 0;
            for (int i = 0; i < glyphData.Count; i++)
            {
                totalWidth += glyphData[i].TextureRect.width;
            }
            int averageWidth = Mathf.RoundToInt((float)totalWidth / glyphData.Count);
            return averageWidth / 2;
        }
        #endregion Calculate Font Asset Properties
    }
}