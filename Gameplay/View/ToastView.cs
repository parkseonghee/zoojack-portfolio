using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 화면 한가운데 잠깐 떴다 사라지는 알림. 버튼도 배경 상자도 없다 —
    /// 누를 것이 없는 소식은 읽고 지나가면 그만이라 진행을 멈춰 세울 이유가 없다.
    ///
    /// 캔버스 맨 끝에 두어 어떤 패널 위에도 뜬다. 패널이 바뀌어도 살아 있으므로
    /// 화면 전환과 겹쳐 사라지는 것까지 자연스럽다.
    /// </summary>
    public class ToastView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI body;

        [SerializeField] private float fadeInDuration = 0.16f;
        [SerializeField] private float fadeOutDuration = 0.4f;

        [Tooltip("떠오를 때 살짝 밀려 올라오는 높이(px). 0이면 제자리에서 나타난다.")]
        [SerializeField] private float riseDistance = 26f;

        private Sequence playing;
        private Vector2 home;
        private bool homeCaptured;

        private RectTransform Rect => (RectTransform)transform;
        private CanvasGroup Group =>
            group != null ? group : (group = GetComponent<CanvasGroup>());

        /// <summary>
        /// 알림을 띄운다. <paramref name="holdSeconds"/> 동안 머문 뒤 저절로 사라진다.
        /// 이미 떠 있으면 새 내용으로 갈아 끼운다 — 알림이 쌓이면 읽을 수 없다.
        /// </summary>
        public void Show(string titleText, string bodyText, float holdSeconds)
        {
            CaptureHome();

            if (title != null) title.text = titleText;
            if (body != null)
            {
                bool hasBody = !string.IsNullOrEmpty(bodyText);
                if (body.gameObject.activeSelf != hasBody) body.gameObject.SetActive(hasBody);
                if (hasBody) body.text = bodyText;
            }

            playing?.Kill();
            gameObject.SetActive(true);

            Group.alpha = 0f;
            Rect.anchoredPosition = home - new Vector2(0f, riseDistance);

            playing = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            playing.Append(Group.DOFade(1f, fadeInDuration).SetEase(Ease.OutQuad));
            playing.Join(Rect.DOAnchorPos(home, fadeInDuration).SetEase(Ease.OutCubic));
            playing.AppendInterval(Mathf.Max(0f, holdSeconds - fadeInDuration));
            playing.Append(Group.DOFade(0f, fadeOutDuration).SetEase(Ease.InQuad));
            playing.OnComplete(() => gameObject.SetActive(false));
        }

        /// <summary>즉시 치운다. 라운드가 넘어갈 때처럼 알림이 뒤늦게 떠 있으면 곤란한 순간용.</summary>
        public void Hide()
        {
            playing?.Kill();
            playing = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // 뜨는 연출이 anchoredPosition을 건드리므로 기준점을 처음 한 번만 붙잡아 둔다.
        // 매번 현재 위치를 기준 삼으면 알림이 뜰 때마다 조금씩 아래로 밀려 내려간다.
        private void CaptureHome()
        {
            if (homeCaptured) return;
            home = Rect.anchoredPosition;
            homeCaptured = true;
        }

        private void OnDisable()
        {
            playing?.Kill();
            playing = null;
        }
    }
}
