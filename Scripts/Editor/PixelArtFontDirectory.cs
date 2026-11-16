using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Domo.PixelArtFont
{
    /// <summary>
    /// Maintains a reference to the analyzer computer shader as well as every pixel art font asset; this is
    /// done so that texture imports can quickly notify the appropriate pixel art font asset for update.
    /// </summary>
    [CreateAssetMenu(menuName = "Text/Pixel Art Font Directory")]
    public class PixelArtFontDirectory : ScriptableObject
    {
        private static PixelArtFontDirectory mInstance;
        public static PixelArtFontDirectory Instance
        {
            get
            {
                if (mInstance) return mInstance;

                const string kDefaultPath = "Assets/";
                string[] searchInFolders = new string[1] { kDefaultPath };
                static PixelArtFontDirectory TryLoad(string path) => 
                    AssetDatabase.LoadAssetAtPath<PixelArtFontDirectory>(path);

                string type = typeof(PixelArtFontDirectory).ToString();
                if (EditorPrefs.HasKey(type)) //Last path of singleton asset is cached for the editor
                {
                    string path = EditorPrefs.GetString(type);
                    mInstance = TryLoad(path);

                    //AssetDatabase.ImportAsset does not work if we can't load it directly
                }

                if (mInstance) return mInstance;

                string filter = $"t:{type}";
                var assets = AssetDatabase.FindAssets(filter, searchInFolders);
                string guid = assets.FirstOrDefault();
                if (guid != null)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    mInstance = TryLoad(assetPath);
                    EditorPrefs.SetString(type, assetPath); //Last path is cached for the editor
                }
                else
                {
                    Debug.LogError("Couldn't find asset!");
                }

                if (mInstance) return mInstance;

                var objs = Resources.FindObjectsOfTypeAll<PixelArtFontDirectory>();
                mInstance = objs.FirstOrDefault();

                return mInstance;
            }
        }

        [Serializable]
        public class TextureToFontAsset
        {
            public Texture2D Texture;
            public PixelArtFontAsset Asset;

            public TextureToFontAsset(Texture2D texture, PixelArtFontAsset asset)
            {
                Texture = texture;
                Asset = asset;
            }
        }

        [SerializeField]
        private ComputeShader m_AnalyzerComputeShader;
        public ComputeShader AnalyzerComputeShader { get { return m_AnalyzerComputeShader; } }

        [SerializeField, ReadOnly]
        private List<TextureToFontAsset> m_SerializedList = new();


        private readonly Dictionary<Texture2D, List<PixelArtFontAsset>> mTextureToFontAsset = new();
        private readonly Dictionary<PixelArtFontAsset, Texture2D> mFontAssetToTexture = new();

        public List<PixelArtFontAsset> this[Texture2D key]
        {
            get
            {
                LazyInstantiate();

                if (mTextureToFontAsset.TryGetValue(key, out var assets))
                {
                    return assets;
                }
                return null;
            }
        }


        public bool TryAdd(PixelArtFontAsset asset)
        {
            Texture2D texture = asset.Texture;
            if (texture == null) return false;

            LazyInstantiate();

            if (mFontAssetToTexture.TryGetValue(asset, out var texture2) && texture2 != texture)
            {
                var collection = mTextureToFontAsset[texture];
                collection.Remove(asset);
                if (collection.Count == 0)
                {
                    mTextureToFontAsset.Remove(texture);
                }
                mFontAssetToTexture.Remove(asset);
                m_SerializedList.RemoveAll(ttfa => ttfa.Asset == asset);
            }
            else if (texture2 == texture)
            {
                return false;
            }

            m_SerializedList.Add(new(texture, asset));

            if (mTextureToFontAsset.TryGetValue(texture, out var assets))
            {
                assets.Add(asset);
            }
            else
            {
                mTextureToFontAsset.Add(texture, new() { asset });
            }
            mFontAssetToTexture.Add(asset, texture);

            EditorUtility.SetDirty(this);
            return true;
        }

        public static bool TryAdd_Static(PixelArtFontAsset asset)
        {
            if (!Instance) return false;
            return Instance.TryAdd(asset);
        }

        private void LazyInstantiate()
        {
            if (m_SerializedList.Count > 0 &&
                mTextureToFontAsset.Count > 0 &&
                mFontAssetToTexture.Count > 0)
            {
                return;
            }

            mTextureToFontAsset.Clear();
            mFontAssetToTexture.Clear();

            for (int i = 0; i < m_SerializedList.Count; i++)
            {
                TextureToFontAsset ttfa = m_SerializedList[i];
                if (mTextureToFontAsset.TryGetValue(ttfa.Texture, out var assets))
                {
                    assets.Add(ttfa.Asset);
                }
                else
                {
                    mTextureToFontAsset.Add(ttfa.Texture, new() { ttfa.Asset });
                }
                mFontAssetToTexture.Add(ttfa.Asset, ttfa.Texture);
            }
        }

        public void Clean()
        {
            for (int i = 0; i < m_SerializedList.Count; i++)
            {
                var ttfa = m_SerializedList[i];
                if (ttfa.Texture == null)
                {
                    m_SerializedList.RemoveAt(i);
                    mFontAssetToTexture.Remove(ttfa.Asset);
                }
                else if (ttfa.Asset == null)
                {
                    m_SerializedList.RemoveAt(i);
                    mFontAssetToTexture.Remove(ttfa.Asset);
                    if (mTextureToFontAsset.TryGetValue(ttfa.Texture, out var assets))
                    {
                        assets.Remove(ttfa.Asset);
                        if (assets.Count == 0)
                        {
                            mTextureToFontAsset.Remove(ttfa.Texture);
                        }
                    }
                }
            }
        }
    }
}
