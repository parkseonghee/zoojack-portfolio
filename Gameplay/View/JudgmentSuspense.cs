using DG.Tweening;
using TMPro;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 최종 판정 후보를 차례로 강조한 뒤 실제 승자를 고정해 보여 주는 연출입니다.
    /// 화면 전체의 비네트는 유지하되 UI 박스 자체는 흔들지 않습니다.
    ///
    /// 두구두구의 박자와 승자 확정 시점만 여기서 정하고, 실제로 보이는 연출은
    /// <see cref="FinalJudgmentCube"/>가 맡습니다 — 박자마다 큐브를 한 번씩 더 밀고,
    /// 확정되는 순간 승자의 면을 세웁니다.
    /// </summary>
    public class JudgmentSuspense
    {
        /// <summary>구르는 소리가 없을 때 쓰는 두구두구 길이(초).</summary>
        const float FallbackRollDuration = 2.4f;

        /// <summary>
        /// 구르는 소리에서 <b>가장 커지는</b> 지점이 클립 길이의 몇 할쯤인지.
        /// 여기까지가 큐브가 빨라지는 구간이다.
        ///
        /// 지금 쓰는 소리(floraphonic-spin-whoosh-1)의 에너지를 재 보면 0초부터 꾸준히 올라
        /// 전체의 35%쯤에서 최고에 이른다. 그 뒤로는 계속 잦아든다.
        /// </summary>
        const float SoundPeakFraction = 0.35f;

        /// <summary>구르는 소리가 없을 때 쓰는 총 회전 시간(초).</summary>
        const float FallbackSpinDuration = 6f;

        const int CandidateCount = 3;   // 플레이어 A · 딜러 · 플레이어 B

        /// <summary>
        /// 큐브가 멈춘 뒤 결과 문구가 뜨기까지의 뜸(초).
        /// 승자 얼굴을 먼저 눈에 담을 시간을 준다 — 곧바로 글자가 덮이면
        /// 두구두구로 쌓은 것이 한 프레임에 흩어진다.
        /// </summary>
        const float RevealDelay = 1f;

        readonly TextMeshProUGUI txtWinner;
        readonly TextMeshProUGUI txtReason;
        readonly VignetteEffect vignette;
        readonly FinalJudgmentCube cube;
        readonly GameObject detailsGroup;
        readonly GameObject outcomeGroup;

        Sequence seq;
        int playedVersion = int.MinValue;

        /// <summary>
        /// 후보 강조 상태가 바뀔 때 호출된다. 승자가 확정되면 그 승자를,
        /// 룰렛이 도는 동안에는 <see cref="FinalWinner.None"/>을 넘긴다.
        /// 왕관/비석 표식이 이 신호로 붙고 떨어진다.
        /// </summary>
        public System.Action<FinalWinner> OnCandidateSettled;

        public JudgmentSuspense(
            TextMeshProUGUI txtWinner,
            TextMeshProUGUI txtReason,
            VignetteEffect vignette,
            FinalJudgmentCube cube,
            GameObject detailsGroup,
            GameObject outcomeGroup)
        {
            this.txtWinner = txtWinner;
            this.txtReason = txtReason;
            this.vignette = vignette;
            this.cube = cube;
            this.detailsGroup = detailsGroup;
            this.outcomeGroup = outcomeGroup;
        }

        /// <param name="matchOver">
        /// 이 판정으로 매치가 끝나는지. 그렇다면 최종 승리 팡파레를 내지 않는다 —
        /// 결과 문구가 뜨는 그 프레임에 매치 종료 카드가 곧바로 덮으며 제 팡파레를
        /// 울리므로, 둘을 겹쳐 내면 어느 쪽도 들리지 않는다.
        /// </param>
        public void Show(
            int judgmentVersion,
            FinalWinner winner,
            string winnerText,
            string reasonText,
            bool dealerWon,
            bool matchOver = false,
            System.Action onRevealed = null)
        {
            if (judgmentVersion == playedVersion)
            {
                // 이미 이 판정을 틀었다. 연출이 아직 돌고 있다면 <b>아무것도 하지 않는다.</b>
                //
                // 예전에는 여기서 onRevealed를 불렀다. 그런데 화면 갱신은 두구두구가 도는
                // 7초 사이에도 몇 번씩 일어나므로(남의 준비 상태·잔액·라운드 수가 바뀔 때),
                // 그때마다 결과 뒤처리가 먼저 실행됐다 — 주사위가 아직 구르는 중에
                // 매치 종료 화면이 올라오고 그 팡파레까지 울렸다.
                //
                // 첫 라운드에 매치가 끝날 때는 그 사이에 바뀔 상태가 없어 갱신이 걸리지
                // 않았고, 그래서 둘째 라운드부터만 드러났다.
                //
                // 뒤처리는 연출이 제 시각(revealAt)에 부르므로 여기서 부를 이유가 없다.
                // 연출이 이미 끝난 뒤의 갱신이라면 아래로 내려가 그대로 다시 그린다.
                if (IsPlaying) return;

                ShowImmediate(winner, winnerText, reasonText);
                onRevealed?.Invoke();
                return;
            }

            playedVersion = judgmentVersion;
            Play(winner, winnerText, reasonText, dealerWon, matchOver, onRevealed);
        }

        /// <summary>두구두구 연출이 지금 돌고 있는지.</summary>
        public bool IsPlaying => seq != null && seq.IsActive() && seq.IsPlaying();

        /// <summary>
        /// 두구두구 박을 깐다. 간격이 점점 좁아져 큐브에 속도가 붙는다.
        /// </summary>
        private void InsertBeats(Sequence target, float from, float until)
        {
            float at = from;
            float interval = 0.24f;
            int index = 0;

            while (at < until)
            {
                int selectedIndex = index++ % CandidateCount;
                target.InsertCallback(at, () => cube?.Flash(selectedIndex));
                at += interval;
                interval = Mathf.Max(0.055f, interval * 0.86f);
            }
        }

        private void ShowImmediate(FinalWinner winner, string winnerText, string reasonText)
        {
            if (IsPlaying) return;

            SetText(winnerText, reasonText);
            cube?.Settle(winner);
            OnCandidateSettled?.Invoke(winner);
            if (detailsGroup != null) detailsGroup.SetActive(true);

            // 이미 본 판정을 화면 갱신 때문에 다시 그리는 길이다. 여기서 또 튀어 오르면
            // 남이 버튼을 누를 때마다 카드가 들썩인다.
            ResultCardPop.ShowInstant(outcomeGroup);
        }

        private void Play(
            FinalWinner winner,
            string winnerText,
            string reasonText,
            bool dealerWon,
            bool matchOver,
            System.Action onRevealed)
        {
            Stop();

            if (detailsGroup != null) detailsGroup.SetActive(false);
            if (outcomeGroup != null) outcomeGroup.SetActive(false);
            cube?.FocusNone();
            OnCandidateSettled?.Invoke(FinalWinner.None);   // 룰렛 중에는 표식을 감춘다

            // ── 박자는 구르는 소리가 정한다 ────────────────────────────
            //
            // 소리를 그림에 맞춰 자르는 것이 아니라 그림이 소리를 따라간다. 큐브는
            // 소리가 커지는 동안 빨라지고, 잦아드는 동안 함께 느려지다 선다.
            //
            //   0 ~ 최고점(35%)      두구두구 — 큐브가 점점 빨라진다
            //   ~ 소리 끝            감속하다가 승자 면을 접어 세운다
            //   ~ + RevealDelay      결과 문구와 정산 카드가 뜬다
            //
            // 소리를 처음부터 끝까지 그대로 틀고, <b>면이 서는 순간을 소리가 끝나는 순간에</b>
            // 맞춘다. 마지막 바람 소리가 잦아드는 것과 초상화가 정면으로 서는 것이 같은
            // 순간이라 둘이 한 동작으로 읽힌다.
            //
            // 가속에서 감속으로 넘어가는 지점도 소리에서 읽는다 — 소리가 가장 커지는 곳이
            // 큐브가 가장 빠른 곳이어야 하기 때문이다. 소리가 없으면 예전 길이로 돌아간다.
            float spinLength = GameAudio.SpinSeconds;
            float snap = cube != null ? cube.SnapDuration : 0f;

            float spinDuration = spinLength > 0f ? spinLength : FallbackSpinDuration;
            float rollDuration = spinLength > 0f
                ? spinLength * SoundPeakFraction
                : FallbackRollDuration;

            float minSpinDown = cube != null ? cube.DefaultSpinDownDuration : 0f;
            float spinDown = Mathf.Max(minSpinDown, spinDuration - rollDuration - snap);

            float settleAt = rollDuration;
            float lockedAt = settleAt + spinDown + snap;
            float revealAt = lockedAt + RevealDelay;

            // 소리는 처음부터 끝까지 그대로 튼다. 자르지도, 중간에 다시 틀지도 않는다 —
            // 소리가 끝나는 순간이 곧 초상화가 정면으로 서는 순간이다.
            GameAudio.PlaySpin();

            // 비네트 명멸은 큐브가 멈출 때까지 끌고 간다. 두구두구만 덮으면
            // 정작 착지하는 순간에 화면이 조용해진다.
            vignette?.PlaySuspense(lockedAt);

            seq = DOTween.Sequence().SetUpdate(true);

            InsertBeats(seq, 0f, rollDuration);

            // 큐브를 승자 면으로 세운다. 아직 글자도 표식도 없다 —
            // 이 구간에서 답을 흘리면 큐브가 멈추는 순간의 맛이 사라진다.
            // 감속 길이는 소리가 잦아드는 길이에 맞춰 늘려 잡는다.
            seq.InsertCallback(settleAt, () => cube?.Settle(winner, spinDown));

            // 면이 완전히 선 순간의 섬광. 구르는 소리가 방금 잦아든 자리에
            // 판정음이 들어와, 소리로도 '멈췄다'가 분명해진다.
            seq.InsertCallback(lockedAt, () =>
            {
                vignette?.FlashResult(!dealerWon);
                GameAudio.PlayDiceReveal();
            });

            // 한 박 쉬고 결과를 한꺼번에 펼친다. 왕관·비석도 이때 함께 붙는다.
            seq.InsertCallback(revealAt, () =>
            {
                // 최종 승리 팡파레. 매치가 여기서 끝나면 내지 않는다(matchOver 설명 참고).
                if (!matchOver) GameAudio.PlayFinalWin();

                SetText(winnerText, reasonText);
                OnCandidateSettled?.Invoke(winner);
                if (detailsGroup != null) detailsGroup.SetActive(true);

                // 겉보기 승리·매치 종료 카드도 같은 연출로 뜬다(ResultCardPop).
                ResultCardPop.Play(outcomeGroup);

                onRevealed?.Invoke();
            });
        }

        public void Stop()
        {
            seq?.Kill();
            seq = null;

            // 연출이 끊기면 구르는 소리도 함께 끊는다. 두지 않으면 큐브가 사라진
            // 화면에서 바람 소리만 7초 넘게 남는다.
            GameAudio.StopSpin();

            if (outcomeGroup != null) ResultCardPop.Settle(outcomeGroup.transform);
        }

        private void SetText(string winnerText, string reasonText)
        {
            if (txtWinner != null) txtWinner.text = winnerText;
            if (txtReason != null) txtReason.text = reasonText;
        }

    }
}
