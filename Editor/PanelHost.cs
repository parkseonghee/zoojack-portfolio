using UnityEngine;

namespace ZooJack.Editor
{
    /// <summary>
    /// 떠 있는 화면(설정·기록·설명서)을 어디에 매달지 고른다.
    ///
    /// <b>게임 씬</b>은 화면이 하나뿐이라 Canvas 바로 아래가 곧 화면 전체다.
    ///
    /// <b>로비 씬</b>은 판 여러 개가 번갈아 뜬다(메인·방 만들기·방 찾기·설정·방).
    /// 이 셋은 <b>방에서만</b> 쓸 것이라 방 판 안에 넣는다. Canvas 바로 아래에 두면
    /// 메인 메뉴 위에도 떠서 그쪽의 '설정' 버튼과 나란히 두 개가 된다.
    /// </summary>
    internal static class PanelHost
    {
        /// <summary>로비에서 방 화면을 담고 있는 판.</summary>
        private const string LobbyRoomName = "PanelRoom";

        public static Transform For(Canvas canvas)
        {
            if (canvas == null) return null;

            var room = LobbyRoom(canvas);
            return room != null ? room : canvas.transform;
        }

        /// <summary>로비의 방 판. 없으면 null이고, 그것이 곧 '로비가 아니다'라는 뜻이다.</summary>
        public static Transform LobbyRoom(Canvas canvas) =>
            canvas == null ? null : canvas.transform.Find(LobbyRoomName);
    }
}
