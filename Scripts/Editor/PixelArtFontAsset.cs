using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Domo.PixelArtFont
{
    [Serializable]
    public class PixelArtFontKerning
    {
        public string Pair;
        public int Delta;
    }

    [Serializable]
    public class GlyphData
    {
        [SerializeField, ReadOnly]
        public char Character;
        [SerializeField, ReadOnly]
        public uint Unicode;
        [SerializeField, ReadOnly]
        public int Advance;
        [SerializeField, ReadOnly]
        public RectInt TextureRect;
        [SerializeField]
        public Vector2Int Bearing;
        [SerializeField]
        public int AdvanceOffset;
        [SerializeField, ReadOnly]
        public RectInt AtlasRect;
    }

    [CreateAssetMenu(menuName = "Text/Pixel Art Font Asset", order = -1)]
    public class PixelArtFontAsset : ScriptableObject
    {
        [Header("Texture Data")]
        [SerializeField]
        private Texture2D m_Texture;
        public Texture2D Texture { get { return m_Texture; } }

        [Space]

        [SerializeField]
        private Vector2Int m_CellSize = new(16, 16);
        public Vector2Int CellSize { get { return m_CellSize; } }

        [SerializeField, Multiline]
        private string m_CharacterList = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        public string CharacterList { get { return m_CharacterList; } }

        [SerializeField]
        [Tooltip("If set to true: when building the font, the builder will assign glyph data for the extra case" +
            "that points towards the exisiting case's texture data.")]
        private bool m_CharacterListIsSharedBetweenCases = false;
        public bool CharacterListIsSharedBetweenCases { get { return m_CharacterListIsSharedBetweenCases; } }

        [Space]

        [Header("Atlas Data")]
        [SerializeField]
        [Tooltip("Use this if you want to process the atlas data in any way, such as adding outlines or dropshadows.")]
        private List<Material> m_BlitMaterials;
        public IReadOnlyList<Material> BlitMaterials { get { return m_BlitMaterials; } }

        [SerializeField]
        [Tooltip("The padding surrounding a glyph texture rect. Must be set to at least 1, as that's what TMPro" +
            "expects. Only use values larger than 1 if you're using a blit material that expands the sprite" +
            "outwards, like adding an outline or a dropshadow.")]
        private int m_AtlasPadding = 1;
        public int AtlasPadding { get { return m_AtlasPadding; } }

        [Header("Font Data")]

        [SerializeField]
        [Tooltip("The spacing used between glyphs, in pixels.")]
        private int m_GlyphSpacing = 1;
        public int GlyphSpacing { get { return m_GlyphSpacing; } }

        [SerializeField]
        [Tooltip("The height of the font in pixels, from the top of the capital letter line down to the descender" +
            "line (extensions below the baseline), in pixels. This will also be the size of the font you use.")]
        private int m_LineHeight;
        public int LineHeight { get { return m_LineHeight; } }
        public void SetLineHeight(int value) => m_LineHeight = value;

        [SerializeField]
        [Tooltip("")]
        private int m_LineGap = 0;
        public int LineGap { get { return m_LineGap; } }

        [SerializeField]
        [Tooltip("The height at which glyphs start from, from within their cells.")]
        private int m_Baseline;
        public int Baseline { get { return m_Baseline; } }

        [SerializeField]
        [Tooltip("How long the glyph space is, in pixels. Most fonts have it at 1/4 em, but wide fonts " +
            "may use 1/3 em. Leave this at -1 to auto-calculate the space width.")]
        private int m_SpaceWidth = -1;
        public int SpaceWidth { get { return m_SpaceWidth; } }
        public void SetSpaceWidth(int value) => m_SpaceWidth = value;

        [SerializeField]
        private int m_UnderlineThickness = 1;
        public int UnderlineThickness { get { return m_UnderlineThickness; } }

        [Space]

        [SerializeField]
        [Tooltip("TextMeshPro's font family name; used to group fonts together.")]
        private string m_FontFamilyName = "Font";
        public string FontFamilyName { get { return m_FontFamilyName; } }

        [Space]
        [Header("Glyph Data")]

        [SerializeField]
        private List<GlyphData> m_GlyphData = new();
        public List<GlyphData> GlyphData { get { return m_GlyphData; } }

        [SerializeField]
        private List<PixelArtFontKerning> m_KerningData = new();
        public List<PixelArtFontKerning> KerningData { get { return m_KerningData; } }

        [Space]

        [Header("Generated Data")]
        [ReadOnly]
        [SerializeField]
        private int m_AscentLine;
        public int AscentLine { get { return m_AscentLine; } }
        public void SetAscentLine(int value) => m_AscentLine = value;

        [ReadOnly]
        [SerializeField]
        private int m_DescentLine;
        public int DescentLine { get { return m_DescentLine; } }
        public void SetDescentLine(int value) => m_DescentLine = value;

        [ReadOnly]
        [SerializeField]
        private int m_MeanLine;
        public int MeanLine { get { return m_MeanLine; } }
        public void SetMeanLine(int value) => m_MeanLine = value;

        [ReadOnly]
        [SerializeField]
        private int m_CapLine;
        public int CapLine { get { return m_CapLine; } }
        public void SetCapHeight(int value) => m_CapLine = value;



        [Space]

        [SerializeField]
        private TMP_FontAsset m_BuiltFontAsset;
        public TMP_FontAsset BuiltFontAsset { get { return m_BuiltFontAsset; } }
        public void SetBuiltFontAsset(TMP_FontAsset asset) => m_BuiltFontAsset = asset;

        [SerializeField]
        private Texture2D m_Atlas;
        public Texture2D Atlas { get { return m_Atlas; } }
        public void SetAtlasTexture(Texture2D texture) => m_Atlas = texture;

        public bool HasBuiltFontAsset { get { return BuiltFontAsset != null; } }

#if UNITY_EDITOR
        public void RebuildData()
        {
            PixelArtFontTextureProcessor.UpdateGlyphData(this);
            PixelArtFontDirectory.TryAdd_Static(this);
        }
#endif
    }
}
