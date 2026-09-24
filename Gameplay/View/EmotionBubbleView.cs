using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>Plate 위 앵커를 따라가며 감정 Sprite 하나를 재사용해 재생한다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image), typeof(CanvasGroup))]
    public sealed class EmotionBubbleView : MonoBehaviour
    {
        [SerializeField] private RectTransform anchor;
        [SerializeField] private Image image;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("크기와 이동")]
        [SerializeField, Min(0.01f)] private float startScale = 0.35f;
        [SerializeField, Min(1f)] private float overshootScale = 1.12f;
        [SerializeField, Min(0f)] private float riseDistance = 30f;

        // 앵커의 높이는 여기서 정하지 않는다. 그 자리에 행동 알약이 앉는지 아닌지는
        // 명패를 쥔 PlayerAvatarView만 알고, 비켜설 것이 있을 때만 비켜야 알약이 없는
        // 화면에서 말풍선이 이유 없이 떠오르지 않는다. 여기는 따라가기만 한다.
        [Header("시간")]
        [SerializeField, Min(0.01f)] private float popDuration = 0.2f;
        [SerializeField, Min(0f)] private float holdDuration = 1f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.45f;

        public bool IsPlaying => gameObject.activeSelf;
        public Sprite CurrentSprite => image != null ? image.sprite : null;

        private RectTransform rect;
        private float elapsed;

        private float TotalDuration => popDuration + holdDuration + fadeDuration;

        private void Awake() => EnsureReferences();

        private void LateUpdate()
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= TotalDuration)
            {
                HideImmediate();
                return;
            }

            ApplyFrame();
        }

        /// <summary>진행 중인 연출이 있어도 중단하고 새 Sprite로 처음부터 재생한다.</summary>
        public void Show(Sprite sprite)
        {
            EnsureReferences();
            if (sprite == null || anchor == null)
            {
                HideImmediate();
                return;
            }

            image.sprite = sprite;
            image.enabled = true;
            elapsed = 0f;
            gameObject.SetActive(true);
            ApplyFrame();
            GameAudio.PlayEmotionPop();
        }

        public void HideImmediate()
        {
            EnsureReferences();
            elapsed = 0f;
            canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void ApplyFrame()
        {
            float popT = Mathf.Clamp01(elapsed / popDuration);
            float scale;
            if (popT < 0.65f)
            {
                float expandT = Mathf.SmoothStep(0f, 1f, popT / 0.65f);
                scale = Mathf.Lerp(startScale, overshootScale, expandT);
            }
            else
            {
                float settleT = Mathf.SmoothStep(0f, 1f, (popT - 0.65f) / 0.35f);
                scale = Mathf.Lerp(overshootScale, 1f, settleT);
            }

            float fadeT = Mathf.Clamp01((elapsed - popDuration - holdDuration) / fadeDuration);
            canvasGroup.alpha = 1f - fadeT;
            rect.localScale = Vector3.one * scale;
            FollowAnchor(fadeT * riseDistance);
        }

        private void FollowAnchor(float rise)
        {
            if (anchor == null || rect.parent == null) return;
            Vector3 local = rect.parent.InverseTransformPoint(anchor.position);
            rect.anchoredPosition = new Vector2(local.x, local.y + rise);
            rect.localRotation = Quaternion.identity;
        }

        private void EnsureReferences()
        {
            if (rect == null) rect = GetComponent<RectTransform>();
            if (image == null) image = GetComponent<Image>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();

            image.preserveAspect = true;
            image.raycastTarget = false;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            startScale = Mathf.Max(0.01f, startScale);
            overshootScale = Mathf.Max(1f, overshootScale);
            riseDistance = Mathf.Max(0f, riseDistance);
            popDuration = Mathf.Max(0.01f, popDuration);
            holdDuration = Mathf.Max(0f, holdDuration);
            fadeDuration = Mathf.Max(0.01f, fadeDuration);
        }
#endif
    }
}
