using UnityEngine;

namespace ZooJack
{
    /// <summary>
    /// 남은 시간을 화면에 적을 정수로 바꾼다.
    ///
    /// 같은 시계를 두 곳이 함께 보여 준다 — 캐릭터 머리 위 명패와 상단 타이머 카드.
    /// 변환이 두 곳에 흩어져 있으면 한쪽은 올림, 다른 쪽은 반올림이 되어 같은 순간에
    /// 다른 숫자가 뜬다(실제로 1초씩 어긋나 있었다). 그래서 여기 한 곳만 둔다.
    ///
    /// 올림을 쓰는 이유: 1초가 조금이라도 남아 있으면 "1s"로 보여야 한다.
    /// 반올림하면 0.5초 남은 시점에 이미 "0s"가 되어, 아직 누를 수 있는데 끝난 것처럼 보인다.
    /// </summary>
    public static class TurnClock
    {
        /// <summary>화면에 적을 남은 초. 음수는 0으로 접는다.</summary>
        public static int Seconds(float secondsLeft) =>
            Mathf.CeilToInt(Mathf.Max(0f, secondsLeft));

        /// <summary>"12s" 꼴의 표시 문자열.</summary>
        public static string Label(float secondsLeft) => Seconds(secondsLeft) + "s";
    }
}
