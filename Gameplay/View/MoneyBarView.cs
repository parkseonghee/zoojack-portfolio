using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 머니바 한 칸. 인스펙터에서 사람마다 한 번 꽂아 두면 매치 내내 움직이지 않는다.
    ///
    /// <see cref="GameDirector"/> 안의 중첩 클래스였다. 밖으로 꺼내도 씬 데이터는 그대로다 —
    /// Unity는 <c>[Serializable]</c> 클래스를 YAML에 <b>필드 목록으로만</b> 적고 타입 이름을
    /// 남기지 않기 때문이다(<c>[SerializeReference]</c>였다면 달랐다).
    /// </summary>
    [Serializable]
    internal class MoneyCard
    {
        [Tooltip("이 칸의 주인. 사람은 매치 내내 바뀌지 않으므로 인스펙터에서 한 번만 정한다.")]
        public CharacterId Owner;

        public Image Portrait;

        [Tooltip("직군 라벨. 역할이 교대되면 이 텍스트 하나만 바뀐다.")]
        public TextMeshProUGUI RoleLabel;

        public TextMeshProUGUI Balance;

        [Tooltip("정산 예고(±N). 최종 판정에서 뜨고 '다음 라운드'에서 숫자에 흡수된다.")]
        public TextMeshProUGUI Delta;
        public RectTransform Root;
        public Outline LocalOutline;
        public GameObject SelfBadge;

        // ── 실행 중 상태 ─────────────────────────────────────────
        [NonSerialized] public int Shown;      // 지금 화면에 찍혀 있는 숫자
        [NonSerialized] public int Target;     // 모델이 들고 있는 진짜 잔액
        [NonSerialized] public bool HasShown;  // 첫 표시는 굴리지 않고 즉시 찍는다
        [NonSerialized] public bool Previewing;
        [NonSerialized] public int Pending;    // 붙잡아 둔 ±N. 승자 공개 뒤에 배지로 뜬다
        [NonSerialized] public Tween Roll;
        [NonSerialized] public PlayerRole LastRole;
        [NonSerialized] public bool HasRole;
        [NonSerialized] public bool PendingRolePulse;
    }

    /// <summary>
    /// 화면 위 머니바. <b>사람 자리</b>다 — 좌석마다 자기 칸이 있고 거기서 움직이지 않는다.
    /// 역할이 교대돼도 각자의 돈은 자기 칸에 그대로 남고, 바뀌는 것은 직군 라벨뿐이다.
    /// 역할 기준으로 칸을 잡으면 교대할 때마다 초상화와 숫자가 통째로 자리를 바꿔
    /// "내 돈이 어느 칸이지?"를 라운드마다 다시 찾아야 한다.
    ///
    /// <b>왜 별도 클래스인가.</b> 이 묶음은 어느 패널에도 속하지 않는다 — 뇌물·판돈·딜러·
    /// 결과 어느 화면이 떠 있든 위쪽에 그대로 있다. 그래서 패널 로직과 섞일 이유가 없는데도
    /// <see cref="GameDirector"/> 한가운데에 250줄로 박혀 있었다. 다루는 데이터
    /// (<see cref="MoneyCard"/> 배열)가 이 클래스 밖에서 읽히는 곳도 없었다.
    ///
    /// 핫시트와 네트워크가 <b>같은 이 경로</b>로 들어온다. 잔액을 어디서 알아내는지는
    /// 저마다 다르지만(로컬 딕셔너리 / FusionGameState), 어떻게 그릴지는 여기만 안다.
    /// </summary>
    internal sealed class MoneyBarView
    {
        // 잔액 숫자가 목표까지 굴러가는 시간(초).
        private const float RollDuration = 0.75f;

        private readonly MoneyCard[] cards;

        /// <summary>
        /// 초상화 조회. <see cref="GameDirector"/>가 스프라이트를 <c>[SerializeField]</c>로
        /// 들고 있고 역할 공개 연출에서도 같은 그림을 쓰므로, 그림을 이쪽으로 옮기는 대신
        /// 조회만 넘겨받는다.
        /// </summary>
        private readonly Func<CharacterId, Sprite> portraitFor;

        public MoneyBarView(MoneyCard[] cards, Func<CharacterId, Sprite> portraitFor)
        {
            this.cards = cards;
            this.portraitFor = portraitFor;
        }

        /// <summary>
        /// 한 사람의 칸을 갱신한다. 칸은 주인 캐릭터로 찾으므로 역할이 바뀌어도 같은 자리에
        /// 머문다.
        /// </summary>
        public void SetCard(CharacterId owner, PlayerRole role, int balance)
        {
            MoneyCard card = Find(owner);
            if (card == null) return;

            if (!card.HasRole || card.LastRole != role) card.PendingRolePulse = true;
            card.LastRole = role;
            card.HasRole = true;

            if (card.RoleLabel != null)
            {
                card.RoleLabel.text = RoleLabel(role);
                card.RoleLabel.color = ZooJackPalette.CharacterAccent(owner);
            }

            // 주인이 고정이라 그림도 고정이지만, 씬에 잘못 꽂혀 있어도 첫 갱신에서
            // 제자리를 찾도록 여기서 한 번 맞춰 둔다.
            Sprite sprite = portraitFor?.Invoke(owner);
            if (card.Portrait != null && sprite != null)
            {
                card.Portrait.sprite = sprite;
                card.Portrait.enabled = true;
            }

            card.Target = balance;

            // 예고 중에는 숫자를 건드리지 않는다. 모델은 이미 정산 후 값을 들고 있지만
            // 화면은 '다음 라운드'를 누를 때까지 정산 전 값에 머물러야 한다.
            if (!card.Previewing) ShowBalance(card, balance, animate: card.HasShown);
        }

        /// <summary>사람 고정 카드 중 현재 로컬 플레이어의 카드만 배지와 외곽선으로 강조한다.</summary>
        public void HighlightLocal(CharacterId localOwner)
        {
            if (cards == null) return;

            foreach (MoneyCard card in cards)
            {
                if (card == null) continue;
                bool isLocal = localOwner != CharacterId.None && card.Owner == localOwner;

                if (card.SelfBadge != null) card.SelfBadge.SetActive(isLocal);
                if (card.LocalOutline != null)
                {
                    card.LocalOutline.enabled = isLocal;
                    card.LocalOutline.effectColor = ZooJackPalette.CharacterAccent(card.Owner);
                    card.LocalOutline.effectDistance = new Vector2(2f, -2f);
                    card.LocalOutline.useGraphicAlpha = false;
                }

                if (!isLocal || !card.PendingRolePulse) continue;
                card.PendingRolePulse = false;
                RectTransform root = card.Root;
                if (root == null && card.RoleLabel != null)
                    root = card.RoleLabel.transform.parent as RectTransform;
                if (root == null) continue;

                root.DOKill();
                root.localScale = Vector3.one;
                root.DOPunchScale(new Vector3(0.07f, 0.07f, 0f), 0.48f, 3, 0.55f)
                    .SetUpdate(true);
            }
        }

        /// <summary>
        /// 정산 결과를 <b>붙잡아 둔다</b>. 잔액 숫자를 정산 전 값으로 되돌려 놓기만 하고,
        /// ±N 배지는 아직 붙이지 않는다.
        ///
        /// 붙잡는 것과 드러내는 것을 나눈 이유: 호스트는 최종 판정에 들어서는 순간 이미
        /// 정산을 마쳤으므로, 붙잡아 두지 않으면 머니 바 숫자가 결과를 먼저 말해 버린다.
        /// 그렇다고 배지까지 같이 띄우면 주사위가 구르기도 전에 답이 나온다.
        /// 그래서 숫자는 곧바로 잠그고, 배지는 <see cref="RevealDeltas"/>까지 미룬다.
        ///
        /// 정산 전 값을 인자로 받지 않고 <c>newBalance - delta</c>로 되계산하는 이유는,
        /// <see cref="SetCard"/>가 이 호출보다 먼저 오든 나중에 오든 결과가 같아야 하기
        /// 때문이다. 네트워크에서는 두 갱신이 같은 틱에 순서 없이 도착한다.
        /// </summary>
        public void HoldDelta(CharacterId owner, int newBalance, int delta)
        {
            MoneyCard card = Find(owner);
            if (card == null) return;

            card.Target = newBalance;
            card.Pending = delta;
            card.Previewing = true;
            ShowBalance(card, newBalance - delta, animate: false);
            HideDeltaBadge(card);
        }

        /// <summary>
        /// 붙잡아 둔 변화량을 ±N 배지로 드러낸다. 승자가 공개된 뒤에 부른다.
        /// 숫자 자체는 <see cref="CommitDeltas"/>가 굴릴 때까지 그대로다.
        /// </summary>
        public void RevealDeltas()
        {
            if (cards == null) return;

            foreach (MoneyCard card in cards)
                if (card != null && card.Previewing) ShowDeltaBadge(card, card.Pending);
        }

        /// <summary>
        /// 예고해 둔 변화를 숫자에 반영한다. 잔액이 목표까지 굴러가고 ±N 배지는 사라진다.
        /// 예고 중인 칸이 없으면 아무 일도 하지 않으므로 여러 번 불러도 안전하다.
        /// </summary>
        public void CommitDeltas()
        {
            if (cards == null) return;

            foreach (MoneyCard card in cards)
            {
                if (card == null || !card.Previewing) continue;

                card.Previewing = false;
                HideDeltaBadge(card);
                ShowBalance(card, card.Target, animate: true);
            }
        }

        private MoneyCard Find(CharacterId owner)
        {
            if (owner == CharacterId.None || cards == null) return null;

            foreach (MoneyCard card in cards)
                if (card != null && card.Owner == owner) return card;

            return null;
        }

        // 숫자를 목표까지 굴린다. 첫 표시이거나 값이 같으면 그냥 찍는다.
        private static void ShowBalance(MoneyCard card, int value, bool animate)
        {
            card.Roll?.Kill();
            card.Roll = null;

            if (!animate || !card.HasShown || card.Shown == value)
            {
                WriteBalance(card, value);
                return;
            }

            int from = card.Shown;
            card.Roll = DOTween.To(() => from, v => WriteBalance(card, v), value, RollDuration)
                .SetEase(Ease.OutCubic)
                .SetUpdate(true)   // 연출 중 timeScale과 무관하게 굴러가야 한다
                .OnComplete(() => WriteBalance(card, value));
        }

        private static void WriteBalance(MoneyCard card, int value)
        {
            card.Shown = value;
            card.HasShown = true;
            if (card.Balance != null) card.Balance.text = value.ToString("N0") + " <sprite index=0>";
        }

        private static void ShowDeltaBadge(MoneyCard card, int delta)
        {
            if (card.Delta == null) return;

            if (delta == 0) { HideDeltaBadge(card); return; }

            card.Delta.gameObject.SetActive(true);
            card.Delta.text = (delta > 0 ? "+" : "-") + Mathf.Abs(delta).ToString("N0") + " <sprite index=0>";
            card.Delta.color = delta > 0 ? ZooJackPalette.MoneyGain : ZooJackPalette.MoneyLoss;

            // 뜨는 순간을 눈에 띄게. 정산은 라운드에 한 번뿐이라 놓치면 안 된다.
            var rect = card.Delta.rectTransform;
            rect.DOKill();
            rect.localScale = Vector3.one * 0.6f;
            rect.DOScale(Vector3.one, 0.32f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private static void HideDeltaBadge(MoneyCard card)
        {
            if (card.Delta == null) return;
            card.Delta.rectTransform.DOKill();
            card.Delta.rectTransform.localScale = Vector3.one;
            card.Delta.gameObject.SetActive(false);
        }

        /// <summary>
        /// 머니바 칸의 직군 라벨. 역할이 없는 자리는 "잔액"이라고만 적는다.
        ///
        /// 밖으로 열어 둔 이유: 매치 종료 순위표가 같은 라벨을 부제로 쓴다. 둘이 같은
        /// 글자여야 머니바에서 보던 사람을 순위표에서 그대로 찾는다.
        /// </summary>
        public static string RoleLabel(PlayerRole role) => role switch
        {
            PlayerRole.PlayerA => ZooJackText.RoleName(PlayerRole.PlayerA),
            PlayerRole.PlayerB => ZooJackText.RoleName(PlayerRole.PlayerB),
            PlayerRole.Dealer  => ZooJackText.RoleName(PlayerRole.Dealer),
            _                  => ZooJackText.Get("Game.Money.Balance", "잔액")
        };
    }
}
