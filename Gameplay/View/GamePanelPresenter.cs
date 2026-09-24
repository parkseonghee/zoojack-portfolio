using System.Collections.Generic;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 핫시트와 네트워크가 <b>똑같이</b> 하는 화면 조작을 모은 곳.
    ///
    /// <b>왜 필요한가.</b> <see cref="GameDirector"/>(핫시트)와
    /// <see cref="NetworkGameDirector"/>(네트워크)는 이름이 같은 메서드를 서른두 개 갖고 있었다.
    /// 재어 보니 그중 열넷은 글자까지 같은 복붙이었고, 나머지 열여덟은 이름만 같고 하는 일이
    /// 달랐다 — 핫시트는 그 자리에서 상태를 바꾸고 네트워크는 RPC를 보낸다.
    /// 복붙된 쪽만 여기로 옮긴다.
    ///
    /// 한쪽만 고쳤을 때 <b>에디터에서는 되는데 빌드에서는 안 되는</b> 차이가 나는 것이 문제였다.
    /// 네트워크로 들어오면 <c>NetworkGameDirector.Awake</c>가 핫시트 디렉터를 꺼 버리므로
    /// (<c>ui.enabled = false</c>) 두 코드는 절대 함께 돌지 않는다. 그래서 어긋나도 예외가 나지
    /// 않고, 화면을 직접 띄워 봐야 드러난다. <see cref="GameDirector.WireUiSounds"/>가 같은 이유로
    /// 먼저 한쪽으로 합쳐져 있었고, 이 클래스는 그 나머지다.
    ///
    /// <b>왜 GameDirector 안이 아니라 별도 클래스인가.</b> GameDirector는 이미 3,200줄이다.
    /// 여기에 더 넣으면 나중에 패널 단위로 쪼개기가 더 어려워진다. 위젯은 GameDirector가
    /// 들고 있으므로 그 참조만 받아 쓴다.
    /// </summary>
    internal sealed class GamePanelPresenter
    {
        private readonly GameDirector ui;

        public GamePanelPresenter(GameDirector ui) => this.ui = ui;

        // ── 패널 전환 ────────────────────────────────────────────────

        /// <summary>
        /// 패널 하나만 켜고 나머지는 끈다. <paramref name="panel"/>이 null이면 전부 꺼서
        /// 테이블만 남긴다.
        ///
        /// <paramref name="accusationPhase"/>를 인자로 받는 이유: 두 모드가 단계를 읽는 곳이
        /// 다르다(핫시트는 자기 필드, 네트워크는 FusionGameState). 점수 합계 상자를 고발
        /// 단계에서 계속 띄워 두는 판단에만 쓴다.
        /// </summary>
        public void ShowOnly(GameObject panel, bool accusationPhase)
        {
            GameObject[] all =
            {
                ui.panelCover, ui.panelWaiting, ui.panelBribe, ui.panelBet, ui.panelDealer,
                ui.panelDecision, ui.panelResult, ui.panelAccusation, ui.panelFinal
            };
            foreach (var p in all)
                if (p != null) p.SetActive(p == panel);

            // 최종 판정을 떠나면 무대도 함께 내린다. 캐릭터가 무대에 묶인 채
            // 다음 라운드로 넘어가면 테이블 위에서 얼어붙는다.
            if (panel != ui.panelFinal) ui.SetFinalStage(false);

            // 결과 패널을 떠나면 다음 라운드의 결과 연출이 다시 걸리게 풀어 준다.
            if (panel != ui.panelResult) ui.ClearRoundResultPresentation();

            ui.HideMatchOverScreen();

            // 점수 합계 상자. 딜러의 카드 배분 중에도 반투명 패널 위에서 현재 점수를 보여 준다.
            if (ui.sharedScoreCards != null)
                ui.sharedScoreCards.SetActive(
                    panel == ui.panelDealer || panel == ui.panelBet || panel == ui.panelDecision ||
                    panel == ui.panelResult || panel == ui.panelAccusation || panel == ui.panelFinal ||
                    accusationPhase);

            Transform bribeCard = ui.txtBribeStatusTitle != null
                ? ui.txtBribeStatusTitle.transform.parent
                : ui.txtBribeAmountA != null ? ui.txtBribeAmountA.transform.parent : null;
            if (bribeCard != null) bribeCard.gameObject.SetActive(true);

            // 타이머 카드는 여기서 건드리지 않는다. 시계가 도는 단계는 보이는 패널과
            // 일치하지 않는다 — 고발·더블다운은 답할 사람만 패널을 보고 나머지는 대기
            // 화면을 보지만, 제한시간은 모두에게 보여야 한다. 매 프레임 도는
            // UpdateTurnTimer가 표시를 맡는다.

            // 딜러 패널이 아닐 때는 조작 피드백 비네트를 숨긴다.
            if (panel != ui.panelDealer) ui.vignette?.Hide();
        }

        // ── 딜러 카드 후보 ───────────────────────────────────────────

        /// <summary>씬에 놓인 후보 슬롯 셋으로 카드 배치 연출기를 만든다.</summary>
        public DealerCardArranger BuildArranger() => new DealerCardArranger(
            GameDirector.RectsOf(ui.btnCandidates), ui.cardsDealer,
            ui.btnCandidates, ui.txtCandidates,
            ui.vignette, ui.dealerCardAnimation);

        /// <summary>결정 버튼을 잠근다. 확정 연출 중 두 번 눌리는 것을 막는다.</summary>
        public void LockDealerConfirmButton()
        {
            if (ui.btnDealerConfirm != null) ui.btnDealerConfirm.interactable = false;
        }

        /// <summary>
        /// 지금 중앙에 놓인 후보를 줬을 때 대상의 점수가 얼마가 되는지 보여 준다.
        ///
        /// 후보 목록과 대상의 손패를 인자로 받는 이유: 핫시트는 RoundContext에서,
        /// 네트워크는 FusionGameState에서 읽는다. 그리는 방법은 똑같다.
        /// </summary>
        public void ShowDealerCandidateScore(
            IReadOnlyList<BlackjackCard> candidates, int index,
            PlayerRole target, IReadOnlyList<BlackjackCard> targetHand)
        {
            if (candidates == null || candidates.Count == 0 || ui.txtDealerScores == null) return;

            index = Mathf.Clamp(index, 0, candidates.Count - 1);
            BlackjackScore preview = ScoreWith(targetHand, candidates[index]);

            ui.txtDealerScores.text = ZooJackText.Get(
                "Game.Dealer.Score", "{0}의 점수 합계: <b>{1}</b>",
                ZooJackText.RoleName(target), ScoreStr(preview));
        }

        // ── 뇌물·판돈 요약 ───────────────────────────────────────────

        /// <summary>뇌물 판의 요약 줄.</summary>
        public void RefreshBribeSummary(int bribe, int balance)
        {
            if (ui.txtBribeSummary == null) return;
            ui.txtBribeSummary.text = ZooJackText.Get(
                "Game.Bribe.Summary",
                "<color={2}>뇌물</color>  <b>{0:N0}</b>     <color={2}>뇌물 후 잔액</color>  <b>{1:N0}</b>",
                bribe, Mathf.Max(0, balance - bribe), ZooJackPalette.BronzeTag);
        }

        /// <summary>판돈 판의 요약 줄. 고른 칩 표시도 함께 맞춘다.</summary>
        public void RefreshBetSummary(int bet, int bribe, int balance)
        {
            ui.BetChips.SetSelectedAmount(bet);
            if (ui.txtBetSummary == null) return;

            int remaining = Mathf.Max(0, balance - (bet + bribe));
            ui.txtBetSummary.text = ZooJackText.Get(
                "Game.Bet.Summary",
                "<color={3}>판돈</color>  <b>{0:N0}</b>     <color={3}>제출한 뇌물</color>  <b>{1:N0}</b>     <color={3}>남은 잔액</color>  <b>{2:N0}</b>",
                bet, bribe, remaining, ZooJackPalette.BronzeTag);
        }

        /// <summary>
        /// 잔액과 이미 낸 뇌물에 맞춰 슬라이더 범위·프리셋 칩을 다시 잡고,
        /// 그 범위 안으로 잘린 판돈을 돌려준다. 요약 줄도 새 값으로 다시 그린다.
        /// </summary>
        public int ApplyBetLimit(int currentBet, int bribe, int balance, BetPresetRow presets)
        {
            int minSteps = RoundSettlement.BetToSteps(RoundSettlement.MinBet);
            int maxSteps = Mathf.Max(minSteps,
                RoundSettlement.BetToSteps(RoundSettlement.MaxBetFor(balance, bribe)));

            int bet = RoundSettlement.StepsToBet(
                Mathf.Clamp(RoundSettlement.BetToSteps(currentBet), minSteps, maxSteps));

            if (ui.sliderBet != null)
            {
                ui.sliderBet.wholeNumbers = true;
                ui.sliderBet.minValue = minSteps;
                ui.sliderBet.maxValue = maxSteps;
                ui.sliderBet.SetValueWithoutNotify(RoundSettlement.BetToSteps(bet));
            }

            presets?.RefreshInteractable(RoundSettlement.StepsToBet(maxSteps));
            RefreshBetSummary(bet, bribe, balance);
            return bet;
        }

        // ── 점수 문자열 ──────────────────────────────────────────────

        /// <summary>이 손패에 후보 한 장을 더 얹었을 때의 점수.</summary>
        public static BlackjackScore ScoreWith(
            IReadOnlyList<BlackjackCard> hand, BlackjackCard candidate)
        {
            var cards = hand == null
                ? new List<BlackjackCard>()
                : new List<BlackjackCard>(hand);
            cards.Add(candidate);
            return BlackjackScoreCalculator.Calculate(new BlackjackHand { Cards = cards });
        }

        /// <summary>
        /// 점수 표시. 버스트는 글자로, 아직 없는 점수(0)는 하이픈으로 적는다.
        ///
        /// 예전에는 네트워크 쪽 사본에 0 처리가 빠져 있어 빈 점수가 0으로 보였다. 후보를
        /// 한 장 얹은 뒤라 실제로 0이 나오는 경로는 없었지만, 두 사본이 다른 답을 내고
        /// 있었다는 사실 자체가 합칠 이유였다. 방어적인 쪽으로 통일했다.
        /// </summary>
        public static string ScoreStr(BlackjackScore score) =>
            score.IsBust ? ZooJackText.Get("Game.Score.Bust", "버스트")
            : score.BestValue == 0 ? ZooJackText.Get("Common.None", "-")
            : score.BestValue.ToString();
    }
}
