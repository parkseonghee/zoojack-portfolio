using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZooJack.Editor
{
    /// <summary>
    /// 로비의 <b>방</b> 화면에 설정·기록·설명서·채팅을 넣는다. 게임 씬에 있는 그것들과
    /// 같은 것이다.
    ///
    /// <b>왜 방에도 필요한가.</b> 매치가 끝나면 모두 방으로 돌아온다. 다음 판을 기다리는
    /// 그 시간이 방금 끝난 매치를 되짚고(기록), 규칙을 다시 읽고(설명서), 소리를 줄이기에
    /// (설정) 가장 좋은 때다. 게임 안에서만 열 수 있으면 그 시간에 할 수 있는 것이 없다.
    /// 무엇보다 <b>말을 나누는 것</b>이 그렇다 — 판이 시작되기를 기다리는 동안 서로에게
    /// 할 말이 가장 많다.
    ///
    /// <b>따로 만들지 않는다.</b> 게임 씬에 쓰는 도구를 그대로 부른다
    /// (<see cref="SettingsPanelSetupTool"/>·<see cref="HistoryPanelSetupTool"/>·
    /// <see cref="HowToPlaySetupTool"/>·<see cref="ChatPanelSetupTool"/>).
    /// 두 벌을 따로 손보면 반드시 어긋나고, 어긋난 쪽은 아무도 눈치채지 못한다.
    /// 붙는 자리는 <see cref="PanelHost"/>가, 게임 씬에만 있는 그림은
    /// <see cref="GameSceneLoan"/>이 대신 챙긴다.
    /// </summary>
    public static class LobbyPanelSetupTool
    {
        [MenuItem("ZooJack/로비/방 화면에 설정·기록·설명서·채팅 넣기")]
        public static void Setup()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (PanelHost.LobbyRoom(canvas) == null)
            {
                EditorUtility.DisplayDialog("로비 화면",
                    "열려 있는 씬에서 방 화면(PanelRoom)을 찾지 못했습니다. LobbyScene을 먼저 여세요.",
                    "확인");
                return;
            }

#if FUSION2 || ZOOJACK_PHOTON_FUSION
            LobbyRoomSetupTool.EnsureRoomAvatarParity(
                Object.FindFirstObjectByType<NetworkLobbyManager>(FindObjectsInactive.Include),
                PanelHost.LobbyRoom(canvas) as RectTransform);
#endif

            SettingsPanelSetupTool.Setup();
            HistoryPanelSetupTool.Setup();
            HowToPlaySetupTool.Setup();
            ChatPanelSetupTool.Setup();

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[LobbyPanelSetupTool] 방 화면에 설정·기록·설명서·채팅을 넣었습니다. " +
                      "방 판 안에 들어가므로 메인 메뉴에서는 보이지 않습니다.");
        }
    }
}
