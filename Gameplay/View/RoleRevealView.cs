using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>첫 역할 배정과 역할 교대 때 로컬 플레이어에게 표시하는 전체 화면 공개 연출.</summary>
    public sealed class RoleRevealView : MonoBehaviour
    {
        public const float DisplaySeconds = 1.8f;

        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform revealCard;
        [SerializeField] private Image portrait;
        [SerializeField] private TextMeshProUGUI eyebrow;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI description;
        [SerializeField] private Image roleAccent;

        private Sequence sequence;

        public bool IsShowing => gameObject.activeSelf;

        public void Show(PlayerRole role, Sprite portraitSprite, Color ownerColor, Action onComplete = null)
        {
            sequence?.Kill();
            sequence = null;

            if (portrait != null)
            {
                portrait.sprite = portraitSprite;
                portrait.enabled = portraitSprite != null;
            }
            if (eyebrow != null)
                eyebrow.text = ZooJackText.Get("RoleReveal.Eyebrow", "새로운 역할");
            if (title != null)
            {
                title.text = ZooJackText.Get(
                    "RoleReveal.Title", "당신은 {0}입니다", RoleName(role));
                title.color = ownerColor;
            }
            if (description != null) description.text = RoleDescription(role);
            if (roleAccent != null) roleAccent.color = ownerColor;

            // 카드가 떠오르는 순간. 이 함수는 역할 배정 버전이 바뀔 때만 불리므로
            // (핫시트·네트워크 양쪽 다) 한 번의 공개에 한 번만 울린다.
            GameAudio.PlayRoleReveal();

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.blocksRaycasts = true;
            }
            if (revealCard != null) revealCard.localScale = Vector3.one * 0.88f;

            sequence = DOTween.Sequence().SetUpdate(true);
            if (canvasGroup != null) sequence.Append(canvasGroup.DOFade(1f, 0.22f));
            if (revealCard != null) sequence.Join(revealCard.DOScale(1f, 0.32f).SetEase(Ease.OutBack));
            sequence.AppendInterval(Mathf.Max(0.8f, DisplaySeconds - 0.47f));
            if (canvasGroup != null) sequence.Append(canvasGroup.DOFade(0f, 0.25f));
            sequence.OnComplete(() =>
            {
                sequence = null;
                gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }

        public void HideImmediate()
        {
            sequence?.Kill();
            sequence = null;
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            if (sequence == null) return;
            sequence.Kill();
            sequence = null;
        }

        public static string RoleName(PlayerRole role) => ZooJackText.RoleName(role);

        public static string RoleDescription(PlayerRole role) => role switch
        {
            PlayerRole.PlayerA => ZooJackText.Get("RoleReveal.PlayerA.Description",
                "카드를 확인하고 판돈을 걸어 플레이어 B를 이기세요."),
            PlayerRole.PlayerB => ZooJackText.Get("RoleReveal.PlayerB.Description",
                "카드를 확인하고 판돈을 걸어 플레이어 A를 이기세요."),
            PlayerRole.Dealer => ZooJackText.Get("RoleReveal.Dealer.Description",
                "비공개 뇌물을 확인하고 두 플레이어에게 카드를 배분하세요."),
            _ => ZooJackText.Get("RoleReveal.Spectator.Description",
                "라운드의 진행을 지켜보세요.")
        };
    }
}
