using DG.Tweening;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 결과를 알리는 카드가 화면에 올라오는 방식.
    ///
    /// 겉보기 승리·최종 판정·매치 종료는 <b>같은 규격의 카드</b>(640×220)를 쓴다.
    /// 그러면 등장도 같아야 한다 — 하나만 툭 나타나면 같은 종류의 알림으로 읽히지 않는다.
    /// 원래 최종 판정 카드에만 있던 연출을 여기로 옮겨, 세 화면이 이 값을 함께 본다.
    ///
    /// 살짝 작게 시작해 튀어 오르며 제 크기가 된다(<see cref="Ease.OutBack"/>).
    /// 투명도가 아니라 크기를 쓰는 이유: 반투명하게 지나가는 동안 글자가 읽히다 말면
    /// 눈이 먼저 지친다. 크기는 끝까지 또렷한 채로 자리만 잡는다.
    /// </summary>
    public static class ResultCardPop
    {
        /// <summary>시작 크기. 1보다 조금만 작아야 한다 — 많이 줄이면 튀어나오는 느낌이 과해진다.</summary>
        public const float FromScale = 0.9f;

        /// <summary>제 크기가 되기까지의 시간(초).</summary>
        public const float Duration = 0.34f;

        /// <summary>카드를 튀어 오르며 띄운다.</summary>
        public static void Play(GameObject card)
        {
            if (card == null) return;

            card.SetActive(true);

            var t = card.transform;
            t.DOKill();
            t.localScale = Vector3.one * FromScale;
            t.DOScale(Vector3.one, Duration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);   // 연출 중 timeScale이 흔들려도 같은 속도로 뜬다
        }

        /// <summary>
        /// 연출 없이 그대로 띄운다. 이미 본 결과를 화면 갱신 때문에 다시 그리는 경우다 —
        /// 같은 카드가 갱신마다 다시 튀어 오르면 화면이 들썩인다.
        /// </summary>
        public static void ShowInstant(GameObject card)
        {
            if (card == null) return;

            card.SetActive(true);
            Settle(card.transform);
        }

        /// <summary>카드를 내린다. 크기를 되돌려 두지 않으면 다음에 뜰 때 작게 시작한다.</summary>
        public static void Hide(GameObject card)
        {
            if (card == null) return;

            Settle(card.transform);
            card.SetActive(false);
        }

        /// <summary>연출만 끝내고 크기를 되돌린다. 카드는 켜 둔 채로 둔다.</summary>
        public static void Settle(Transform card)
        {
            if (card == null) return;

            card.DOKill();
            card.localScale = Vector3.one;
        }
    }
}
