using System;
using TMPro;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 테이블 위 판돈 표시 — 각자의 칩 더미, 그 아래 금액, 화면 위 가운데의 총합 배지,
    /// 그리고 판돈 패널에서 지금 고른 액수의 칩 그림.
    ///
    /// <b>왜 한 클래스인가.</b> 넷은 같은 사실(누가 얼마를 걸었나)을 네 군데에 적는 일이고,
    /// 무엇보다 <b>칩 색이 금액으로 정해진다</b>는 규칙을 함께 쓴다. 규칙이 한 곳에 있어야
    /// 고른 칩과 테이블에 놓인 칩이 같은 색으로 보인다.
    ///
    /// <b>왜 별도 클래스인가.</b> 이 표시는 패널을 따라다니지 않는다 — 뇌물·딜러·결정·결과
    /// 어느 화면이 떠 있어도 테이블 위에 그대로 있다. 그런데도
    /// <see cref="GameDirector"/> 안에 판돈 선택 흐름과 뒤섞여 있었다.
    ///
    /// 핫시트와 네트워크가 <b>같은 이 경로</b>로 들어온다.
    /// </summary>
    internal sealed class BetChipsView
    {
        /// <summary>칩 더미 아래쪽 끝에서 금액 라벨까지의 간격(px).</summary>
        private const float LabelGap = 6f;

        // 칩 색이 바뀌는 경계 금액. 고른 칩과 테이블 위 칩이 같은 규칙을 써야
        // "내가 고른 그 칩"으로 읽힌다.
        private const int BlackFrom = 601;
        private const int RedFrom   = 401;
        private const int BlueFrom  = 201;

        private readonly BetChipStackView stackA, stackB;
        private readonly TextMeshProUGUI amountA, amountB;
        private readonly GameObject totalCard;
        private readonly TextMeshProUGUI totalLabel, totalAmount;
        private readonly TextMeshProUGUI selectedLabel;
        private readonly TMP_SpriteAsset tokenGreen, tokenBlue, tokenRed, tokenBlack;

        /// <summary>
        /// 매치가 끝났는가. 단계로는 가려낼 수 없어서 물어봐야 한다 — 끝난 뒤에도 단계는
        /// 최종 판정이었다가 라운드 종료로 넘어가고, 그 둘은 평소에는 배지를 띄우는 단계다.
        ///
        /// 값이 아니라 조회를 받는 이유: 핫시트와 네트워크가 모두 이 경로를 쓰는데,
        /// 둘 다 매치 종료 여부를 <see cref="GameDirector"/>가 들고 있는 한 곳에서 읽고 있었다.
        /// 인자로 바꾸면 부르는 두 곳이 각자 답을 만들게 되어 어긋날 자리가 생긴다.
        /// </summary>
        private readonly Func<bool> matchEnded;

        // 공개된 두 베팅의 합. 배지 표시가 단계에 달려 있어 값을 들고 있어야 한다.
        private int total;
        private bool revealed;

        public BetChipsView(
            BetChipStackView stackA, BetChipStackView stackB,
            TextMeshProUGUI amountA, TextMeshProUGUI amountB,
            GameObject totalCard, TextMeshProUGUI totalLabel, TextMeshProUGUI totalAmount,
            TextMeshProUGUI selectedLabel,
            TMP_SpriteAsset tokenGreen, TMP_SpriteAsset tokenBlue,
            TMP_SpriteAsset tokenRed, TMP_SpriteAsset tokenBlack,
            Func<bool> matchEnded)
        {
            this.stackA = stackA;
            this.stackB = stackB;
            this.amountA = amountA;
            this.amountB = amountB;
            this.totalCard = totalCard;
            this.totalLabel = totalLabel;
            this.totalAmount = totalAmount;
            this.selectedLabel = selectedLabel;
            this.tokenGreen = tokenGreen;
            this.tokenBlue = tokenBlue;
            this.tokenRed = tokenRed;
            this.tokenBlack = tokenBlack;
            this.matchEnded = matchEnded;
        }

        /// <summary>
        /// 각 자리에 그 좌석이 테이블에 올린 만큼 칩을 놓는다. 두 사람의 액수는 서로 다르며,
        /// 더블다운을 선언한 쪽만 자기 액수의 2배로 올라간다. 0이면 그 자리를 비운다.
        /// </summary>
        /// <param name="seed">
        /// 기둥 순서를 섞는 씨앗. 라운드 번호를 넘기면 라운드마다 배열이 달라지고,
        /// 같은 라운드 안에서는 모든 피어가 똑같은 모양을 본다.
        /// 두 자리에 다른 값을 주는 이유는, 양쪽이 같은 금액을 걸었을 때
        /// 좌우가 거울처럼 똑같이 서는 걸 피하기 위해서다.
        /// </param>
        public void Show(int betA, int betB, int seed, GamePhase currentPhase)
        {
            if (betA < 0) betA = 0;
            if (betB < 0) betB = 0;

            stackA?.Show(betA, seed * 2);
            stackB?.Show(betB, seed * 2 + 1);

            // 베팅은 양쪽이 다 낸 뒤에야 0이 아니게 된다(FusionGameState.HostStartInitialDeal).
            // 그전에 숫자를 띄우면 먼저 낸 쪽의 금액이 새므로, 판정을 이 값 하나에 맡긴다.
            bool bothIn = betA > 0 && betB > 0;

            ShowAmount(amountA, stackA, betA, bothIn);
            ShowAmount(amountB, stackB, betB, bothIn);

            revealed = bothIn;
            total = betA + betB;
            RefreshTotal(currentPhase);
        }

        /// <summary>
        /// 총합 배지를 갱신한다. <b>단계가 바뀔 때마다</b> 다시 불러야 한다 —
        /// 표시 여부가 단계에 달려 있는데 <see cref="Show"/>는 라운드에 두 번뿐이다.
        /// </summary>
        public void RefreshTotal(GamePhase currentPhase)
        {
            // 최초 카드 배분은 판돈 선택보다 먼저 시작되므로 금액은 아직 0이다.
            // 이때도 딜러 화면의 다른 상태 카드와 통일되도록 총베팅 박스 자체는 보여 준다.
            bool showBeforeBetting = currentPhase == GamePhase.DealerCardDistribution;
            bool visible = (revealed || showBeforeBetting)
                && matchEnded?.Invoke() != true
                && FitsIn(currentPhase);

            if (totalCard != null && totalCard.activeSelf != visible)
                totalCard.SetActive(visible);

            if (!visible) return;

            if (totalLabel != null)
                totalLabel.text = ZooJackText.Get("Game.Bet.Total", "총 베팅");

            if (totalAmount != null)
                totalAmount.text = $"<b>{total:N0} <sprite index=0></b>";
        }

        /// <summary>판돈 패널에서 지금 고른 액수. 금액에 맞는 칩 그림으로 함께 바뀐다.</summary>
        public void SetSelectedAmount(int amount)
        {
            if (selectedLabel == null) return;

            TMP_SpriteAsset spriteAsset = TokenFor(amount);

            if (spriteAsset != null) selectedLabel.spriteAsset = spriteAsset;
            selectedLabel.text = amount.ToString("N0") + " <sprite index=0>";
        }

        /// <summary>
        /// 총합 배지(y 278~324)를 띄워도 되는 단계인지.
        ///
        /// 베팅은 딜러 카드 배분이 <b>시작될 때</b> 공개된다. 딜러 패널보다 나중에 그려지는
        /// 별도 카드이므로 반투명 이미지 위에서도 타이머처럼 선명하게 보인다.
        /// </summary>
        private static bool FitsIn(GamePhase p) => p switch
        {
            // 다음 라운드 버튼을 누른 직후에는 이전 라운드 베팅 값이 아직 남아 있다.
            // 새 라운드 시작 시 초기화되기 전까지 준비 화면에 노출하지 않는다.
            GamePhase.WaitingForPlayers => false,

            // 후보 무대(y 98~286)와 딜러 판정문(y 310~358) 사이에 배지가 끼어 양쪽을 자른다.
            GamePhase.FinalJudgment => false,

            // 겹치지는 않지만 화면을 어둡게 깔고 한 곳을 보게 만드는 단계다.
            // 배지만 밝게 떠 있으면 시선이 갈린다.
            GamePhase.TieRedeal => false,

            _ => true
        };

        // 금액은 자기 칩 더미 바로 아래에 붙는다. 더미는 금액에 따라 폭도 높이도
        // 달라지므로 고정 좌표를 쓰지 않고 매번 실제 모양에 맞춰 내려 단다.
        private void ShowAmount(
            TextMeshProUGUI label, BetChipStackView stack, int bet, bool bothIn)
        {
            if (label == null) return;

            if (!bothIn)
            {
                if (label.gameObject.activeSelf) label.gameObject.SetActive(false);
                return;
            }

            TMP_SpriteAsset spriteAsset = TokenFor(bet);
            if (spriteAsset != null) label.spriteAsset = spriteAsset;
            label.text = bet.ToString("N0") + " <sprite index=0>";
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);

            if (stack == null) return;
            Rect chips = stack.RestingChipBounds();
            if (chips.height <= 0f) return;

            var rect = label.rectTransform;
            rect.anchoredPosition = new Vector2(
                chips.center.x,
                chips.yMin - LabelGap - rect.sizeDelta.y * 0.5f);
        }

        private TMP_SpriteAsset TokenFor(int amount) =>
            amount >= BlackFrom ? tokenBlack
            : amount >= RedFrom ? tokenRed
            : amount >= BlueFrom ? tokenBlue
            : tokenGreen;
    }
}
