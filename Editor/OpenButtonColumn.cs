using UnityEngine;

namespace ZooJack.Editor
{
    /// <summary>
    /// 왼쪽 위에 세로로 서는 여는 버튼 셋(설정·기록·설명서)의 자리.
    ///
    /// <b>왜 한곳에 모으는가.</b> 셋은 각자 다른 도구가 만든다. 자리를 도구마다 따로 적어
    /// 두면 한 도구만 다시 돌려도 그 하나가 열에서 빠져나오고, 세 숫자를 나란히 놓고
    /// 보는 사람이 없어 아무도 눈치채지 못한다. 실제로 설명서 버튼이 혼자
    /// 어긋난 자리로 흘러갔다.
    ///
    /// 자리는 <b>칸 번호</b>로만 고른다. 도구는 자기가 몇 번째 칸인지만 알면 되고,
    /// 열의 시작점과 간격을 바꾸고 싶으면 여기 두 숫자만 고치면 셋이 함께 움직인다.
    /// </summary>
    internal static class OpenButtonColumn
    {
        /// <summary>위에서부터의 칸 번호. 순서를 바꾸면 화면의 순서가 바뀐다.</summary>
        public enum Slot
        {
            Settings = 0,
            History = 1,
            HowToPlay = 2
        }

        /// <summary>버튼 하나의 크기. 아이콘(80×80)보다 넓어 누르기 쉬운 넓이를 준다.</summary>
        public static readonly Vector2 Size = new Vector2(108f, 52f);

        /// <summary>열의 가로 자리. 화면 왼쪽 끝에서 이만큼 안쪽이다.</summary>
        private const float X = 74f;

        /// <summary>첫 칸. 상태바(높이 100) 아래로 충분히 내려온 자리다.</summary>
        private const float FirstY = -142f;

        /// <summary>칸 사이 간격.</summary>
        private const float Step = 62f;

        /// <summary>
        /// 이 칸의 자리. 앵커는 왼쪽 위(0,1), 피벗은 가운데라고 보고 계산한 값이다 —
        /// 버튼을 만드는 쪽이 그렇게 세워야 이 값이 맞는다.
        /// </summary>
        public static Vector2 PositionFor(Slot slot) =>
            new Vector2(X, FirstY - Step * (int)slot);
    }
}
