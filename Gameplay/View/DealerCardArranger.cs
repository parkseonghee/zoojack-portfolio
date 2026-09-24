using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 딜러 후보 카드 배치기.
    /// - 공정 카드(0번)를 중앙에 크게, 조작 카드(1·2번)를 우측 상단 작은 슬롯에 배치.
    /// - 작은 카드를 클릭하면 중앙 카드와 자리를 교환(DOTween 연출).
    /// - 중앙 카드를 클릭하면 그 카드를 최종 선택으로 제출.
    /// - 중앙 카드가 조작이면 검붉은 비네트, 공정이면 초록 비네트로 피드백.
    /// GameDirector(핫시트)와 NetworkGameDirector(멀티) 양쪽에서 재사용한다.
    /// </summary>
    public class DealerCardArranger
    {
        [System.Serializable]
        public class AnimationSettings
        {
            [Min(0f)] public float MoveDuration = 0.32f;
            [Min(0f)] public float ConfirmPopDuration = 0.10f;
            [Min(0f)] public float ConfirmFlyDuration = 0.40f;

            // 중앙 슬롯을 기준으로 한 확정 카드의 도착 오프셋.
            public Vector2 ExitOffsetA = new Vector2(-530f, 20f);
            public Vector2 ExitOffsetB = new Vector2( 530f, 20f);

            [Range(0f, 2f)] public float ConfirmPopScale = 1.12f;
            [Range(0f, 2f)] public float ConfirmExitScale = 0.45f;
            [Min(0f)] public float DismissDuration = 0.18f;

            [Min(0f)] public float TimerUrgentThreshold = 5f;
            public Color TimerUrgentColor = new Color(1f, 0.25f, 0.20f);
            [Range(0f, 1f)] public float TimerPulseScale = 0.22f;
        }

        readonly RectTransform[]     rects;
        readonly CardView[]          views;
        readonly Button[]            buttons;
        readonly TextMeshProUGUI[]   labels;
        readonly VignetteEffect      vignette;
        readonly AnimationSettings   animation;

        // 후보 장수. 카드를 뽑는 쪽(BlackjackDeckService)이 정한 수를 그대로 따른다 —
        // 여기 3을 따로 적어 두면 뽑는 장수를 바꿨을 때 화면만 옛 수에 남는다.
        const int SlotCount = BlackjackDeckService.DefaultCandidateCount;

        // 슬롯 레이아웃은 Canvas에 저장된 값을 원본으로 사용한다.
        readonly Vector2[] slotPositions = new Vector2[SlotCount];
        readonly Vector2[] slotSizes     = new Vector2[SlotCount];
        readonly Vector3[] slotScales    = new Vector3[SlotCount];
        readonly Vector3[] labelScales   = new Vector3[SlotCount];

        int count;
        readonly int[] slot = new int[SlotCount]; // slot[card] = 0(중앙) / 1,2(작은 슬롯)
        int centerCard;
        int version = int.MinValue;
        bool targetIsPlayerA;
        bool locked; // 확정 연출 중에는 추가 입력을 막는다.
        TextMeshProUGUI timerText;
        Vector3 timerBaseScale = Vector3.one;
        Color timerBaseColor = Color.white;

        /// <summary>현재 중앙에 놓인 카드의 후보 인덱스(타임아웃 자동 선택용).</summary>
        public int CurrentCenter => centerCard;

        public DealerCardArranger(RectTransform[] rects, CardView[] views, Button[] buttons,
            TextMeshProUGUI[] labels, VignetteEffect vignette, AnimationSettings animation = null)
        {
            this.rects = rects; this.views = views; this.buttons = buttons;
            this.labels = labels; this.vignette = vignette;
            this.animation = animation ?? new AnimationSettings();

            CaptureAuthoredLayout();
        }

        private void CaptureAuthoredLayout()
        {
            for (int i = 0; i < 3; i++)
            {
                if (rects != null && i < rects.Length && rects[i] != null)
                {
                    slotPositions[i] = rects[i].anchoredPosition;
                    slotSizes[i]     = rects[i].sizeDelta;
                    slotScales[i]    = rects[i].localScale;
                }
                else
                {
                    slotScales[i] = Vector3.one;
                }

                labelScales[i] = labels != null && i < labels.Length && labels[i] != null
                    ? labels[i].transform.localScale
                    : Vector3.one;
            }
        }

        /// <summary>새 후보 세트가 오면 배치를 초기화한다. 같은 세트면 비네트만 갱신.</summary>
        /// <param name="targetPlayerIsA">이 카드를 받을 대상이 플레이어 A인지(확정 연출 방향 결정).</param>
        public void Setup(IReadOnlyList<BlackjackCard> cands, int candidateVersion, bool targetPlayerIsA)
        {
            targetIsPlayerA = targetPlayerIsA;
            if (candidateVersion == version) { RefreshVignette(); return; }
            version = candidateVersion;
            locked = false;
            count = cands == null ? 0 : Mathf.Min(cands.Count, 3);
            centerCard = 0;

            for (int i = 0; i < 3; i++)
            {
                bool show = i < count;
                if (buttons[i] != null) buttons[i].gameObject.SetActive(show);
                if (!show) { views[i]?.Hide(); continue; }

                views[i]?.ShowCandidate(cands[i], i);
                if (labels[i] != null)
                {
                    labels[i].text = i == 0
                        ? ZooJackText.Get("Game.Dealer.Candidate.Fair", "공정 후보")
                        : ZooJackText.Get("Game.Dealer.Candidate.Cheated", "조작 후보");
                }
                slot[i] = i; // 0->중앙, 1->작은0, 2->작은1
                ApplyImmediate(i);
            }
            RefreshVignette();
        }

        /// <summary>카드 클릭 처리: 중앙이면 확정 연출 후 제출, 작은 슬롯이면 중앙과 스왑.</summary>
        public void HandleClick(int card, System.Action<int> onCenterChanged)
        {
            if (locked || card < 0 || card >= count) return;

            if (slot[card] == 0)
            {
                onCenterChanged?.Invoke(centerCard);
                return;
            }

            int prevCenter  = centerCard;
            int targetSlot  = slot[card];
            slot[card]      = 0;
            slot[prevCenter] = targetSlot;
            centerCard      = card;

            // 카드가 실제로 자리를 옮기는 순간에만 울린다. 위에서 이미 돌아간 두 갈래
            // (연출 중이거나, 이미 중앙에 있는 카드를 다시 누른 경우)에는 아무것도
            // 움직이지 않으므로 소리도 없어야 한다 — 두 장이 함께 움직여도 한 번이다.
            GameAudio.PlayCardSlide();

            Animate(card);        // 작은 카드 -> 중앙
            Animate(prevCenter);  // 중앙 카드 -> 방금 비운 작은 슬롯
            RefreshVignette();
            onCenterChanged?.Invoke(centerCard);
        }

        /// <summary>시간 초과 등으로 중앙 카드를 강제 확정한다(연출 포함, 중복 호출 안전).</summary>
        public void ConfirmCenter(System.Action<int> onSelect)
        {
            if (locked || count <= 0) return;
            PlayConfirm(centerCard, onSelect);
        }

        /// <summary>확정 연출: 작은 카드는 접히고, 중앙 카드가 대상 플레이어 쪽으로 날아간다.</summary>
        void PlayConfirm(int card, System.Action<int> onSelect)
        {
            locked = true;
            vignette?.Hide();

            for (int i = 0; i < count; i++)
            {
                if (i == card) continue;
                var small = rects[i];
                if (small == null) continue;
                small.DOKill();
                small.DOScale(0f, animation.DismissDuration).SetEase(Ease.InBack).SetUpdate(true);
            }

            var rt = rects[card];
            if (rt == null) { onSelect?.Invoke(card); return; }

            rt.DOKill();
            rt.SetAsLastSibling(); // 날아가는 동안 다른 UI에 가리지 않도록
            Vector2 exit = slotPositions[0]
                + (targetIsPlayerA ? animation.ExitOffsetA : animation.ExitOffsetB);
            Vector3 centerScale = slotScales[0];

            DOTween.Sequence().SetUpdate(true)
                .Append(rt.DOScale(centerScale * animation.ConfirmPopScale,
                    animation.ConfirmPopDuration).SetEase(Ease.OutQuad))
                .Append(rt.DOAnchorPos(exit, animation.ConfirmFlyDuration).SetEase(Ease.InCubic))
                .Join(rt.DOScale(centerScale * animation.ConfirmExitScale,
                    animation.ConfirmFlyDuration).SetEase(Ease.InCubic))
                .OnComplete(() => onSelect?.Invoke(card));
        }

        /// <summary>남은 시간에 따라 타이머 텍스트를 갱신한다. 5초 이하부터 붉게 맥동한다.</summary>
        public void StyleTimer(TextMeshProUGUI txt, float secondsLeft)
        {
            if (txt == null) return;
            if (timerText != txt)
            {
                timerText = txt;
                timerBaseScale = txt.transform.localScale;
                timerBaseColor = txt.color;
            }
            // 머리 위 명패와 같은 변환을 써야 두 시계가 같은 숫자를 가리킨다.
            txt.text = TurnClock.Label(secondsLeft);

            if (secondsLeft > animation.TimerUrgentThreshold)
            {
                txt.color = timerBaseColor;
                txt.transform.localScale = timerBaseScale;
                return;
            }

            // |sin(pi*t)| → 1초 주기 맥동. 0초에 가까울수록 붉고 크게 뛴다.
            float pulse     = Mathf.Abs(Mathf.Sin(secondsLeft * Mathf.PI));
            float threshold = Mathf.Max(0.01f, animation.TimerUrgentThreshold);
            float intensity = 1f - Mathf.Clamp01(secondsLeft / threshold);
            txt.color = Color.Lerp(timerBaseColor, animation.TimerUrgentColor,
                0.45f + 0.55f * intensity);
            txt.transform.localScale = timerBaseScale
                * (1f + animation.TimerPulseScale * pulse * (0.4f + 0.6f * intensity));
        }

        public void RefreshVignette()
        {
            // 카드 선택 중 화면 가장자리 비네트는 거슬린다는 피드백으로 제거했다.
            // 비네트 컴포넌트는 최종 판정 "두구두구" 섬광 연출에서만 사용한다.
            vignette?.Hide();
        }

        public void HideVignette() => vignette?.Hide();

        void ApplyImmediate(int card)
        {
            var rt = rects[card]; if (rt == null) return;
            rt.DOKill();
            int s = slot[card];
            rt.anchoredPosition = PosOf(s);
            rt.sizeDelta        = SizeOf(s);
            rt.localScale       = ScaleOf(s); // 이전 확정 연출 후 Canvas에 저장된 스케일로 복귀
            SetLabelScale(card, s);
        }

        void Animate(int card)
        {
            var rt = rects[card]; if (rt == null) return;
            rt.DOKill();
            int s = slot[card];
            rt.DOAnchorPos(PosOf(s), animation.MoveDuration).SetEase(Ease.OutCubic);
            rt.DOSizeDelta(SizeOf(s), animation.MoveDuration).SetEase(Ease.OutCubic);
            rt.DOScale(ScaleOf(s), animation.MoveDuration).SetEase(Ease.OutCubic);
            SetLabelScale(card, s);
        }

        void SetLabelScale(int card, int targetSlot)
        {
            if (labels[card] == null) return;
            labels[card].transform.localScale = labelScales[Mathf.Clamp(
                targetSlot, 0, labelScales.Length - 1)];
        }

        Vector2 PosOf(int s) => slotPositions[Mathf.Clamp(s, 0, slotPositions.Length - 1)];
        Vector2 SizeOf(int s) => slotSizes[Mathf.Clamp(s, 0, slotSizes.Length - 1)];
        Vector3 ScaleOf(int s) => slotScales[Mathf.Clamp(s, 0, slotScales.Length - 1)];
    }
}
