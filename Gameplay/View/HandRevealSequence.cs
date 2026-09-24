using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 덮여 있던 손패를 한 장씩 연다.
    ///
    /// 결과 공개 전까지 상대 손패는 업카드 한 장만 앞면이다. 공개 순간에 나머지가
    /// 한꺼번에 뒤집히면 승패가 한 프레임에 끝나 버려서, 정작 이 게임에서 제일 조마조마해야
    /// 할 지점이 밋밋해진다. 그래서 앞면으로 보여 줄 장수를 시간에 걸쳐 한 장씩 올린다.
    ///
    /// <b>언제 열지를 스스로 판단한다.</b> 단계(phase)를 받지 않고 "손패 장수는 그대로인데
    /// 앞면으로 보여 달라는 장수만 늘었다"는 조건으로 알아낸다. 그것이 곧 '이미 테이블에
    /// 뒷면으로 놓여 있던 카드가 열린다'는 뜻이기 때문이다. 손패 장수가 달라졌다면 새로 받은
    /// 카드이거나 라운드가 초기화된 것이므로 뜸 들이지 않고 그대로 그린다.
    ///
    /// 덕분에 부르는 쪽(핫시트·네트워크 두 디렉터)은 지금까지와 똑같이 호출하면 된다.
    /// </summary>
    public class HandRevealSequence
    {
        /// <summary>
        /// 공개가 시작되고 <b>첫 장이 뒤집히기까지</b>의 뜸(초).
        ///
        /// 늘린 1.5초가 전부 여기 들어 있다(원래 0.45초). 뒤집는 간격이나 판정 전 뜸을
        /// 늘리면 표식이 늦게 뜰 뿐 카드는 곧바로 넘어가 버린다 — 조마조마한 구간은
        /// 카드가 <b>아직 덮여 있는</b> 동안이므로, 뜸은 뒤집기 앞에 있어야 한다.
        /// </summary>
        public const float FirstDelay = 1.95f;

        /// <summary>장과 장 사이 간격(초). 카드 뒤집기 자체보다 길어야 한 장씩으로 읽힌다.</summary>
        public const float StepInterval = 0.7f;

        /// <summary>마지막 장이 뒤집히고 결과를 말하기까지의 뜸(초).</summary>
        public const float TailDelay = 0.5f;

        private class Hand
        {
            public int handCount = -1;   // -1 = 아직 한 번도 그린 적 없음
            public int wanted;           // 직전에 요청받은 앞면 장수
            public int shown;
            public Sequence seq;
        }

        private readonly Dictionary<PlayerRole, Hand> hands = new Dictionary<PlayerRole, Hand>();
        private readonly List<Action> pending = new List<Action>();
        private readonly Action redraw;

        /// <param name="redraw">
        /// 한 장이 더 열릴 때마다 손패를 다시 그리는 동작. 디렉터는 화면이 바뀔 때만
        /// 그리므로, 시간에 따라 열리는 이 연출은 스스로 다시 그려 달라고 해야 한다.
        /// </param>
        public HandRevealSequence(Action redraw)
        {
            this.redraw = redraw;
        }

        /// <summary>지금 어느 손이든 열리는 중인지.</summary>
        public bool IsRunning
        {
            get
            {
                foreach (var hand in hands.Values)
                    if (hand.seq != null) return true;
                return false;
            }
        }

        /// <summary>
        /// 이 손을 이만큼 앞면으로 보여 달라는 요청을 받아, <b>지금 실제로 보여 줄</b> 장수를 돌려준다.
        /// 열리는 중이라면 아직 열린 만큼만 돌려준다.
        /// </summary>
        public int Submit(PlayerRole role, int handCount, int wanted)
        {
            var hand = Get(role);
            handCount = Mathf.Max(0, handCount);
            int target = Mathf.Min(wanted, handCount);

            // 이미 열고 있는 중이면 그 진행을 존중한다. 화면을 다시 그릴 때마다 같은 요청이
            // 다시 들어오므로, 여기서 매번 다시 판단하면 연출이 계속 처음으로 돌아간다.
            if (hand.seq != null)
            {
                // 손패 자체가 갈아엎어졌다면(라운드 초기화) 연출을 접는다.
                if (handCount != hand.handCount)
                {
                    Kill(hand);
                    hand.shown = target;
                }
                hand.handCount = handCount;
                hand.wanted = wanted;
                return hand.shown;
            }

            // 직전까지 덮인 카드가 있었는데 이제 전부 보여 달라고 한다 — 이것이 '공개'다.
            //
            // 장수가 그대로인지로 판단하지 않는 이유: 마지막 히트로 버스트가 나면 카드가
            // 한 장 늘면서 동시에 공개로 넘어간다. 장수만 보면 그 판을 '새로 받았다'로
            // 읽어 뒤집기를 건너뛰게 되고, 하필 제일 극적인 판에서 연출이 빠진다.
            bool wasHiding = hand.wanted < hand.handCount;
            bool opensAll = target >= handCount;
            bool reveal = wasHiding && opensAll && target > hand.shown;

            hand.handCount = handCount;
            hand.wanted = wanted;

            if (reveal)
            {
                StartRamp(hand, target);
                return hand.shown;
            }

            hand.shown = target;
            return hand.shown;
        }

        /// <summary>
        /// 이 손이 지금 앞면으로 보여 주고 있는 장수. 읽기만 하고 아무것도 시작하지 않는다.
        /// </summary>
        public int Shown(PlayerRole role) => Get(role).shown;

        /// <summary>
        /// 다 열린 뒤에 할 일을 건다. 열리는 중이 아니면 그 자리에서 바로 한다.
        /// 승자 발표처럼 "카드가 먼저"인 것들이 여기로 들어온다.
        /// </summary>
        public void WhenDone(Action action)
        {
            if (action == null) return;
            if (!IsRunning) { action(); return; }
            pending.Add(action);
        }

        /// <summary>
        /// 연출을 즉시 끝낸다. 씬을 떠날 때 부른다.
        /// 대기 중인 일은 <b>하지 않고 버린다</b> — 씬이 사라지는 중이라 붙잡을 화면이 없다.
        /// </summary>
        public void StopAll()
        {
            pending.Clear();   // Kill의 Flush보다 먼저 비워야 실행되지 않는다
            foreach (var hand in hands.Values) Kill(hand);
        }

        // ── 내부 ─────────────────────────────────────────────────────

        private Hand Get(PlayerRole role)
        {
            if (!hands.TryGetValue(role, out var hand))
                hands[role] = hand = new Hand();
            return hand;
        }

        private void StartRamp(Hand hand, int target)
        {
            int from = hand.shown;
            var seq = DOTween.Sequence().SetUpdate(true);

            for (int step = from + 1; step <= target; step++)
            {
                int shown = step;
                seq.InsertCallback(FirstDelay + (shown - from - 1) * StepInterval, () =>
                {
                    hand.shown = shown;
                    redraw?.Invoke();
                });
            }

            // 마지막 장이 뒤집히고 한 박 쉰 다음에야 '끝났다'로 친다.
            // 곧바로 결과를 말하면 마지막 카드를 눈에 담을 틈이 없다.
            seq.InsertCallback(
                FirstDelay + (target - from - 1) * StepInterval + TailDelay,
                () =>
                {
                    hand.seq = null;
                    Flush();
                });

            hand.seq = seq;
        }

        // 도중에 끊긴 연출도 대기 중인 일은 풀어 준다. 풀지 않으면 "카드가 다 열리면
        // 결과를 말한다"고 걸어 둔 것이 영영 실행되지 않아, 결과 카드가 숨은 채로 남는다.
        private void Kill(Hand hand)
        {
            if (hand.seq == null) return;
            var seq = hand.seq;
            hand.seq = null;
            seq.Kill();
            Flush();
        }

        // 마지막 손까지 다 열렸을 때만 대기 중인 일을 처리한다.
        private void Flush()
        {
            if (IsRunning || pending.Count == 0) return;

            var actions = pending.ToArray();
            pending.Clear();
            foreach (var action in actions) action();
        }
    }
}
