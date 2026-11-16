using TMPro;
using UnityEngine;

namespace Domo.PixelArtFont
{
    [ExecuteAlways]
    public class TextPixelSnap : MonoBehaviour
    {
        [SerializeField]
        private TMP_Text m_Text;

        [SerializeField]
        private bool m_CacheData;


        TMP_TextInfo mTextInfo;
        TMP_MeshInfo[] mCachedMeshInfo;


        private void OnEnable()
        {
            m_Text.OnPreRenderText += SnapToNearestPixel;
            m_Text.ForceMeshUpdate();

            if (m_CacheData)
            {
                mTextInfo = m_Text.textInfo;
                mCachedMeshInfo = mTextInfo.CopyMeshInfoVertexData();
            }
        }

        private void OnDisable()
        {
            m_Text.OnPreRenderText -= SnapToNearestPixel;
            m_Text.ForceMeshUpdate();
        }


#if UNITY_EDITOR
        private void OnValidate()
        {
            if (m_Text) m_Text.ForceMeshUpdate();
        }
#endif

        private void SnapToNearestPixel(TMP_TextInfo textInfo)
        {
            GetScreenOffset(out Vector2 origin, out Vector2 pixelsPerUnit);

            Vector3 offset = origin;

            if (offset == Vector3.zero)
            {
                return;
            }

            TMP_CharacterInfo[] chars = textInfo.characterInfo;
            TMP_MeshInfo[] meshInfo = m_CacheData ? mCachedMeshInfo : textInfo.CopyMeshInfoVertexData();

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                var charInfo = chars[i];

                if (!charInfo.isVisible) continue;

                int vIndex = charInfo.vertexIndex;
                int materialIndex = charInfo.materialReferenceIndex;

                Vector3[] sourceVerticies = meshInfo[materialIndex].vertices;
                Vector3[] destinationVerticies = textInfo.meshInfo[materialIndex].vertices;

                for (int j = 0; j < 4; j++)
                {
                    destinationVerticies[vIndex + j] = sourceVerticies[vIndex + j] + offset;
                }
            }


            //Pushing changes to meshes
            for (int i = 0; i < textInfo.meshInfo.Length; i++)
            {
                textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                m_Text.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
            }
        }

        private void GetScreenOffset(out Vector2 origin, out Vector2 pixelsPerUnit)
        {
            origin = Vector2.zero;
            pixelsPerUnit = Vector2.one;

            Canvas canvas = m_Text.canvas;

            if (canvas == null) return;

            Canvas root = canvas.rootCanvas;
            Transform t = m_Text.transform;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceCamera ? root.worldCamera : null;

            bool isCardinal = IsCardinal(t.right) && IsCardinal(t.up);

            static bool IsCardinal(Vector2 v, float angleTolerance = 1)
            {
                v = v.normalized;
                float tolerance = Mathf.Sin(angleTolerance);
                return Mathf.Abs(v.x) < tolerance || Mathf.Abs(v.y) < tolerance;
            }

            if (!isCardinal) return;

            //Assuming square pixels bc that's how SPP is designed
            origin = RectTransformUtility.WorldToScreenPoint(camera, t.position);
            pixelsPerUnit = RectTransformUtility.WorldToScreenPoint(camera, t.up) - origin;
        }
    }
}
