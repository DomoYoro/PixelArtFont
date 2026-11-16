using UnityEditor;
using UnityEngine;

namespace Domo.PixelArtFont
{
    [CustomEditor(typeof(PixelArtFontDirectory))]
    public class PixelArtFontDirectoryEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var directory = (PixelArtFontDirectory)target;

            base.OnInspectorGUI();

            if (GUILayout.Button("Refresh Directory"))
            {
                directory.Clean();
            }
        }

        public void OnValidate()
        {
            var directory = (PixelArtFontDirectory)target;
            directory.Clean();
        }
    }
}
