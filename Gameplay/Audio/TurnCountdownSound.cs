using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 차례 시계가 마지막 <see cref="StartsAt"/>초에 들어설 때 째깍이는 소리.
    ///
    /// <b>왜 따로 두는가.</b> 시계는 매 프레임 갱신되지만 소리는 <b>넘어서는 순간</b>에 한 번만
    /// 나야 한다. 그 경계를 여기 한 곳에서 지켜, 핫시트와 네트워크가 각자 세는 일이 없게 한다.
    /// (두 디렉터는 시계를 서로 다르게 센다 — 한쪽은 경과 시간을 쌓고 다른 쪽은 마감 틱을 본다.
    /// 하지만 둘 다 (도는 중인가, 남은 초)로 정규화한 뒤라 이 창구를 같이 쓸 수 있다.)
    ///
    /// 시간이 남았는데 차례가 끝나면(먼저 눌렀거나 단계가 넘어갔거나) 소리도 함께 거둔다.
    /// 그러지 않으면 시계가 사라진 화면에 초침만 남는다.
    /// </summary>
    public static class TurnCountdownSound
    {
        /// <summary>
        /// 이 초 아래로 내려가면 소리가 시작된다.
        /// 숫자가 붉게 맥동하기 시작하는 값(DealerCardArranger의 TimerUrgentThreshold)과 같다 —
        /// 눈과 귀가 같은 순간에 급해져야 한 신호로 읽힌다.
        /// </summary>
        public const float StartsAt = 5f;

        private static bool ticking;

        /// <summary>
        /// 플레이를 다시 시작할 때의 초기화. 에디터가 도메인을 다시 읽지 않도록 설정돼 있으면
        /// static 값이 지난 판에서 그대로 넘어오므로, 시작 지점에서 한 번 되돌린다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Reset() => ticking = false;

        /// <summary>
        /// 시계를 갱신하는 자리에서 매 프레임 부른다.
        /// </summary>
        /// <param name="running">지금 시계가 돌고 있는지.</param>
        /// <param name="secondsLeft">남은 시간(초).</param>
        public static void Update(bool running, float secondsLeft)
        {
            bool shouldTick = running && secondsLeft > 0f && secondsLeft <= StartsAt;
            if (shouldTick == ticking) return;

            ticking = shouldTick;
            if (shouldTick) GameAudio.PlayCountdown(secondsLeft);
            else GameAudio.StopCountdown();
        }
    }
}
