#if FUSION2 || ZOOJACK_PHOTON_FUSION
using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ZooJack
{
    /// <summary>
    /// 로비 씬 흐름 제어.
    /// 메인 메뉴 → (서버 만들기 | 서버 찾기 | 설정) → 방 로비 → 게임 씬.
    /// 방 이름은 Photon 세션 이름이 된다. 공개방은 비밀번호 없이 열리고,
    /// 비공개방은 생성 화면에서 고른 비밀번호로 보호한다.
    /// </summary>
    public class NetworkLobbyManager : MonoBehaviour, INetworkRunnerCallbacks
    {
        public const int MaxRoomRows = 8;

        const string PropPassword  = "pw";               // 세션 프로퍼티 키(비밀번호 해시)

        [Header("메인 메뉴")]
        [SerializeField] private GameObject panelMain;
        [SerializeField] private Button btnGoCreate;
        [SerializeField] private Button btnGoBrowse;
        [SerializeField] private Button btnGoSettings;
        [SerializeField] private Button btnQuit;

        [Header("서버 만들기")]
        [SerializeField] private GameObject panelCreate;
        [SerializeField] private TMP_InputField inputRoomName;
        [SerializeField] private TMP_InputField inputRoomPassword;
        [SerializeField] private Toggle togglePublicRoom;
        [SerializeField] private Toggle togglePrivateRoom;
        [SerializeField] private Button btnCreateConfirm;
        [SerializeField] private Button btnCreateBack;

        [Header("서버 찾기")]
        [SerializeField] private GameObject panelBrowse;
        [SerializeField] private GameObject[] roomRows;
        [SerializeField] private TextMeshProUGUI[] roomRowNames;
        [SerializeField] private TextMeshProUGUI[] roomRowInfos;
        [SerializeField] private Button[] roomRowButtons;
        [SerializeField] private TextMeshProUGUI txtBrowseEmpty;
        [SerializeField] private Button btnRefresh;
        [SerializeField] private Button btnJoinSelected;
        [SerializeField] private Button btnBrowseBack;

        [Header("비밀번호 입력")]
        [SerializeField] private GameObject panelPassword;
        [SerializeField] private TextMeshProUGUI txtPasswordTarget;
        [SerializeField] private TMP_InputField inputJoinPassword;
        [SerializeField] private Button btnPasswordConfirm;
        [SerializeField] private Button btnPasswordCancel;

        [Header("설정")]
        [SerializeField] private GameObject panelSettings;
        [SerializeField] private TMP_InputField inputNickname;
        [SerializeField] private Button btnSettingsBack;

        [Header("방 로비 (테이블)")]
        [SerializeField] private GameObject panelRoom;
        [SerializeField] private TextMeshProUGUI txtRoomInfo;

        [Tooltip("방에 들어온 사람 수. 방 화면 도구(ZooJack/로비/방 인원 표시 만들기)가 배선한다.")]
        [SerializeField] private TextMeshProUGUI txtRoomCount;
        [SerializeField] private AvatarStage avatarStage;
        [SerializeField] private EmotionWheelController emotionWheel;
        [SerializeField] private LobbyCharacterCard[] characterCards;

        [Tooltip("아직 자리를 고르지 않은 사람에게 띄우는 '역할을 선택하세요'. " +
                 "방 화면 도구(ZooJack/로비/캐릭터 선택 안내 만들기)가 배선한다.")]
        [SerializeField] private GameObject selectPrompt;
        [SerializeField] private Button btnReady;
        [SerializeField] private TextMeshProUGUI txtReadyLabel;
        [SerializeField] private Button btnLeave;

        [Header("테이블 위 캐릭터 스프라이트")]
        [SerializeField] private Sprite avatarRabbit;
        [SerializeField] private Sprite avatarFox;
        [SerializeField] private Sprite avatarCroc;

        [Header("공통")]
        [SerializeField] private TextMeshProUGUI txtStatus;

        [Header("Fusion 설정")]
        // 매치가 잡히면 여는 씬은 ZooJackScenes.Game이다. 예전에는 이 이름이 여기
        // 직렬화 필드로 있어, 씬 이름을 바꾸면 인스펙터를 열어 보기 전에는 "방은 만들어지는데
        // 게임이 안 열린다"로만 드러났다.
        [SerializeField] private FusionGameState gameStatePrefab;

        // 게임 세션 러너와 방 목록 조회용 러너를 분리해 콜백 출처를 명확히 한다.
        private NetworkRunner _runner;
        private NetworkRunner _browseRunner;

        private FusionGameState _gameState;
        private bool _isStarting;
        private bool _startRequested;
        private bool _gameLoading;

        // 로비에서 캐릭터를 보여줄 세 자리. 동물과 1:1이라 자리가 곧 캐릭터다.
        private static readonly PlayerRole[] SeatRoles =
        {
            PlayerRole.PlayerA, PlayerRole.PlayerB, PlayerRole.Dealer
        };

        // 자리가 방금 찼는지 판단하려고 직전 프레임의 점유 상태를 들고 있는다.
        // 나타나는 순간에만 순간이동시켜야, 앞사람이 서 있던 자리에서 미끄러져 오지 않는다.
        private readonly bool[] _seatShown = new bool[SeatRoles.Length];
        private bool _avatarWired;
        private bool _emotionWired;
        private bool _promptedForCharacter;
        private bool _roomChatGuidePending;
        private string _roomChatGuideRoomName;
        private readonly int[] _lastEmotionSequences =
            new int[FusionGameState.MaxPlayers + 1];
        private bool _hasPendingLocalEmotion;
        private EmotionId _pendingLocalEmotion;
        private PlayerRole _pendingLocalEmotionRole;
        private float _pendingLocalEmotionExpiry;
        private float _nextLocalEmotionTime;

        private readonly List<SessionInfo> _sessions = new List<SessionInfo>();
        private SessionInfo _pendingJoin;   // 비밀번호 입력 대기 중인 방
        private string _selectedRoomName;   // 목록에서 선택만 된 방. 참가 버튼 전에는 접속하지 않는다.
        private int _hostPasswordHash;      // 호스트가 접속 요청을 검증할 때 쓰는 해시

        private static readonly Color BrowseRowBorder =
            new Color(0.541f, 0.478f, 0.353f, 0.96f);
        private static readonly Color BrowseRowSelectedBorder =
            new Color(1f, 0.64f, 0.12f, 1f);

        private void Awake()
        {
            if (emotionWheel == null)
                emotionWheel = EmotionWheelController.FindInScene();
            emotionWheel?.SetOpenGuard(CanOpenLobbyEmotionWheel);

            btnGoCreate.onClick.AddListener(OpenCreate);
            btnGoBrowse.onClick.AddListener(OpenBrowse);
            btnGoSettings.onClick.AddListener(OpenSettings);
            btnQuit.onClick.AddListener(QuitGame);

            btnCreateConfirm.onClick.AddListener(CreateRoom);
            btnCreateBack.onClick.AddListener(BackToMain);
            if (togglePublicRoom != null)
                togglePublicRoom.onValueChanged.AddListener(isOn =>
                {
                    if (!isOn) return;
                    GameAudio.PlayClick();
                    ApplyRoomPrivacy(false);
                });
            if (togglePrivateRoom != null)
                togglePrivateRoom.onValueChanged.AddListener(isOn =>
                {
                    if (!isOn) return;
                    GameAudio.PlayClick();
                    ApplyRoomPrivacy(true);
                });
            ApplyRoomPrivacy(togglePrivateRoom != null && togglePrivateRoom.isOn);

            btnRefresh.onClick.AddListener(OpenBrowse);
            if (btnJoinSelected != null)
                btnJoinSelected.onClick.AddListener(JoinSelectedRoom);
            btnBrowseBack.onClick.AddListener(BackToMain);

            btnPasswordConfirm.onClick.AddListener(ConfirmPassword);
            btnPasswordCancel.onClick.AddListener(() => { _pendingJoin = null; ShowOnly(panelBrowse); });

            btnSettingsBack.onClick.AddListener(CloseSettingsAndBack);

            for (int i = 0; i < roomRowButtons.Length; i++)
            {
                int index = i; // 클로저 캡처
                if (roomRowButtons[i] != null)
                    roomRowButtons[i].onClick.AddListener(() => OnRoomRowClicked(index));
            }

            if (characterCards != null)
                foreach (var card in characterCards)
                    if (card != null) card.Clicked += SelectCharacter;

            btnReady.onClick.AddListener(ToggleReady);
            btnLeave.onClick.AddListener(LeaveRoom);

            WireUiSounds();

            // 이름 짓는 자리는 환경설정으로 옮겨 갔다(SettingsPanel). 이 칸은 화면에서
            // 지워졌지만, 남아 있다면 같은 값을 보여 준다 — 저장 열쇠가 둘이 되면 어느
            // 쪽이 진짜 이름인지가 눌러 본 순서에 따라 달라진다.
            if (inputNickname != null) inputNickname.text = GameSettings.Nickname;

            if (AdoptRunningSession()) return;

            ShowOnly(panelMain);
            SetStatus(ZooJackText.Get("Lobby.Status.Start", "서버를 만들거나 찾아서 참가하세요."));
        }

        /// <summary>
        /// 게임에서 돌아온 경우를 이어받는다. 매치가 끝나고 전원이 '다시 플레이'를 누르면
        /// 호스트가 <c>runner.LoadScene</c>으로 이 씬을 다시 여는데, 러너와
        /// <see cref="FusionGameState"/>는 살아 있으므로 방을 새로 만들 필요가 없다.
        ///
        /// 이 씬은 처음 실행될 때도 열리므로(메인 메뉴), 러너가 돌고 있는지로 둘을 가른다.
        /// 이걸 하지 않으면 매치를 마친 세 사람이 서로 연결된 채 메인 메뉴를 보게 된다.
        /// </summary>
        private bool AdoptRunningSession()
        {
            var runner = FindFirstObjectByType<NetworkRunner>();
            if (runner == null || !runner.IsRunning) return false;

            _runner = runner;
            _runner.AddCallbacks(this);

            // 비밀번호 해시는 씬과 함께 사라진다. 호스트가 이걸 잃으면 접속 검증
            // (OnConnectRequest)이 무방비가 되므로 세션 프로퍼티에서 되찾는다.
            _hostPasswordHash = 0;
            if (runner.SessionInfo != null
                && runner.SessionInfo.Properties.TryGetValue(PropPassword, out var pw))
                _hostPasswordHash = (int)pw;

            // 앞 매치의 자리 점유 기록이 없는 상태에서 출발한다. 남아 있으면 같은 자리에
            // 앉는 사람이 순간이동 없이 미끄러져 온다.
            ResetSeatVisuals();

            ShowOnly(panelRoom);
            string roomName = runner.SessionInfo != null ? runner.SessionInfo.Name : "?";
            txtRoomInfo.text = ZooJackText.Get("Lobby.Room.Info", "방: {0}  [{1}]",
                                 roomName, runner.IsServer
                                     ? ZooJackText.Get("Lobby.Room.Host", "방장")
                                     : ZooJackText.Get("Lobby.Room.Guest", "참가자"))
                             + (_hostPasswordHash != 0 ? "  🔒" : "");
            SetStatus(ZooJackText.Get("Lobby.Status.MatchEnded",
                "매치가 끝났습니다. 캐릭터를 다시 고르세요."));
            _promptedForCharacter = false;
            SetActionButtonsInteractable(false);
            QueueRoomChatGuide(roomName);
            return true;
        }

        /// <summary>
        /// 방 입장이 끝난 로컬 플레이어에게 채팅 위치를 알려 준다.
        /// 서버 방송이 아니라 각 클라이언트가 직접 남기므로 늦게 참가해도 빠지지 않는다.
        /// </summary>
        private void QueueRoomChatGuide(string roomName)
        {
            _roomChatGuideRoomName = string.IsNullOrWhiteSpace(roomName) ? "?" : roomName;
            _roomChatGuidePending = true;
        }

        private void ShowRoomChatGuideIfPending()
        {
            if (!_roomChatGuidePending || panelRoom == null || !panelRoom.activeInHierarchy)
                return;

            _roomChatGuidePending = false;
            ChatLog.AddSystem(ZooJackText.Get(
                "Chat.Guide.RoomEntered",
                "\"{0}\" 로비에 입장하였습니다.",
                _roomChatGuideRoomName));
        }

        /// <summary>
        /// 로비 버튼을 누를 때 나는 소리를 전부 여기서 붙인다.
        ///
        /// <b>목록이 여기 하나뿐인 이유.</b> 게임 화면 쪽 목록은 <c>GameDirector.WireUiSounds</c>에
        /// 있다. 두 화면이 각자 자기 버튼을 들고 있으므로 목록도 각자 두되, 붙이는 방법은
        /// <see cref="GameAudio.AttachClick"/> 하나를 지난다.
        ///
        /// 설정·기록·설명서·채팅은 자기 버튼 소리를 스스로 내므로 여기 넣지 않는다.
        /// </summary>
        private void WireUiSounds()
        {
            GameAudio.AttachClick(
                btnGoCreate, btnGoBrowse, btnGoSettings, btnQuit,   // 메인 메뉴
                btnCreateConfirm, btnCreateBack,                    // 방 만들기
                btnRefresh, btnJoinSelected, btnBrowseBack,         // 방 찾기
                btnPasswordConfirm, btnPasswordCancel,              // 비밀번호
                btnSettingsBack,                                    // 설정
                btnReady, btnLeave);                                // 방 로비

            // 방 목록의 줄들. 몇 줄인지는 씬이 정하므로 배열째로 넘긴다.
            GameAudio.AttachClick(roomRowButtons);

            // 캐릭터를 고르는 카드. 카드는 자기 버튼을 내놓기만 하고 소리는 내지 않는다 —
            // 로비의 소리 목록은 여기 하나로 유지한다.
            if (characterCards == null) return;
            foreach (var card in characterCards)
                if (card != null) GameAudio.AttachClick(card.Button);
        }

        private void OnDestroy()
        {
            // 씬 전환(Lobby→Game)으로 파괴될 때 러너 콜백을 해제한다.
            // 파괴된 컴포넌트로 OnShutdown 등이 호출되면 MissingReferenceException이 발생한다.
            // 게임 씬에서의 세션 종료 처리는 NetworkGameDirector가 담당한다.
            if (_runner != null)       _runner.RemoveCallbacks(this);
            if (_browseRunner != null) _browseRunner.RemoveCallbacks(this);

            UnwireAvatarStage();
            UnwireEmotionSystem();
        }

        // AvatarStage는 네트워크 수명주기를 모른 채 매 프레임 돈다. 세션이 끊긴 뒤에도
        // 이벤트가 살아 있으면 죽은 러너로 RPC를 쏘게 되므로 짝을 맞춰 풀어 준다.
        private void WireAvatarStage()
        {
            if (avatarStage == null || _avatarWired) return;
            avatarStage.LocalPositionChanged += OnLocalAvatarMoved;
            _avatarWired = true;
        }

        private void WireEmotionSystem()
        {
            if (_emotionWired || _gameState == null || avatarStage == null) return;
            if (emotionWheel == null)
                emotionWheel = EmotionWheelController.FindInScene();
            if (emotionWheel == null) return;

            emotionWheel.EmotionSelected += OnLocalEmotionSelected;
            emotionWheel.SetOpenGuard(CanOpenLobbyEmotionWheel);
            _gameState.EmotionBroadcastReceived += OnEmotionBroadcastReceived;
            _emotionWired = true;
        }

        private void UnwireEmotionSystem()
        {
            if (emotionWheel != null)
            {
                if (_emotionWired)
                    emotionWheel.EmotionSelected -= OnLocalEmotionSelected;
                emotionWheel.SetOpenGuard(null);
                emotionWheel.CancelSelection();
            }
            if (_gameState != null && _emotionWired)
                _gameState.EmotionBroadcastReceived -= OnEmotionBroadcastReceived;
            _emotionWired = false;
        }

        private bool CanOpenLobbyEmotionWheel()
        {
            if (!isActiveAndEnabled || _gameLoading || panelRoom == null || !panelRoom.activeInHierarchy)
                return false;
            if (_gameState == null || _gameState.Object == null || !_gameState.Object.IsValid ||
                _runner == null || !_runner.IsRunning)
                return false;
            if (Time.unscaledTime < _nextLocalEmotionTime) return false;

            var role = _gameState.GetLocalPublicRole();
            if (role == PlayerRole.None || avatarStage == null || !avatarStage.IsVisible(role))
                return false;
            return !GameDirector.IsTextInputFocused();
        }

        private void OnLocalEmotionSelected(EmotionId emotionId)
        {
            if (!CanOpenLobbyEmotionWheel()) return;

            var role = _gameState.GetLocalPublicRole();
            avatarStage.ShowEmotion(role, emotionId);
            _hasPendingLocalEmotion = true;
            _pendingLocalEmotion = emotionId;
            _pendingLocalEmotionRole = role;
            _pendingLocalEmotionExpiry = Time.unscaledTime + 2f;
            _nextLocalEmotionTime = Time.unscaledTime + FusionGameState.EmotionCooldownSeconds;
            _gameState.Rpc_RequestEmotion((int)emotionId);
        }

        private void OnEmotionBroadcastReceived(int playerSlot, EmotionId emotionId, int sequence)
        {
            if (playerSlot < 1 || playerSlot >= _lastEmotionSequences.Length) return;
            if (sequence <= _lastEmotionSequences[playerSlot]) return;
            _lastEmotionSequences[playerSlot] = sequence;

            var role = _gameState != null ? _gameState.RoleForSlot(playerSlot) : PlayerRole.None;
            if (role == PlayerRole.None || avatarStage == null) return;

            bool predictedLocal = playerSlot == _gameState.LocalSlot &&
                                  _hasPendingLocalEmotion &&
                                  _pendingLocalEmotion == emotionId &&
                                  Time.unscaledTime <= _pendingLocalEmotionExpiry;
            if (predictedLocal)
            {
                _hasPendingLocalEmotion = false;
                if (role == _pendingLocalEmotionRole) return;
                avatarStage.HideEmotion(_pendingLocalEmotionRole);
            }

            if (avatarStage.IsVisible(role)) avatarStage.ShowEmotion(role, emotionId);
        }

        private void UnwireAvatarStage()
        {
            if (avatarStage == null || !_avatarWired) return;
            avatarStage.LocalPositionChanged -= OnLocalAvatarMoved;
            _avatarWired = false;
        }

        private void OnLocalAvatarMoved(Vector2 position)
        {
            if (_gameState == null || _runner == null || !_runner.IsRunning) return;
            _gameState.Rpc_ReportAvatarPosition(position);
        }

        private void Update()
        {
            // Awake에서 방 화면을 켠 경우에도 ChatPanel.OnEnable이 구독을 마친 다음 프레임에
            // 안내를 남겨야 즉시 또렷하게 보인다.
            ShowRoomChatGuideIfPending();

            // 방 로비에 들어간 뒤에만 게임 상태를 폴링한다.
            if (_runner == null || panelRoom == null || !panelRoom.activeSelf) return;

            // LocalInstance는 FusionGameState.Spawned() 안에서 설정됨 (안전한 접근 시점)
            // FindObjectOfType은 Spawned() 전에 컴포넌트를 반환해 Networked 프로퍼티 예외를 유발하므로 사용 금지
            if (_gameState == null)
                _gameState = FusionGameState.LocalInstance;

            if (_gameState == null)
            {
                SetStatus(ZooJackText.Get("Lobby.Status.Initializing", "서버 초기화 중..."));
                SetActionButtonsInteractable(false);
                return;
            }

            if (_gameLoading) return;

            WireAvatarStage();
            WireEmotionSystem();
            RefreshRoom();

            if (_gameState.CurrentPhase == GamePhase.RoleAssignment ||
                _gameState.CurrentPhase == GamePhase.BribeSelection)
            {
                _gameLoading = true;
                SetStatus(ZooJackText.Get("Lobby.Status.GameLoading", "게임 시작! 로딩 중..."));

                // 씬이 넘어가는 동안에는 조종을 끊는다. 여기서 놓아 두면 로딩 중에도
                // 캐릭터가 걸어다니며 위치 보고를 계속 올리는데, 그 값은 게임 씬이
                // 역할 배정과 함께 시작 위치로 덮어쓸 값이라 의미가 없다.
                avatarStage?.SetLocalRole(PlayerRole.None);

                // 호스트만 Fusion 네트워크 씬 로드를 호출한다. Fusion이 모든 클라이언트에
                // 씬 전환을 복제하며, 런너 스폰 객체(FusionGameState)는 그대로 유지된다.
                // Unity의 SceneManager.LoadScene을 쓰면 네트워크 씬오브젝트가 파괴되어
                // 세션이 붕괴하므로 절대 사용하지 않는다.
                if (_runner.IsServer)
                {
                    var sceneRef = ResolveSceneRef(ZooJackScenes.Game);
                    if (sceneRef.IsValid)
                        _runner.LoadScene(sceneRef, LoadSceneMode.Single);
                    else
                        Debug.LogError($"[Lobby] '{ZooJackScenes.Game}' 씬을 빌드 설정에서 찾을 수 없습니다.");
                }
            }
        }

        // 씬 이름으로 빌드 인덱스 기반 SceneRef를 해석한다 (빌드 인덱스 하드코딩 방지).
        private SceneRef ResolveSceneRef(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    return SceneRef.FromIndex(i);
            }
            return SceneRef.None;
        }

        // ── 메인 메뉴 ────────────────────────────────────────────────

        private void OpenCreate()
        {
            ApplyRoomPrivacy(false);
            ShowOnly(panelCreate);
            SetStatus(ZooJackText.Get("Lobby.Status.CreateHint",
                "방 공개 여부와 이름을 설정하세요."));
        }

        /// <summary>
        /// 공개방에서는 비밀번호를 받을 이유가 없으므로 입력을 잠그고 남은 값을 지운다.
        /// 토글은 ToggleGroup이 한쪽만 켜지게 하지만, 코드에서도 상태를 맞춰 두어
        /// 씬 배선이 바뀌어도 공개/비공개가 동시에 선택되는 일을 막는다.
        /// </summary>
        private void ApplyRoomPrivacy(bool privateRoom)
        {
            if (togglePublicRoom == null && togglePrivateRoom == null)
            {
                if (inputRoomPassword != null) inputRoomPassword.interactable = true;
                return;
            }

            togglePublicRoom?.SetIsOnWithoutNotify(!privateRoom);
            togglePrivateRoom?.SetIsOnWithoutNotify(privateRoom);

            if (inputRoomPassword == null) return;
            inputRoomPassword.interactable = privateRoom;
            CanvasGroup passwordGroup = inputRoomPassword.GetComponent<CanvasGroup>();
            if (passwordGroup != null) passwordGroup.alpha = privateRoom ? 1f : 0.5f;
            if (!privateRoom) inputRoomPassword.text = string.Empty;
        }

        private void OpenSettings()
        {
            ShowOnly(panelSettings);
            SetStatus(ZooJackText.Get("Lobby.Status.Settings", "환경 설정"));
        }

        private void CloseSettingsAndBack()
        {
            if (inputNickname != null) GameSettings.SetNickname(inputNickname.text);
            GameSettings.Save();
            BackToMain();
        }

        private void BackToMain()
        {
            ShutdownBrowseRunner();
            ClearRoomSelection();
            ShowOnly(panelMain);
            SetStatus(ZooJackText.Get("Lobby.Status.Start", "서버를 만들거나 찾아서 참가하세요."));
        }

        private void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── 서버 만들기 ──────────────────────────────────────────────

        private void CreateRoom()
        {
            string name = inputRoomName.text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                SetStatus(ZooJackText.Get("Lobby.Status.EnterRoomName", "방 이름을 입력하세요."));
                return;
            }

            bool privateRoom = togglePrivateRoom != null
                ? togglePrivateRoom.isOn
                : inputRoomPassword != null && !string.IsNullOrEmpty(inputRoomPassword.text);
            string password = privateRoom && inputRoomPassword != null
                ? inputRoomPassword.text
                : string.Empty;
            if (privateRoom && string.IsNullOrEmpty(password))
            {
                SetStatus(ZooJackText.Get("Lobby.Status.EnterCreatePassword",
                    "비공개방 비밀번호를 입력하세요."));
                return;
            }
            StartSession(GameMode.Host, name, password);
        }

        // ── 서버 찾기 ────────────────────────────────────────────────

        private async void OpenBrowse()
        {
            ShowOnly(panelBrowse);
            ClearRoomSelection();

            // 이미 목록 로비에 접속해 있다면 현재 수신된 목록을 다시 그린다.
            // 목록을 비운 뒤 콜백을 기다리면 변경 사항이 없는 경우 빈 화면으로 남을 수 있다.
            if (_browseRunner != null)
            {
                RefreshRoomRows();
                SetBrowseListStatus();
                return;
            }

            _sessions.Clear();
            RefreshRoomRows();
            SetStatus(ZooJackText.Get("Lobby.Status.BrowseLoading", "방 목록을 불러오는 중..."));

            var runnerGO = new GameObject("LobbyBrowseRunner");
            _browseRunner = runnerGO.AddComponent<NetworkRunner>();
            _browseRunner.ProvideInput = false;
            _browseRunner.AddCallbacks(this);

            var result = await _browseRunner.JoinSessionLobby(SessionLobby.ClientServer);
            if (!result.Ok)
            {
                SetStatus(ZooJackText.Get("Lobby.Status.BrowseFailed",
                    "방 목록을 불러오지 못했습니다: {0}", result.ShutdownReason));
                ShutdownBrowseRunner();
                return;
            }
            SetStatus(ZooJackText.Get("Lobby.Status.BrowseSelect", "방을 선택해 참가하세요."));
        }

        private void ShutdownBrowseRunner()
        {
            if (_browseRunner == null) return;
            var runner = _browseRunner;
            _browseRunner = null;
            runner.RemoveCallbacks(this);
            _ = runner.Shutdown();
        }

        private void RefreshRoomRows()
        {
            int selectedIndex = FindSelectedRoomIndex();
            if (selectedIndex >= 0)
            {
                var selected = _sessions[selectedIndex];
                if (!selected.IsOpen || selected.PlayerCount >= selected.MaxPlayers)
                {
                    _selectedRoomName = null;
                    selectedIndex = -1;
                }
            }

            for (int i = 0; i < roomRows.Length; i++)
            {
                bool has = i < _sessions.Count;
                if (roomRows[i] != null) roomRows[i].SetActive(has);
                if (!has) continue;

                var s = _sessions[i];
                bool locked = PasswordHashOf(s) != 0;
                roomRowNames[i].text = (locked ? "🔒 " : "") + s.Name;
                roomRowInfos[i].text = $"{s.PlayerCount}/{s.MaxPlayers}";
                // 정원이 찼거나 닫힌 방은 누를 수 없다.
                roomRowButtons[i].interactable = s.IsOpen && s.PlayerCount < s.MaxPlayers;

                var frame = roomRows[i].GetComponent<RoundedPanelGraphic>();
                if (frame != null)
                    frame.SetBorder(i == selectedIndex
                        ? BrowseRowSelectedBorder
                        : BrowseRowBorder, i == selectedIndex ? 5f : 2.2f);
            }

            if (txtBrowseEmpty != null)
                txtBrowseEmpty.gameObject.SetActive(_sessions.Count == 0);

            if (selectedIndex < 0) _selectedRoomName = null;
            if (btnJoinSelected != null)
                btnJoinSelected.interactable = selectedIndex >= 0
                    && roomRowButtons[selectedIndex] != null
                    && roomRowButtons[selectedIndex].interactable;
        }

        private void OnRoomRowClicked(int index)
        {
            if (index < 0 || index >= _sessions.Count) return;
            var session = _sessions[index];

            if (!session.IsOpen || session.PlayerCount >= session.MaxPlayers) return;
            _selectedRoomName = session.Name;
            RefreshRoomRows();
            SetStatus(ZooJackText.Get("Lobby.Status.RoomSelected",
                "'{0}' 방을 선택했습니다. 참가 버튼을 눌러 주세요.", session.Name));
        }

        private void JoinSelectedRoom()
        {
            int index = FindSelectedRoomIndex();
            if (index < 0)
            {
                ClearRoomSelection();
                SetStatus(ZooJackText.Get("Lobby.Status.SelectRoomFirst",
                    "참가할 방을 먼저 선택하세요."));
                return;
            }

            var session = _sessions[index];
            if (!session.IsOpen || session.PlayerCount >= session.MaxPlayers)
            {
                ClearRoomSelection();
                RefreshRoomRows();
                SetStatus(ZooJackText.Get("Lobby.Status.RoomUnavailable",
                    "선택한 방에는 지금 참가할 수 없습니다."));
                return;
            }

            if (PasswordHashOf(session) == 0)
            {
                JoinRoom(session, "");
                return;
            }

            _pendingJoin = session;
            if (txtPasswordTarget != null)
                txtPasswordTarget.text = ZooJackText.Get(
                    "Lobby.Password.RoomTitle", "'{0}' 비밀 방", session.Name);
            if (inputJoinPassword != null) inputJoinPassword.text = "";
            ShowOnly(panelPassword);
            SetStatus(ZooJackText.Get("Lobby.Status.EnterPassword", "비밀번호를 입력하세요."));
        }

        private int FindSelectedRoomIndex()
        {
            if (string.IsNullOrEmpty(_selectedRoomName)) return -1;
            for (int i = 0; i < _sessions.Count; i++)
                if (_sessions[i].IsValid && _sessions[i].Name == _selectedRoomName)
                    return i;
            return -1;
        }

        private void ClearRoomSelection()
        {
            _selectedRoomName = null;
            if (btnJoinSelected != null) btnJoinSelected.interactable = false;
        }

        private void SetBrowseListStatus()
        {
            SetStatus(_sessions.Count == 0
                ? ZooJackText.Get("Lobby.Status.NoRooms", "열려 있는 방이 없습니다. 직접 만들어 보세요.")
                : ZooJackText.Get("Lobby.Status.RoomCount", "{0}개의 방을 찾았습니다.", _sessions.Count));
        }

        private void ConfirmPassword()
        {
            if (_pendingJoin == null || !_pendingJoin.IsValid) return;

            string entered = inputJoinPassword != null ? inputJoinPassword.text : "";
            if (HashPassword(entered) != PasswordHashOf(_pendingJoin))
            {
                SetStatus(ZooJackText.Get("Lobby.Status.WrongPassword", "비밀번호가 일치하지 않습니다."));
                return;
            }

            var session = _pendingJoin;
            _pendingJoin = null;
            JoinRoom(session, entered);
        }

        private void JoinRoom(SessionInfo session, string password)
        {
            // 목록 조회용 러너를 정리한 뒤 새 러너로 실제 세션에 참가한다.
            ShutdownBrowseRunner();
            StartSession(GameMode.Client, session.Name, password);
        }

        private static int PasswordHashOf(SessionInfo session)
        {
            if (session.Properties != null
                && session.Properties.TryGetValue(PropPassword, out var prop)
                && prop != null
                && prop.PropertyValue is int hash)
                return hash;
            return 0;
        }

        /// <summary>
        /// 비밀번호를 32비트 FNV-1a 해시로 바꾼다.
        /// string.GetHashCode는 프로세스마다 값이 달라질 수 있어 절대 쓰면 안 된다.
        /// 0은 "비밀번호 없음"을 뜻하므로 결과가 0이면 1로 밀어낸다.
        /// </summary>
        public static int HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password)) return 0;
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in password)
                {
                    h ^= c;
                    h *= 16777619u;
                }
                int result = (int)h;
                return result == 0 ? 1 : result;
            }
        }

        // 엔디안에 의존하지 않도록 빅엔디안으로 직접 직렬화한다.
        private static byte[] TokenOf(int hash) => new[]
        {
            (byte)(hash >> 24), (byte)(hash >> 16), (byte)(hash >> 8), (byte)hash
        };

        private static int HashOfToken(byte[] token) =>
            token == null || token.Length < 4
                ? 0
                : (token[0] << 24) | (token[1] << 16) | (token[2] << 8) | token[3];

        // ── 세션 시작 ────────────────────────────────────────────────

        private async void StartSession(GameMode mode, string roomName, string password)
        {
            if (_isStarting || _runner != null) return;

            _isStarting = true;
            _hostPasswordHash = HashPassword(password);
            SetInteractable(false);
            SetStatus(mode == GameMode.Host
                ? ZooJackText.Get("Lobby.Status.CreatingRoom", "방을 만드는 중...")
                : ZooJackText.Get("Lobby.Status.JoiningRoom", "방에 참가하는 중..."));

            var runnerGO = new GameObject("NetworkRunner");
            _runner = runnerGO.AddComponent<NetworkRunner>();
            _runner.ProvideInput = false;
            _runner.AddCallbacks(this);
            DontDestroyOnLoad(runnerGO);

            var sceneManager = runnerGO.AddComponent<NetworkSceneManagerDefault>();
            var args = new StartGameArgs
            {
                GameMode        = mode,
                SessionName     = roomName,
                PlayerCount     = FusionGameState.MaxPlayers,
                Scene           = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex),
                SceneManager    = sceneManager,
                ConnectionToken = TokenOf(_hostPasswordHash)
            };

            if (mode == GameMode.Host)
            {
                args.IsVisible = true;
                args.IsOpen    = true;
                // 비밀번호 해시를 세션 프로퍼티로 공개해 목록에서 자물쇠를 표시할 수 있게 한다.
                // 해시만 노출되므로 원문 비밀번호는 드러나지 않는다.
                args.SessionProperties = new Dictionary<string, SessionProperty>
                {
                    { PropPassword, SessionProperty.Convert(_hostPasswordHash) }
                };
            }

            var result = await _runner.StartGame(args);
            _isStarting = false;

            if (!result.Ok)
            {
                SetStatus(ZooJackText.Get("Lobby.Status.ConnectionFailed",
                    "연결 실패: {0}", result.ShutdownReason));
                Destroy(runnerGO);
                _runner = null;
                SetInteractable(true);
                ShowOnly(mode == GameMode.Host ? panelCreate : panelBrowse);
                return;
            }

            // 방에 들어설 때는 아무 자리도 차 있지 않은 상태에서 출발한다. 이전 방의
            // 점유 기록이 남아 있으면 같은 자리에 앉은 사람이 순간이동 없이 미끄러져 온다.
            ResetSeatVisuals();

            ShowOnly(panelRoom);
            txtRoomInfo.text = ZooJackText.Get("Lobby.Room.Info", "방: {0}  [{1}]",
                                 roomName, _runner.IsServer
                                     ? ZooJackText.Get("Lobby.Room.Host", "방장")
                                     : ZooJackText.Get("Lobby.Room.Guest", "참가자"))
                             + (_hostPasswordHash != 0 ? "  🔒" : "");
            SetStatus(ZooJackText.Get("Lobby.Status.Initializing", "서버 초기화 중..."));
            _promptedForCharacter = false;
            SetActionButtonsInteractable(false);
            QueueRoomChatGuide(roomName);
        }

        // ── 방 로비 ──────────────────────────────────────────────────

        /// <summary>
        /// 캐릭터를 고른다. 동물이 곧 자리이므로 역할까지 함께 정해진다.
        ///
        /// 로컬에 미리 반영하지 않는다. 호스트가 거절할 수 있고(이미 찬 자리, 준비 중),
        /// 미리 칠해 두면 거절당한 한 프레임 동안 남의 자리에 내 캐릭터가 서 있게 된다.
        /// 화면은 다음 <see cref="RefreshRoom"/>에서 확정된 상태만 보고 그린다.
        /// </summary>
        private void SelectCharacter(CharacterId character)
        {
            if (_gameState == null)
            {
                SetStatus(ZooJackText.Get("Lobby.Status.InitializingRetry",
                    "서버 초기화 중... 잠시 후 다시 시도하세요."));
                return;
            }

            var role = CharacterIdentity.RoleFor(character);
            if (role == PlayerRole.None) return;

            if (_gameState.IsLocalPlayerReady())
            {
                SetStatus(ZooJackText.Get("Lobby.Status.CancelReadyToChange",
                    "준비를 취소해야 캐릭터를 바꿀 수 있습니다."));
                return;
            }

            // 앉아 있는 자리를 다시 누르면 일어선다. 고른 것을 무를 방법이 없으면
            // 잘못 누른 사람은 방을 나갔다 들어오는 수밖에 없었다.
            if (role == _gameState.GetLocalPublicRole())
            {
                _gameState.Rpc_SelectRole(PlayerRole.None);
                SetStatus(ZooJackText.Get("Lobby.Status.CharacterCleared",
                    "선택을 취소했습니다. 아래에서 역할을 고르세요."));
                return;
            }

            _gameState.Rpc_SelectRole(role);
            SetStatus(ZooJackText.Get("Lobby.Status.CharacterSelected",
                "{0} 선택 — {1}. WASD 또는 방향키로 움직여 보세요.",
                CharacterIdentity.NameKo(character), RoleKo(role)));
        }

        private void ToggleReady()
        {
            if (_gameState == null)
            {
                SetStatus(ZooJackText.Get("Lobby.Status.InitializingRetry",
                    "서버 초기화 중... 잠시 후 다시 시도하세요."));
                return;
            }
            if (_gameState.GetLocalPublicRole() == PlayerRole.None)
            {
                SetStatus(ZooJackText.Get("Lobby.Status.SelectCharacterFirst",
                    "캐릭터를 먼저 고르세요."));
                return;
            }

            bool next = !_gameState.IsLocalPlayerReady();
            _gameState.Rpc_SetReady(next);
            SetStatus(next
                ? ZooJackText.Get("Lobby.Status.Ready", "준비 완료! 다른 플레이어를 기다리는 중...")
                : ZooJackText.Get("Lobby.Status.ReadyCanceled", "준비 취소됨."));
        }

        private async void LeaveRoom()
        {
            if (_runner == null) return;

            SetStatus(ZooJackText.Get("Lobby.Status.LeavingRoom", "방에서 나가는 중..."));
            var runnerToShutdown = _runner;
            ResetLobbyState();
            await runnerToShutdown.Shutdown();

            ReturnToMain(ZooJackText.Get("Lobby.Status.LeftRoom", "방에서 나왔습니다."));
        }

        /// <summary>
        /// 방 화면을 확정된 네트워크 상태만 보고 다시 그린다. 카드·캐릭터·준비 버튼이
        /// 모두 같은 한 번의 조회에서 나오므로 서로 어긋날 수 없다.
        /// </summary>
        private void RefreshRoom()
        {
            var myRole = _gameState.GetLocalPublicRole();
            bool myReady = _gameState.IsLocalPlayerReady();

            RefreshCharacterCards(myRole);
            RefreshAvatars(myRole);
            RefreshReadyButton(myRole, myReady);
            RefreshRoomCount();

            // 아직 아무것도 고르지 않았다면 상태줄로 안내한다. 한 번만 쓴다 —
            // 매 프레임 덮어쓰면 선택·준비 결과 메시지가 곧바로 지워진다.
            if (myRole == PlayerRole.None && !_promptedForCharacter)
            {
                _promptedForCharacter = true;
                SetStatus(ZooJackText.Get("Lobby.Status.SelectCharacter",
                    "아래에서 캐릭터를 고르세요. 고르면 WASD·방향키로 움직일 수 있습니다."));
            }
            else if (myRole != PlayerRole.None)
            {
                _promptedForCharacter = false; // 자리를 비우면 다시 안내한다
            }

            // 호스트: 3명 모두 준비 완료 시 자동 게임 시작
            if (!_startRequested
                && _runner != null && _runner.IsServer
                && _gameState.ConnectedPlayerCount == FusionGameState.MaxPlayers
                && _gameState.ReadyMask == 0b111)
            {
                _startRequested = true;
                SetStatus(ZooJackText.Get("Lobby.Status.AllReady",
                    "모든 플레이어 준비 완료! 게임 시작 중..."));
                _gameState.Rpc_RequestStartGame();
            }
        }

        /// <summary>
        /// 방에 몇 명이 들어왔는지.
        ///
        /// <b>정원까지 함께 적는다.</b> "2"만으로는 한 명을 더 기다려야 하는지 알 수 없는데,
        /// 이 게임은 셋이 다 모여야 시작하므로 그것이 방에서 가장 궁금한 값이다.
        ///
        /// 자리를 골랐는지가 아니라 <b>방에 들어왔는지</b>를 센다. 캐릭터를 아직 고르지
        /// 않은 사람도 이미 방 안에 있고, 그 사람 몫의 자리는 남에게 열려 있지 않다.
        /// </summary>
        private void RefreshRoomCount()
        {
            if (txtRoomCount == null) return;

            string text = ZooJackText.Get("Lobby.Room.Count", "{0} / {1}",
                _gameState.ConnectedPlayerCount, FusionGameState.MaxPlayers);
            if (txtRoomCount.text != text) txtRoomCount.text = text;
        }

        private void RefreshCharacterCards(PlayerRole myRole)
        {
            // 아직 아무 자리도 고르지 않았다 — 이때만 화살표와 안내 문구가 뜬다.
            bool guide = myRole == PlayerRole.None;
            if (selectPrompt != null && selectPrompt.activeSelf != guide)
                selectPrompt.SetActive(guide);

            if (characterCards == null) return;

            foreach (var card in characterCards)
            {
                if (card == null) continue;

                var role = card.Role;
                bool occupied = _gameState.PlayerIdForRole(role) != 0;
                bool ready = _gameState.IsRoleReady(role);
                bool mine = role == myRole && myRole != PlayerRole.None;

                card.Render(
                    !occupied      ? LobbyCardState.Free
                    : mine && ready ? LobbyCardState.MineReady
                    : mine          ? LobbyCardState.Mine
                    : ready         ? LobbyCardState.TakenReady
                                    : LobbyCardState.Taken,
                    guide);
            }
        }

        private void RefreshAvatars(PlayerRole myRole)
        {
            if (avatarStage == null) return;

            avatarStage.SetLocalRole(myRole);

            for (int i = 0; i < SeatRoles.Length; i++)
            {
                var role = SeatRoles[i];
                bool occupied = _gameState.PlayerIdForRole(role) != 0;

                if (occupied != _seatShown[i])
                {
                    avatarStage.SetVisible(role, occupied);
                    // 방금 앉았다면 보간 없이 자리에 세운다. 목표만 주면 앞사람이
                    // 서 있던 곳에서 걸어오는 것처럼 미끄러진다.
                    if (occupied) avatarStage.Teleport(role, _gameState.GetAvatarPosition(role));
                    _seatShown[i] = occupied;
                }

                if (!occupied) continue;

                avatarStage.SetSprite(role, AvatarSpriteFor(_gameState.CharacterForRole(role)));
                // 로비에는 단계가 없어 기다릴 사람도 없다 — 시계도 행동 문구도 붙지 않는다.
                avatarStage.SetPlate(role, PlayerNames.Label(role, RoleKo(role)), -1, null);
                avatarStage.SetLabel(role,
                    _gameState.IsRoleReady(role) ? AvatarStage.ReadyBadge : string.Empty);

                if (role != myRole)
                    avatarStage.SetRemoteTarget(role, _gameState.GetAvatarPosition(role));
            }
        }

        private void RefreshReadyButton(PlayerRole myRole, bool myReady)
        {
            bool canReady = myRole != PlayerRole.None;
            if (btnReady != null && btnReady.interactable != canReady)
                btnReady.interactable = canReady;

            // 버튼에는 동작 이름만 넣는다. "캐릭터를 고르세요" 같은 안내를 라벨에 넣으면
            // 버튼 하나만 옆으로 길어져 히트·스탠드와 같은 규격이 깨진다.
            // 안내는 아래 상태줄이 맡는다.
            if (txtReadyLabel == null) return;
            string label = myReady
                ? ZooJackText.Get("Lobby.Button.CancelReady", "준비 취소")
                : ZooJackText.Get("Lobby.Button.Ready", "준비");
            if (txtReadyLabel.text != label) txtReadyLabel.text = label;
        }

        private Sprite AvatarSpriteFor(CharacterId id) => id switch
        {
            CharacterId.Rabbit => avatarRabbit,
            CharacterId.Fox    => avatarFox,
            CharacterId.Croc   => avatarCroc,
            _                  => null
        };

        private void SetActionButtonsInteractable(bool interactable)
        {
            if (btnReady != null) btnReady.interactable = interactable;
            if (characterCards == null) return;
            foreach (var card in characterCards)
                if (card != null && card.Button != null) card.Button.interactable = interactable;
        }

        private void SetInteractable(bool interactable)
        {
            btnCreateConfirm.interactable = interactable;
            btnGoCreate.interactable      = interactable;
            btnGoBrowse.interactable      = interactable;
            foreach (var b in roomRowButtons)
                if (b != null) b.interactable = interactable;
            if (btnJoinSelected != null)
            {
                int selectedIndex = FindSelectedRoomIndex();
                btnJoinSelected.interactable = interactable
                    && selectedIndex >= 0
                    && _sessions[selectedIndex].IsOpen
                    && _sessions[selectedIndex].PlayerCount < _sessions[selectedIndex].MaxPlayers;
            }
        }

        // 방장이 나갔거나 연결이 끊겼을 때 메인 메뉴로 복귀
        private void ResetAndReturnToMain(string reason)
        {
            if (_gameLoading) return; // 게임 씬 전환 중이면 무시

            ResetLobbyState();
            ReturnToMain(reason);
        }

        private void ResetLobbyState()
        {
            UnwireAvatarStage();
            UnwireEmotionSystem();
            ResetSeatVisuals();

            _runner         = null;
            _gameState      = null;
            _startRequested = false;
            _gameLoading    = false;
            _hasPendingLocalEmotion = false;
            _nextLocalEmotionTime = 0f;
            Array.Clear(_lastEmotionSequences, 0, _lastEmotionSequences.Length);
        }

        // 세 자리를 모두 비운 모습으로 되돌린다. 캐릭터는 자리가 차는 순간 다시 나타난다.
        private void ResetSeatVisuals()
        {
            for (int i = 0; i < _seatShown.Length; i++)
            {
                _seatShown[i] = false;
                avatarStage?.SetVisible(SeatRoles[i], false);
            }

            avatarStage?.SetLocalRole(PlayerRole.None);
        }

        private void ReturnToMain(string statusMsg)
        {
            ShowOnly(panelMain);
            SetInteractable(true);
            SetStatus(statusMsg);
        }

        private void ShowOnly(GameObject panel)
        {
            GameObject[] all = { panelMain, panelCreate, panelBrowse, panelPassword, panelSettings, panelRoom };
            foreach (var p in all)
                if (p != null) p.SetActive(p == panel);
        }

        private void SetStatus(string msg)
        {
            if (txtStatus != null) txtStatus.text = msg;
        }

        private static string RoleKo(PlayerRole role) => role == PlayerRole.None
            ? ZooJackText.Get("Lobby.Role.Unselected", "미선택")
            : ZooJackText.RoleName(role);

        // ── INetworkRunnerCallbacks ──────────────────────────────────

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (runner != _browseRunner) return;

            _sessions.Clear();
            foreach (var s in sessionList)
                if (s.IsValid && s.IsVisible && _sessions.Count < MaxRoomRows)
                    _sessions.Add(s);

            RefreshRoomRows();
            SetBrowseListStatus();
        }

        // 호스트가 비밀번호를 검증한다. 클라이언트 쪽 대조만으로는 우회가 가능하므로
        // 실제 차단은 반드시 여기(서버)에서 이뤄져야 한다.
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token)
        {
            if (_hostPasswordHash == 0 || HashOfToken(token) == _hostPasswordHash)
            {
                request.Accept();
                return;
            }
            Debug.Log("[Lobby] 비밀번호 불일치 — 접속 거부");
            request.Refuse();
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[Lobby] 접속: {player.PlayerId}");
            if (!runner.IsServer) return;

            var gs = FusionGameState.LocalInstance;
            if (gs != null)
                gs.HostRegisterPlayer(player);
            else
                Debug.LogWarning($"[Lobby] OnPlayerJoined: FusionGameState 아직 없음 — player {player.PlayerId} 등록 지연");
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            Debug.Log($"[Lobby] 퇴장: {player.PlayerId}");
            if (runner.IsServer && _gameState != null)
                _gameState.HostUnregisterPlayer(player);
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (runner != _runner) return; // 목록 조회용 러너의 종료는 무시

            Debug.Log($"[Lobby] Shutdown: {shutdownReason}");
            string msg = shutdownReason switch
            {
                ShutdownReason.Ok => ZooJackText.Get("Lobby.Status.LeftRoom", "방에서 나왔습니다."),
                ShutdownReason.GameNotFound => ZooJackText.Get(
                    "Lobby.Status.RoomNotFound", "방을 찾을 수 없습니다."),
                ShutdownReason.GameIsFull => ZooJackText.Get("Lobby.Status.RoomFull", "방이 꽉 찼습니다."),
                _ => ZooJackText.Get("Lobby.Status.Disconnected", "연결 종료: {0}", shutdownReason)
            };
            ResetAndReturnToMain(msg);
        }

        void INetworkRunnerCallbacks.OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (runner != _runner) return;

            Debug.Log($"[Lobby] 서버 연결 끊김: {reason}");
            ResetAndReturnToMain(ZooJackText.Get("Lobby.Status.HostLeft", "방장이 나갔습니다."));
        }

        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner != _runner) return;

            // Spawned()가 OnSceneLoadDone 이후에 호출될 수 있으므로 Update()에서 LocalInstance를 폴링
            Debug.Log($"[Lobby] OnSceneLoadDone — FusionGameState.LocalInstance: {(FusionGameState.LocalInstance != null ? "있음" : "없음")}");

            // 호스트는 씬 로드가 완전히 끝난 이 시점에 FusionGameState를 스폰한다.
            // (StartGame 직후 스폰하면 씬 동기화 완료 처리에 의해 디스폰되므로 여기서 한다.)
            // 씬오브젝트가 아닌 런너 스폰 객체이므로 Lobby→Game 전환에도 유지된다.
            // LocalInstance가 이미 있으면(= GameScene 로드 등) 중복 스폰하지 않는다.
            if (runner.IsServer && FusionGameState.LocalInstance == null)
            {
                if (gameStatePrefab != null)
                {
                    // DontDestroyOnLoad 플래그로 스폰하면 모든 피어에서 씬 전환(Single 로드)에도
                    // 디스폰되지 않고 유지된다 → Lobby→Game 전환 시 게임 상태가 보존된다.
                    runner.Spawn(gameStatePrefab, flags: NetworkSpawnFlags.DontDestroyOnLoad);
                    Debug.Log("[Lobby] FusionGameState 스폰 요청 (호스트, DontDestroyOnLoad).");
                }
                else
                {
                    Debug.LogError("[Lobby] gameStatePrefab이 비어 있음 — FusionGameState를 스폰할 수 없습니다.");
                }
            }
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            if (runner != _runner) return;

            ResetAndReturnToMain(ZooJackText.Get("Lobby.Status.JoinFailed",
                "참가 실패 ({0}). 비밀번호가 틀렸거나 방이 닫혔습니다.", reason));
        }

        void INetworkRunnerCallbacks.OnConnectedToServer(NetworkRunner runner) { }
        public void OnInput(NetworkRunner runner, NetworkInput input) { }
        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    }
}
#else
using UnityEngine;

namespace ZooJack
{
    public class NetworkLobbyManager : MonoBehaviour
    {
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10f, 10f, 480f, 50f), GUI.skin.box);
            GUILayout.Label("NetworkLobbyManager: Photon Fusion 2 미활성. FUSION2 심볼을 추가하세요.");
            GUILayout.EndArea();
        }
    }
}
#endif
