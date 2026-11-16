using UnityEditor;
using UnityEngine;

namespace Domo.PixelArtFont
{
    //Responsible for manually updating font atlas assets
    public class PixelArtFontAssetPostProcessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, 
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!PixelArtFontDirectory.Instance) return;

            foreach (var assetPath in importedAssets)
            {
                if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) != typeof(Texture2D))
                {
                    continue;
                }
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                var fontAssets = PixelArtFontDirectory.Instance[texture];
                if (fontAssets == null) continue;

                for (int i = 0; i < fontAssets.Count; i++) 
                {
                    var fontAsset = fontAssets[i];
                    Texture2D sourceTex = fontAsset.Texture;
                    Texture2D atlasTex = fontAsset.Atlas;
                    if (sourceTex == null) continue;
                    if (atlasTex == null) continue;

                    if (sourceTex.imageContentsHash != atlasTex.imageContentsHash)
                    {
                        PixelArtFontBuilder.Build(fontAsset);
                    }
                }
            }
        }
    }
}
