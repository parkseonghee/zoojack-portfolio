#if FUSION2 || ZOOJACK_PHOTON_FUSION
using System;
using System.Collections;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 디버그 전용: 커맨드라인 인자로 로비를 자동 연결/역할선택/준비까지 진행한다. 인자가 없으면 아무 동작도 하지 않는다.
    /// 멀티프로세스 테스트용 — 여러 exe를 클릭 없이 자동으로 전체 로비 흐름을 밟게 함.
    ///   ZooJack.exe -zoojackHost TEST -zoojackRole Dealer -logFile host.log
    ///   ZooJack.exe -zoojackJoin TEST -zoojackRole A      -logFile c1.log
    ///   ZooJack.exe -zoojackJoin TEST -zoojackRole B      -logFile c2.log
    /// </summary>
    public class LobbyAutoConnectDebug : MonoBehaviour
    {
        private void Start()
        {
            // 씬 전환(Lobby→Game) 후에도 상태 추적을 이어가기 위해 유지한다 (테스트 전용).
            DontDestroyOnLoad(gameObject);

            var args = Environment.GetCommandLineArgs();
            string mode = null, code = null, role = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-zoojackHost") { mode = "host"; code = args[i + 1]; }
                else if (args[i] == "-zoojackJoin") { mode = "join"; code = args[i + 1]; }
                else if (args[i] == "-zoojackRole") { role = args[i + 1]; }
            }

            if (mode == null)
            {
                Debug.Log("[AutoConnect] 인자 없음 — 자동 연결 비활성.");
                return;
            }

            float delay = mode == "host" ? 1.5f : 5.0f; // 클라는 호스트가 방을 먼저 만들 시간을 줌
            Debug.Log($"[AutoConnect] mode={mode} code={code} role={role} delay={delay}s");
            StartCoroutine(RunFlow(mode, code, role, delay));
        }

        private IEnumerator RunFlow(string mode, string code, string role, float delay)
        {
            yield return new WaitForSeconds(delay);

            var mgr = FindFirstObjectByType<NetworkLobbyManager>();
            if (mgr == null) { Debug.LogError("[AutoConnect] NetworkLobbyManager를 찾지 못함."); yield break; }

            var t = typeof(NetworkLobbyManager);
            const BindingFlags BF = BindingFlags.NonPublic | BindingFlags.Instance;

            // 1) 방 생성/참가
            // 버튼을 누르는 대신 세션 시작을 직접 부른다. 참가는 이제 방 목록을 거치므로
            // (목록이 갱신될 때까지 기다렸다가 해당 줄을 눌러야 한다) 버튼 흉내로는
            // 재현할 수 없다. 어차피 두 경로가 같은 메서드로 모이므로 여기를 부른다.
            var start = t.GetMethod("StartSession", BF);
            if (start == null) { Debug.LogError("[AutoConnect] StartSession 메서드 없음."); yield break; }
            Debug.Log($"[AutoConnect] STEP1 {mode} — room '{code}'");
            start.Invoke(mgr, new object[]
            {
                mode == "host" ? Fusion.GameMode.Host : Fusion.GameMode.Client, code, ""
            });

            // 2) LocalInstance 준비 대기 (최대 12초)
            float waited = 0f;
            while (FusionGameState.LocalInstance == null && waited < 12f)
            {
                yield return new WaitForSeconds(0.5f);
                waited += 0.5f;
            }
            if (FusionGameState.LocalInstance == null)
            {
                Debug.LogError("[AutoConnect] STEP2 실패 — LocalInstance 12초 내 미해결.");
                yield break;
            }
            Debug.Log($"[AutoConnect] STEP2 LocalInstance OK ({waited}s)  Connected={FusionGameState.LocalInstance.ConnectedPlayerCount}/{FusionGameState.MaxPlayers}");

            // 3) 캐릭터 선택 — 동물이 곧 자리이므로 -zoojackRole 인자를 그대로 쓴다.
            yield return new WaitForSeconds(1.5f);
            var seat = role == "Dealer" ? PlayerRole.Dealer
                     : role == "B"      ? PlayerRole.PlayerB
                                        : PlayerRole.PlayerA;
            var character = CharacterIdentity.FromInitialRole(seat);

            var select = t.GetMethod("SelectCharacter", BF);
            if (select != null)
            {
                Debug.Log($"[AutoConnect] STEP3 캐릭터 '{CharacterIdentity.NameKo(character)}' ({seat}) 선택");
                select.Invoke(mgr, new object[] { character });
            }
            else Debug.LogError("[AutoConnect] SelectCharacter 메서드 없음.");

            // 4) 준비
            yield return new WaitForSeconds(1.5f);
            var ready = t.GetMethod("ToggleReady", BF);
            if (ready != null) { Debug.Log("[AutoConnect] STEP4 준비"); ready.Invoke(mgr, null); }
            else Debug.LogError("[AutoConnect] ToggleReady 메서드 없음.");

            // 5) 이후 상태/씬 전환을 2초마다 20초간 추적 (게임 시작 전환 캡처)
            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForSeconds(2f);
                var gs = FusionGameState.LocalInstance;
                string scene = SceneManager.GetActiveScene().name;
                if (gs != null)
                    Debug.Log($"[AutoConnect] WATCH +{(i + 1) * 2}s  scene={scene}  gsAlive=YES  Connected={gs.ConnectedPlayerCount}/3  ReadyMask={ReadMask(gs)}  Phase={gs.CurrentPhase}");
                else
                    Debug.Log($"[AutoConnect] WATCH +{(i + 1) * 2}s  scene={scene}  gsAlive=NO (게임상태 파괴/소실됨)");
            }
        }

        private static string ReadMask(FusionGameState gs)
        {
            var p = typeof(FusionGameState).GetProperty("ReadyMask");
            return p != null ? Convert.ToString((int)p.GetValue(gs), 2).PadLeft(3, '0') : "?";
        }
    }
}
#endif
