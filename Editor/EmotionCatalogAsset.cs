#if UNITY_EDITOR
using UnityEditor;

namespace ZooJack.Editor
{
    /// <summary>
    /// 감정 카탈로그 에셋을 찾아 주는 곳.
    ///
    /// <b>왜 경로를 적어 두지 않는가.</b> 세 도구가 저마다 같은 경로 문자열
    /// ("Assets/ImportedAsset/Emotions/EmotionCatalog.asset")을 들고 있었다. 그런데
    /// 에셋은 그 뒤 Assets/Resources로 옮겨 갔고, 문자열은 그대로 남아 빈손을
    /// 돌려주기 시작했다. 도구들은 그 빈손을 <b>씬에 그대로 써 넣어</b> 멀쩡히
    /// 이어져 있던 참조를 지웠다 — 로비의 감정표현이 통째로 보이지 않게 된 이유다.
    ///
    /// 종류로 찾으면 에셋이 어디로 옮겨 가든 따라간다.
    /// </summary>
    internal static class EmotionCatalogAsset
    {
        /// <summary>못 찾으면 null. 부르는 쪽은 <b>이 null을 씬에 써 넣지 말아야 한다.</b></summary>
        public static EmotionCatalog Find()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(EmotionCatalog)))
            {
                var catalog = AssetDatabase.LoadAssetAtPath<EmotionCatalog>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (catalog != null) return catalog;
            }

            return null;
        }
    }
}
#endif
