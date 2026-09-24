using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace ZooJack
{
    /// <summary>
    /// 씬의 고정 TMP 문구를 ZooJack String Table 항목에 연결한다.
    /// Inspector의 fallback은 씬 미리보기이자 테이블 누락 시 안전망이다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ZooJackLocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        [SerializeField, TextArea(1, 5)] private string fallback;
        [SerializeField] private TMP_Text target;

        public string Key => key;

        private void Reset()
        {
            target = GetComponent<TMP_Text>();
            fallback = target != null ? target.text : string.Empty;
        }

        private void OnEnable()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            Refresh();
        }

        private void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

        private void OnLocaleChanged(Locale _) => Refresh();

        public void Refresh()
        {
            if (target != null) target.text = ZooJackText.Get(key, fallback);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (target == null) target = GetComponent<TMP_Text>();
            if (!Application.isPlaying && target != null && !string.IsNullOrEmpty(fallback))
                target.text = fallback;
        }
#endif
    }
}
