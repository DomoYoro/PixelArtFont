using UnityEngine;
using UnityEditor;

namespace Domo.PixelArtFont
{
    public class PixelArtFontBuilderWindow : EditorWindow
    {
        [MenuItem("Window/PixelArtFont/Pixel Art Font Builder")]
        private static void OpenWindow()
        {
            GetWindow<PixelArtFontBuilderWindow>().Show();
        }

        private PixelArtFontAsset mFontAsset;
        private Vector2 mPropertiesScroll = Vector2.zero;
        private int mSheetZoom = 2;
        private int mAtlasZoom = 2;
        private int mSelectedIndex = 0;

        private const float kVerticalSpace = 5f;

        private void OnGUI()
        {
            Rect windowRect = new(0, 0, position.width, position.height);

            const float kHeaderHeight = 60f;
            DrawHeader(new Rect(0, 0, position.width, kHeaderHeight));

            if (mFontAsset == null) return;

            Rect contentRect = windowRect;
            float headerSpace = kHeaderHeight + kVerticalSpace;
            contentRect.position += new Vector2(0, headerSpace);
            contentRect.height -= headerSpace;

            const float kWidthRatio = 0.5f;
            Rect leftRect = new(
                contentRect.position, 
                new(kWidthRatio * contentRect.width, contentRect.height));
            Rect rightRect = new(
                contentRect.position + new Vector2(leftRect.width, 0), 
                new((1 - kWidthRatio) * contentRect.width, contentRect.height));

            DrawSheet(rightRect);

            DrawProperties(leftRect);
        }

        private void DrawHeader(Rect rect)
        {
            GUILayout.BeginHorizontal(GUILayout.Height(rect.height));
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            var obj = EditorGUILayout.ObjectField(mFontAsset, typeof(PixelArtFontAsset), allowSceneObjects: false);
            if (obj is PixelArtFontAsset asset && asset != mFontAsset)
            {
                mFontAsset = asset;
            }
            if (mFontAsset == null)
            {
                var selection = Selection.activeObject;
                if (selection != null && selection is PixelArtFontAsset p)
                {
                    mFontAsset = p;
                }
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();

            if (mFontAsset == null)
            {
                GUILayout.EndHorizontal();
                return;
            }

            if (GUILayout.Button("Build", GUILayout.Height(60)))
            {
                PixelArtFontBuilder.Build(mFontAsset);
            }

            if (GUILayout.Button("Rebuild Data", GUILayout.Height(60)))
            {
                mFontAsset.RebuildData();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawProperties(Rect rect)
        {
            float lw = EditorGUIUtility.labelWidth;
            bool wide = EditorGUIUtility.wideMode;
            EditorGUIUtility.labelWidth = 128f;
            EditorGUIUtility.wideMode = true;

            GUILayout.BeginArea(rect);
            mPropertiesScroll = GUILayout.BeginScrollView(mPropertiesScroll);
            if (mFontAsset != null)
            {
                EditorGUI.BeginChangeCheck();
                SerializedObject so = new(mFontAsset);

                void PropertyField(string propName)
                {
                    var prop = so.FindProperty(propName);
                    if (prop == null) return;
                    EditorGUILayout.PropertyField(prop);
                }

                void RelativePropertyField(SerializedProperty prop, string propName)
                {
                    EditorGUILayout.PropertyField(prop.FindPropertyRelative(propName));
                }

                PropertyField("m_Texture");
                
                PropertyField("m_CellSize");
                PropertyField("m_CellSpacing");
                PropertyField("m_CharacterList");
                PropertyField("m_CharacterListIsSharedBetweenCases");
                PropertyField("m_BlitMaterials");
                PropertyField("m_AtlasPadding");
                
                PropertyField("m_GlyphSpacing");
                PropertyField("m_LineHeight");
                PropertyField("m_LineGap");
                PropertyField("m_Baseline");
                PropertyField("m_SpaceWidth");
                PropertyField("m_FontFamilyName");
                EditorGUILayout.Space();

                int count = mFontAsset.GlyphData.Count;
                if (count > 0)
                {
                    mSelectedIndex = Mathf.Clamp(mSelectedIndex, 0, count - 1);

                    var glyphProp = so.FindProperty("m_GlyphData").GetArrayElementAtIndex(mSelectedIndex);

                    GUILayout.Label(new GUIContent("Selected Glyph Data"), EditorStyles.boldLabel);

                    EditorGUILayout.BeginHorizontal();
                    RelativePropertyField(glyphProp, "Character");
                    RelativePropertyField(glyphProp, "Unicode");
                    EditorGUILayout.EndHorizontal();
                    RelativePropertyField(glyphProp, "Advance");
                    RelativePropertyField(glyphProp, "TextureRect");

                    RelativePropertyField(glyphProp, "Bearing");
                    RelativePropertyField(glyphProp, "AdvanceOffset");

                    RelativePropertyField(glyphProp, "AtlasRect");

                    EditorGUILayout.Space();
                }
                PropertyField("m_KerningData");

                if (EditorGUI.EndChangeCheck())
                {
                    so.ApplyModifiedProperties();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();

            EditorGUIUtility.labelWidth = lw;
            EditorGUIUtility.wideMode = wide;
        }

        private void DrawSheet(Rect rect)
        {
            Rect sheetRect = rect;
            sheetRect = Expand(sheetRect, -kVerticalSpace * Vector2.one);
            Texture2D tex = mFontAsset.Texture;

            static Rect Expand(Rect rect, Vector2 expansion)
            {
                rect.position -= expansion;
                rect.size += 2 * expansion;
                return rect;
            }

            if (tex == null) return;

            Vector2 texSize = new(tex.width, tex.height);

            const float kSliderHeight = 20f;
            Rect sliderRect = new(sheetRect.position, new(sheetRect.width, kSliderHeight));
            mSheetZoom = EditorGUI.IntSlider(sliderRect, mSheetZoom, 1, 6);
            Vector2 diff = new(0, kSliderHeight + kVerticalSpace);
            sheetRect.position += diff;
            sheetRect.size -= diff;

            Material mat = new(Shader.Find("Sprites/Default"));
            Rect previewRect = new(sheetRect.position, texSize * mSheetZoom);

            GUILayout.BeginArea(previewRect);
            Rect textureRect = new(Vector2.zero, previewRect.size);

            EditorGUI.DrawPreviewTexture(textureRect, mFontAsset.Texture, mat, ScaleMode.ScaleToFit);
            HandleSheetClick(textureRect, mSheetZoom);

            var glyphData = mFontAsset.GlyphData;
            for (int i = 0; i < glyphData.Count; i++) 
            {
                GlyphData glyph = glyphData[i];
                RectInt glyphRect = glyph.TextureRect;
                float invertedHeight = texSize.y - (glyphRect.position.y + glyphRect.size.y);
                Rect invertedGlyphRect = new(new(glyphRect.position.x, invertedHeight), glyphRect.size);
                Color color = i == mSelectedIndex ? Color.orange : Color.cadetBlue;
                Rect scaledRect = Scale(invertedGlyphRect, mSheetZoom);
                DrawOutline(scaledRect, color);
            }

            static Rect Scale(Rect rect, float scale)
            {
                return new(rect.position * scale, rect.size * scale);
            }

            GUILayout.EndArea();

            diff = new(0, previewRect.size.y + kVerticalSpace);
            sheetRect.position += diff;
            sheetRect.size -= diff;

            if (mFontAsset.Atlas)
            {
                Rect sliderRect2 = new(sheetRect.position, new(sheetRect.width, kSliderHeight));
                mAtlasZoom = EditorGUI.IntSlider(sliderRect2, mAtlasZoom, 1, 6);
                diff = new(0, kSliderHeight + kVerticalSpace);
                sheetRect.position += diff;
                sheetRect.size -= diff;

                Texture2D atlas = mFontAsset.Atlas;
                Vector2 atlasSize = new(atlas.width, atlas.height);
                Rect atlasAreaRect = new(sheetRect.position, atlasSize * mAtlasZoom);
                Rect atlasRect = new(Vector2.zero, atlasAreaRect.size);
                GUILayout.BeginArea(atlasAreaRect);
                EditorGUI.DrawPreviewTexture(atlasRect, atlas, mat, ScaleMode.ScaleToFit);
                GUILayout.EndArea();
            }
        }

        private void HandleSheetClick(Rect rect, int zoom)
        {
            Event e = Event.current;
            if (e.type != EventType.MouseDown) return;
            if (e.button != 0) return;
            if (!rect.Contains(e.mousePosition)) return;

            Vector2 relative = new Vector2(
                e.mousePosition.x - rect.position.x,
                rect.height - e.mousePosition.y - rect.position.y) / zoom;

            var glyphData = mFontAsset.GlyphData;

            for (int i = 0; i < glyphData.Count; i++)
            {
                GlyphData glyph = glyphData[i];
                RectInt cell = glyph.TextureRect;
                if (cell.Contains(RoundToInt(relative)))
                {
                    mSelectedIndex = i;
                    e.Use();
                    Repaint();
                    return;
                }
            }
        }

        static Vector2Int RoundToInt(Vector2 v) => new(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));

        private static void DrawOutline(Rect r, Color c, float lineWidth = 1f)
        {
            EditorGUI.DrawRect(new(r.x, r.y, r.size.x, lineWidth), c);
            EditorGUI.DrawRect(new(r.x, r.y, lineWidth, r.size.y), c);
            EditorGUI.DrawRect(new(r.x, r.yMax, r.size.x, lineWidth), c);
            EditorGUI.DrawRect(new(r.xMax, r.y, lineWidth, r.size.y), c);
        }
    }
}