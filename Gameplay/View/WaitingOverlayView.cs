using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 화면 한가운데를 덮는 기다림 안내. 확인 버튼이 있어 눌러야 넘어간다(핫시트 인계).
    ///
    /// <b>우상단에 붙던 작은 대기 상자는 없앴다.</b> 같은 말을 세 군데가 하고 있었다 —
    /// 좌상단 단계 표시줄, 우상단 대기 상자, 그리고 캐릭터 머리 위 명패. 남은 둘이 더
    /// 정확하다: 표시줄은 <b>무슨 단계인지</b>를, 명패는 <b>누구를</b> 기다리는지를 그
    /// 사람 머리 위에서 말한다. 대기 상자는 둘 다 하지 못하면서 판의 한 귀퉁이만 먹었다.
    ///
    /// 배치는 씬에 저장된 패널을 그대로 쓰고, 런타임에는 문구만 바꾼다.
    /// </summary>
    public class WaitingOverlayView : MonoBehaviour
    {
        [Header("화면 중앙(확인 버튼 있음)")]
        [SerializeField] private GameObject coverPanel;
        [SerializeField] private TextMeshProUGUI coverTitle;
        [SerializeField] private TextMeshProUGUI coverDescription;
        [SerializeField] private RectTransform coverHourglass;

        [Header("Hourglass Animation")]
        [SerializeField, Min(0f)] private float spinDuration = 0.55f;
        [SerializeField, Min(0f)] private float spinCooldown = 1.15f;

        private Sequence hourglassSequence;
        private RectTransform activeHourglass;
        private HourglassLoadingGraphic activeHourglassGraphic;

        public void Show(string title, string description)
        {
            if (coverPanel != null) coverPanel.SetActive(true);

            EnsureCharacters(coverTitle, title);
            EnsureCharacters(coverDescription, description);
            if (coverTitle != null) coverTitle.text = title;

            if (coverDescription != null)
            {
                bool hasDescription = !string.IsNullOrEmpty(description);
                if (coverDescription.gameObject.activeSelf != hasDescription)
                    coverDescription.gameObject.SetActive(hasDescription);
                if (hasDescription) coverDescription.text = description;
            }

            PlayHourglass(coverHourglass);
        }

        private static void EnsureCharacters(TextMeshProUGUI label, string text)
        {
            if (label == null || label.font == null || string.IsNullOrEmpty(text)) return;
            label.font.TryAddCharacters(text, out _);
        }

        private void OnDisable()
        {
            StopHourglass();
        }

        private void PlayHourglass(RectTransform hourglass)
        {
            StopHourglass();
            if (hourglass == null) return;

            activeHourglass = hourglass;
            activeHourglassGraphic = hourglass.GetComponent<HourglassLoadingGraphic>();
            hourglass.localRotation = Quaternion.identity;
            if (activeHourglassGraphic != null)
                activeHourglassGraphic.SandProgress = 0f;

            hourglassSequence = DOTween.Sequence();
            if (activeHourglassGraphic != null)
            {
                hourglassSequence.Append(DOTween.To(
                        () => activeHourglassGraphic.SandProgress,
                        value => activeHourglassGraphic.SandProgress = value,
                        1f,
                        spinCooldown)
                    .SetEase(Ease.Linear));
            }
            else
            {
                hourglassSequence.AppendInterval(spinCooldown);
            }

            hourglassSequence
                .Append(hourglass
                    .DOLocalRotate(new Vector3(0f, 0f, -180f), spinDuration, RotateMode.Fast)
                    .SetEase(Ease.InOutCubic))
                .AppendCallback(() =>
                {
                    hourglass.localRotation = Quaternion.identity;
                    if (activeHourglassGraphic != null)
                        activeHourglassGraphic.SandProgress = 0f;
                })
                .SetLoops(-1, LoopType.Restart)
                .SetUpdate(true)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        private void StopHourglass()
        {
            hourglassSequence?.Kill();
            hourglassSequence = null;
            if (activeHourglass != null)
                activeHourglass.localRotation = Quaternion.identity;
            if (activeHourglassGraphic != null)
                activeHourglassGraphic.SandProgress = 0f;
            activeHourglass = null;
            activeHourglassGraphic = null;
        }
    }
}
