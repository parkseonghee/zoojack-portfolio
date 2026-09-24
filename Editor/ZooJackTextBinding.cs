using TMPro;
using UnityEditor;

namespace ZooJack.Editor
{
    /// <summary>
    /// 씬의 고정 TMP를 String Table 항목에 묶는 한 줄짜리 일.
    ///
    /// <b>왜 따로 두는가.</b> 화면을 조립하는 도구가 여럿인데(환경설정, 방 화면, …) 저마다
    /// 같은 세 줄을 적어 두면 <see cref="ZooJackLocalizedText"/>의 필드 이름이 바뀌는 날
    /// 고쳐야 할 곳이 도구 수만큼 늘어난다. 묶는 방법은 하나여야 한다.
    /// </summary>
    public static class ZooJackTextBinding
    {
        /// <summary>
        /// <paramref name="target"/>에 <paramref name="key"/>를 물리고 지금 글자도 맞춘다.
        /// <paramref name="fallback"/>은 씬 미리보기이자 표가 없을 때의 안전망이다.
        /// </summary>
        public static void Bind(TMP_Text target, string key, string fallback)
        {
            if (target == null) return;
            target.text = fallback;

            var binding = target.GetComponent<ZooJackLocalizedText>();
            if (binding == null) binding = target.gameObject.AddComponent<ZooJackLocalizedText>();

            var so = new SerializedObject(binding);
            so.FindProperty("key").stringValue = key;
            so.FindProperty("fallback").stringValue = fallback;
            so.FindProperty("target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
