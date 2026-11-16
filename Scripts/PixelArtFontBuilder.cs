using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

namespace Domo.PixelArtFont
{
    /// <summary>
    /// Responsible for building & maintaining the font asset
    /// </summary>
    public static class PixelArtFontBuilder
    {
        public static void Build(PixelArtFontAsset asset)
        {
            if (asset == null)
            {
                Debug.LogError("No asset being used for build!");
                return;
            }

            if (asset.GlyphData.Count == 0)
            {
                Debug.LogError("Can't build a font asset without any glyph data!");
                return;
            }

            TMP_FontAsset fontAsset = asset.BuiltFontAsset;
            bool created = fontAsset == null;

            string outputPath = asset.HasBuiltFontAsset ? AssetDatabase.GetAssetPath(asset.BuiltFontAsset) :
                (System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(asset)) + "\\" + asset.name + "_Font.asset");

            if (fontAsset == null)
            {
                fontAsset = ScriptableObject.CreateInstance<TMP_FontAsset>();
                fontAsset.name = asset.name;
            }
            else
            {
                string fontAssetPath = AssetDatabase.GetAssetPath(fontAsset);
                var subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(fontAssetPath);

                foreach (var sub in subs)
                {
                    if (sub == null) continue;
                    if (sub is Texture2D)
                    {
                        //Remove the atlas since we just generated a new one
                        AssetDatabase.RemoveObjectFromAsset(sub);
                    }
                }
            }

            bool createdTexture = false;
            var newTex = PixelArtFontAtlasBuilder.BuildAtlas(asset);
            if (newTex != null)
            {
                asset.SetAtlasTexture(newTex);
                createdTexture = true;
            }
            Texture2D atlas = asset.Atlas;

            fontAsset.faceInfo = GetFaceInfo(asset);
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;
            fontAsset.isMultiAtlasTexturesEnabled = false;
            fontAsset.atlasTextures = new[] { atlas };

            //Set material
            bool createdMaterial = false;
            Material material = fontAsset.material;
            if (material == null)
            {
                const string kShaderName = "UI/PixelArtFont";
                material = new(Shader.Find(kShaderName)) { name = $"{asset.FontFamilyName}_Material", };
                createdMaterial = true;
                fontAsset.material = material;
            }

            material.SetTexture(Shader.PropertyToID("_MainTex"), atlas);


            bool shareCases = asset.CharacterListIsSharedBetweenCases;
            bool shareToUpper = false, shareToLower = false;
            if (shareCases)
            {
                string charList = asset.CharacterList;
                shareToUpper = charList.ToUpper().CompareTo(charList) != 0;
                shareToLower = charList.ToLower().CompareTo(charList) != 0;
            }

            fontAsset.glyphTable.Clear();
            fontAsset.characterTable.Clear();

            Dictionary<uint, uint> indexOf = new();
            Dictionary<char, uint> indexOfChar = new();

            var glyphData = asset.GlyphData;

            AddCharacterData(fontAsset, shareToUpper, shareToLower, indexOf, indexOfChar, glyphData);
            AddKerningData(asset, fontAsset, indexOfChar);

            fontAsset.boldSpacing = 2 * asset.LineHeight;
            fontAsset.boldStyle = 1;

            //We need to set fields through serialization as these properties are not exposed
            var so = new SerializedObject(fontAsset);

            Vector2Int atlasSize = new(atlas.width, atlas.height);
            int padding = asset.AtlasPadding;
            padding = Mathf.Max(1, padding);
            int lineHeight = asset.LineHeight;

            TrySetInt(so, "m_AtlasWidth", atlasSize.x);
            TrySetInt(so, "m_AtlasHeight", atlasSize.y);
            TrySetInt(so, "m_AtlasPadding", padding);
            TrySetInt(so, "m_AtlasRenderMode", (int)GlyphRenderMode.RASTER);
            TrySetInt(so, "m_AtlasTextureIndex", 0);
            TrySetInt(so, "m_FaceInfo.m_UnitsPerEM", lineHeight);
            so.ApplyModifiedPropertiesWithoutUndo();

            static void TrySetInt(SerializedObject so, string prop, int value)
            {
                var p = so.FindProperty(prop);
                if (p != null) p.intValue = value;
                else
                {
                    Debug.LogError($"Couldn't find {prop} in TMP aset! TMP version is likely outdated.");
                }
            }

            if (created)
            {
                AssetDatabase.CreateAsset(fontAsset, outputPath);

                asset.SetBuiltFontAsset(fontAsset);
            }

            if (createdTexture)
            {
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);
            }

            if (createdMaterial)
            {
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }

            EditorUtility.SetDirty(asset);
            fontAsset.ReadFontAssetDefinition();

            TMPro_EventManager.ON_FONT_PROPERTY_CHANGED(true, fontAsset);

            if (PixelArtFontDirectory.Instance)
            {
                PixelArtFontDirectory.Instance.TryAdd(asset);
            }
            AssetDatabase.SaveAssets();
        }

        private static FaceInfo GetFaceInfo(PixelArtFontAsset asset)
        {
            int lineHeight = asset.LineHeight;

            FaceInfo faceInfo = new()
            {
                familyName = asset.FontFamilyName,
                styleName = "Regular",
                pointSize = lineHeight,
                lineHeight = lineHeight,
                scale = 1f,
                tabWidth = 4 * asset.SpaceWidth,

                ascentLine = asset.AscentLine,
                descentLine = asset.DescentLine,
                meanLine = asset.MeanLine,
                capLine = asset.CapLine,

                strikethroughOffset = asset.CapLine / 2,
                strikethroughThickness = asset.UnderlineThickness,
                underlineThickness = asset.UnderlineThickness,
                underlineOffset = -Mathf.Max(1, Mathf.Min(2, asset.DescentLine)),

                superscriptSize = 0.5f,
                superscriptOffset = asset.CapLine,
                subscriptSize = 0.5f,
                subscriptOffset = -asset.DescentLine,
            };

            return faceInfo;
        }

        private static void AddCharacterData(TMP_FontAsset fontAsset, bool shareToUpper, bool shareToLower, 
            Dictionary<uint, uint> indexOf, Dictionary<char, uint> indexOfChar, List<GlyphData> glyphData)
        {
            uint indexOffset = 0;

            for (int i = 0; i < glyphData.Count; i++)
            {
                GlyphData g = glyphData[i];
                char c = g.Character;
                uint index = (uint)(i + 1) + indexOffset;
                RectInt atlasRect = g.AtlasRect;
                GlyphRect rect = new(new Rect(atlasRect.position, atlasRect.size));
                GlyphMetrics metrics = new(atlasRect.width, atlasRect.height, g.Bearing.x, g.Bearing.y,
                    g.Advance + g.AdvanceOffset);

                Glyph glyph = new(index, metrics, rect, 1f, 0);
                fontAsset.glyphTable.Add(glyph);

                TMP_Character character = new(g.Unicode, fontAsset, glyph);
                fontAsset.characterTable.Add(character);

                indexOf.Add(g.Unicode, index);
                indexOfChar.Add(g.Character, index);

                static uint AddOtherGlyph(TMP_FontAsset fontAsset, Dictionary<uint, uint> indexOf,
                    Dictionary<char, uint> indexOfChar, uint indexOffset, uint index, Glyph glyph, char upper)
                {
                    uint index2 = index + ++indexOffset;
                    uint unicode2 = upper;
                    TMP_Character c2 = new(unicode2, fontAsset, glyph);
                    if (!indexOf.TryAdd(unicode2, index2)) return --indexOffset;
                    indexOfChar.Add(upper, index2);
                    fontAsset.characterTable.Add(c2);
                    return indexOffset;
                }

                if (shareToUpper)
                {
                    char upper = char.ToUpper(c);
                    if (upper == c) continue;
                    indexOffset = AddOtherGlyph(fontAsset, indexOf, indexOfChar, indexOffset, index, glyph, upper);
                }
                else if (shareToLower)
                {
                    char lower = char.ToLower(c);
                    if (lower == c) continue;
                    indexOffset = AddOtherGlyph(fontAsset, indexOf, indexOfChar, indexOffset, index, glyph, lower);
                }
            }
        }

        private static void AddKerningData(PixelArtFontAsset asset, TMP_FontAsset fontAsset, Dictionary<char, uint> indexOfChar)
        {
            IReadOnlyList<PixelArtFontKerning> kerningData = asset.KerningData;
            List<GlyphPairAdjustmentRecord> pairs = fontAsset.fontFeatureTable.glyphPairAdjustmentRecords;
            pairs.Clear();

            for (int i = 0; i < kerningData.Count; i++)
            {
                PixelArtFontKerning kerning = kerningData[i];
                string pair = kerning.Pair;

                if (pair.Length != 2)
                {
                    Debug.LogError("Kerning pair has more than two characters! Aborting this kerning pair.");
                    continue;
                }

                (char a, char b) = (pair[0], pair[1]);

                if (!indexOfChar.TryGetValue(a, out uint indexA) ||
                    !indexOfChar.TryGetValue(b, out uint indexB))
                {
                    continue;
                }

                GlyphAdjustmentRecord adjA = new(indexA, new(0, 0, kerning.Delta, 0));
                GlyphAdjustmentRecord adjB = new(indexB, new(0, 0, 0, 0));
                pairs.Add(new(adjA, adjB));
            }
        }

        [MenuItem("Tools/Text/Rebuild All Pixel Art Font Assets")]
        public static void RebuildAllPixelArtFontAssets()
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(PixelArtFontAsset)}");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = AssetDatabase.LoadAssetAtPath<PixelArtFontAsset>(path);
                if (asset == null) continue;
                Build(asset);
            }
        }
    }
}
