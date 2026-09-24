using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZooJack.Editor
{
    /// <summary>
    /// 게임 씬에만 있는 것을 빌리는 동안 그 씬을 잠깐 겹쳐 연다.
    ///
    /// 설정·기록·설명서는 로비에도 붙는다(<see cref="LobbyPanelSetupTool"/>). 그런데 이
    /// 화면들을 만들려면 게임 씬에만 있는 것이 몇 가지 필요하다 — 머니바가 쓰는 금화 그림
    /// 묶음, GameDirector가 들고 있는 초상화, 카드 그림 묶음. 경로로 박아 두면 그림을 갈아
    /// 끼울 때 <b>로비 쪽만</b> 옛것으로 남으므로, 게임이 실제로 쓰는 그 자리에서 빌려 온다.
    ///
    /// <b>자산만 가져올 수 있다.</b> 빌린 씬은 곧 닫히므로 그 안의 오브젝트를 들고 나오면
    /// 파괴된 참조가 된다. 스프라이트·글꼴처럼 프로젝트에 있는 것만 꺼내라.
    /// </summary>
    internal static class GameSceneLoan
    {
        /// <summary>
        /// <paramref name="borrow"/>를 실행하되, 게임 씬이 열려 있지 않으면 겹쳐 열어 두고
        /// 실행한 뒤 도로 닫는다. 게임 씬에서 도구를 돌리는 경우(대부분)에는 아무 일도
        /// 하지 않고 그냥 실행한다.
        /// </summary>
        public static void While(System.Action borrow)
        {
            if (borrow == null) return;

            if (Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include) != null)
            {
                borrow();
                return;
            }

            string path = FindGameScenePath();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[GameSceneLoan] 게임 씬을 찾지 못했습니다. 빌려 올 그림 없이 " +
                                 "만듭니다 — 빈 자리는 인스펙터에서 채워 주세요.");
                borrow();
                return;
            }

            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { borrow(); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }

        /// <summary>
        /// 게임 씬 이름은 <see cref="ZooJackScenes.Game"/> 한 곳에만 있다.
        ///
        /// 예전에는 이 이름이 로비의 직렬화 필드에만 있어서, 여기서 로비 오브젝트를 찾아
        /// <c>SerializedObject</c>로 그 필드를 문자열로 들여다봐야 했다. 로비 씬이 열려
        /// 있지 않으면 실패했고, 필드 이름을 바꾸면 컴파일은 통과한 채 조용히 null이 됐다.
        /// </summary>
        private static string FindGameScenePath()
        {
            const string name = ZooJackScenes.Game;

            // 빌드 설정에 든 것이 실제로 열리는 씬이다. 같은 이름의 사본이 어딘가 있어도
            // 게임이 여는 것은 이쪽이다.
            foreach (var entry in EditorBuildSettings.scenes)
                if (System.IO.Path.GetFileNameWithoutExtension(entry.path) == name)
                    return entry.path;

            foreach (string guid in AssetDatabase.FindAssets(name + " t:Scene"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return path;
            }

            return null;
        }
    }
}
