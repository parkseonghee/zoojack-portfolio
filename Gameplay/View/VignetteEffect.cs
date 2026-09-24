using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 화면 가장자리(모서리)에만 색이 깔리는 비네트 오버레이.
    /// 딜러가 조작 카드를 중앙에 두면 검붉은색, 공정 카드면 초록색으로 페이드한다.
    /// 방사형 그라데이션 스프라이트를 런타임에 생성하므로 별도 텍스처 에셋이 필요 없다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class VignetteEffect : MonoBehaviour
    {
        [SerializeField] private Color fairColor        = new Color(0.10f, 0.85f, 0.20f);
        [SerializeField] private Color manipulatedColor = new Color(0.70f, 0.02f, 0.02f);
        [SerializeField] private Color suspenseColor    = new Color(0.45f, 0.02f, 0.06f);
        [SerializeField] private float fadeDuration = 0.35f;
        [SerializeField] private float maxAlpha = 0.72f;

        private Image image;
        private Tween tween;

        // 부모가 비활성인 상태에서 접근돼도 안전하도록 지연 초기화.
        private Image Img => image != null ? image : (image = GetComponent<Image>());

        private void Awake()
        {
            Img.raycastTarget = false;
            if (Img.sprite == null) Img.sprite = BuildRadialSprite();
            SetAlpha(0f);
        }

        public void ShowFair()        => FadeIn(fairColor);
        public void ShowManipulated() => FadeIn(manipulatedColor);

        /// <summary>
        /// 고발 판정 대기 연출. 검붉은 가장자리가 점점 빠르게 명멸하며 긴장감을 끌어올린다.
        /// </summary>
        public void PlaySuspense(float duration)
        {
            tween?.Kill();
            SetColorKeepAlpha(suspenseColor);

            var seq = DOTween.Sequence().SetUpdate(true);
            float elapsed = 0f;
            float interval = 0.30f;
            while (elapsed < duration)
            {
                seq.Append(Img.DOFade(0.62f, interval * 0.42f));
                seq.Append(Img.DOFade(0.14f, interval * 0.58f));
                elapsed += interval;
                interval = Mathf.Max(0.08f, interval * 0.82f); // 점점 빨라진다
            }
            tween = seq;
        }

        /// <summary>판정 확정 순간의 섬광. 조작이 드러나면 초록, 딜러가 빠져나가면 붉은색.</summary>
        public void FlashResult(bool exposed)
        {
            tween?.Kill();
            SetColorKeepAlpha(exposed ? fairColor : manipulatedColor);
            tween = DOTween.Sequence().SetUpdate(true)
                .Append(Img.DOFade(0.88f, 0.07f))
                .Append(Img.DOFade(0f, 1.0f).SetEase(Ease.OutQuad));
        }

        private void SetColorKeepAlpha(Color color)
        {
            float a = Img.color.a;
            Img.color = new Color(color.r, color.g, color.b, a);
        }

        public void Hide()
        {
            tween?.Kill();
            tween = Img.DOFade(0f, fadeDuration).SetUpdate(true);
        }

        private void FadeIn(Color color)
        {
            tween?.Kill();
            SetColorKeepAlpha(color);
            tween = Img.DOFade(maxAlpha, fadeDuration).SetUpdate(true);
        }

        private void SetAlpha(float a)
        {
            var c = Img.color; c.a = a; Img.color = c;
        }

        private static Sprite BuildRadialSprite()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float maxDist = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    // 중앙 절반은 완전 투명, 가장자리로 갈수록 급격히 진해진다.
                    float a = Mathf.Clamp01((d - 0.5f) / 0.5f);
                    a *= a;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
