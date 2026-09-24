#if FUSION2 || ZOOJACK_PHOTON_FUSION
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ZooJack
{
    /// <summary>
    /// 멀티플레이(Fusion) 게임 씬 컨트롤러.
    /// FusionGameState의 Networked 상태를 읽어 GameDirector의 UI를 구동하고,
    /// 버튼 입력을 RPC로 호스트에 전송한다.
    /// 네트워크 세션이 없으면 스스로 비활성화되어 GameDirector(핫시트)가 동작한다.
    /// </summary>
    [RequireComponent(typeof(GameDirector))]
    public class NetworkGameDirector : MonoBehaviour, MatchSurrender.IHost
    {
        private GameDirector ui;
        private FusionGameState gs;
        private NetworkRunner sessionRunner;
        private DealerCardArranger arranger;
        private JudgmentSuspense suspense;
        private bool networkUiInitialized;
        private bool avatarStageWired;
        private bool emotionNetworkWired;
        private bool returningToRoomLobby;
        private bool returningToLobby;
        private BribeSelector bribeSelector; // 뇌물 프리셋 칩 + 스테퍼 UI 로직
        /// <summary>핫시트 디렉터와 똑같이 하는 화면 조작. 두 모드가 이 한 벌을 공유한다.</summary>
        private GamePanelPresenter panels;
        private BetPresetRow betPresets;     // 판돈 프리셋 칩 UI 로직
        private int selectedBet = RoundSettlement.DefaultBet;
        private int bribePanelBalance = RoundSettlement.SeedMoney; // 뇌물 + 판돈의 예산
        private bool bribeSubmittedLocal; // RPC 왕복 전 즉시 UI 반영용
        private bool betSubmittedLocal;
        /// <summary>
        /// 이번 라운드에 내가 낸 뇌물. 더블다운 버튼의 잔액 미리보기에 쓴다.
        /// 뇌물은 비공개라 <see cref="FusionGameState"/>의 secretBribes는 호스트에만 있고,
        /// 클라이언트가 자기 뇌물을 아는 경로는 자기가 보낸 값을 기억하는 것뿐이다.
        /// </summary>
        private int localSubmittedBribe;
        private long lastSignature = long.MinValue;
        private int lastSeenRound = -1;
        /// <summary>
        /// 직전에 본 더블다운 상태. 자리마다 한 번씩만 알림을 띄우려고 기억해 둔다 —
        /// 둘이 각자 걸 수 있으므로 하나로는 두 번째 선언을 놓친다.
        /// </summary>
        private int lastActionAnnouncementVersion;
        private int lastRoleAssignmentVersion;

        /// <summary>
        /// 항복 알림을 읽는 사이가 끝나는 시각(실시간). -1이면 아직 아무도 접지 않았다.
        /// </summary>
        private float surrenderRevealAt = -1f;
        private bool roleRevealBlocking;
        private readonly int[] lastEmotionSequences = new int[FusionGameState.MaxPlayers + 1];
        private bool hasPendingLocalEmotion;
        private EmotionId pendingLocalEmotion;
        private PlayerRole pendingLocalEmotionRole;
        private float pendingLocalEmotionExpiry;
        private float nextLocalEmotionTime;
        private bool gameChatGuideShown;

        private void Awake()
        {
            ui = GetComponent<GameDirector>();
            gs = FusionGameState.LocalInstance;
            sessionRunner = gs != null ? gs.Runner : FindFirstObjectByType<NetworkRunner>();

            if (gs == null && (sessionRunner == null || !sessionRunner.IsRunning))
            {
                // 오프라인/디버그 실행 → 핫시트 모드 유지
                enabled = false;
                return;
            }

            // 네트워크 씬에서는 FusionGameState.Spawned()가 이 Awake보다 늦을 수 있다.
            // 러너가 살아 있다면 핫시트 Start()를 먼저 차단하고 LocalInstance를 기다린다.
            ui.enabled = false;
            if (ui.emotionWheel == null)
                ui.emotionWheel = EmotionWheelController.FindInScene();
            ui.emotionWheel?.SetOpenGuard(CanOpenNetworkEmotionWheel);
            if (gs != null)
                InitializeNetworkUi();
        }

        private void InitializeNetworkUi()
        {
            if (networkUiInitialized || gs == null) return;

            sessionRunner = gs.Runner;
            panels = new GamePanelPresenter(ui);
            arranger = panels.BuildArranger();
            suspense = new JudgmentSuspense(
                ui.txtFinalWinner, ui.txtFinalReason, ui.vignette,
                ui.finalCube, ui.finalRoundDetails, ui.finalOutcomeGroup);
            suspense.OnCandidateSettled = ui.Markers.RefreshFinal;
            ui.Markers.RefreshFinal(FinalWinner.None);
            WireButtons();
            WireAvatarStage();
            WireEmotionNetwork();

            // 항복은 환경설정 화면 안에 있고 그 화면은 이쪽을 모른다. 여기서 손을 든다.
            // 핫시트 디렉터는 Start가 막혀 있어 등록하지 않으므로 자리를 다투지 않는다.
            MatchSurrender.SetHost(this);

            // 게임 씬이 열렸다는 것이 곧 새 매치다 — 다시 플레이는 방 로비를 거쳐 이 씬을
            // 다시 연다. 지난 매치의 라운드 기록은 여기서 비운다. 첫 라운드가 끝날 때까지
            // 기다리면 그동안 기록 화면이 지난 판을 보여 준다.
            MatchHistory.BeginMatch();

            networkUiInitialized = true;
            lastSignature = long.MinValue;
        }

        // ── 캐릭터 이동 ───────────────────────────────────────────────

        private void WireAvatarStage()
        {
            if (ui.avatarStage == null || avatarStageWired) return;
            ui.avatarStage.LocalPositionChanged += OnLocalAvatarMoved;
            avatarStageWired = true;
        }

        // AvatarStage는 네트워크 수명주기와 무관하게 매 프레임 돈다. 세션이 끊기거나
        // 로비로 돌아가는 중에 despawn된 오브젝트로 RPC를 쏘면 Fusion이 예외를 던지므로,
        // 보내기 직전에 오브젝트가 아직 살아 있는지 확인한다.
        private void OnLocalAvatarMoved(Vector2 position)
        {
            if (returningToLobby || gs == null) return;
            if (gs.Object == null || !gs.Object.IsValid) return;
            if (gs.Runner == null || !gs.Runner.IsRunning) return;
            gs.Rpc_ReportAvatarPosition(position);
        }

        private void OnDestroy()
        {
            MatchSurrender.ClearHost(this);

            if (ui != null && ui.avatarStage != null && avatarStageWired)
                ui.avatarStage.LocalPositionChanged -= OnLocalAvatarMoved;

            if (ui != null && ui.emotionWheel != null)
            {
                if (emotionNetworkWired)
                    ui.emotionWheel.EmotionSelected -= OnLocalEmotionSelected;
                ui.emotionWheel.SetOpenGuard(null);
            }
            if (gs != null && emotionNetworkWired)
                gs.EmotionBroadcastReceived -= OnEmotionBroadcastReceived;
        }

        private void WireEmotionNetwork()
        {
            if (emotionNetworkWired || gs == null || ui == null) return;
            if (ui.emotionWheel == null)
                ui.emotionWheel = EmotionWheelController.FindInScene();
            if (ui.emotionWheel == null || ui.avatarStage == null) return;

            ui.emotionWheel.EmotionSelected += OnLocalEmotionSelected;
            ui.emotionWheel.SetOpenGuard(CanOpenNetworkEmotionWheel);
            gs.EmotionBroadcastReceived += OnEmotionBroadcastReceived;
            emotionNetworkWired = true;
        }

        private bool CanOpenNetworkEmotionWheel()
        {
            if (!isActiveAndEnabled || !networkUiInitialized || returningToLobby || returningToRoomLobby)
                return false;
            if (gs == null || gs.Object == null || !gs.Object.IsValid ||
                gs.Runner == null || !gs.Runner.IsRunning ||
                gs.CurrentPhase == GamePhase.WaitingForPlayers)
                return false;
            if (Time.unscaledTime < nextLocalEmotionTime) return false;

            PlayerRole role = gs.GetLocalPublicRole();
            if (role == PlayerRole.None || !ui.avatarStage.IsVisible(role)) return false;
            return !GameDirector.IsTextInputFocused();
        }

        private void OnLocalEmotionSelected(EmotionId emotionId)
        {
            if (!CanOpenNetworkEmotionWheel()) return;

            PlayerRole role = gs.GetLocalPublicRole();
            ui.avatarStage.ShowEmotion(role, emotionId); // 네트워크 왕복 전에 즉시 표시

            hasPendingLocalEmotion = true;
            pendingLocalEmotion = emotionId;
            pendingLocalEmotionRole = role;
            pendingLocalEmotionExpiry = Time.unscaledTime + 2f;
            nextLocalEmotionTime = Time.unscaledTime + FusionGameState.EmotionCooldownSeconds;
            gs.Rpc_RequestEmotion((int)emotionId);
        }

        private void OnEmotionBroadcastReceived(int playerSlot, EmotionId emotionId, int sequence)
        {
            if (playerSlot < 1 || playerSlot >= lastEmotionSequences.Length) return;
            if (sequence <= lastEmotionSequences[playerSlot]) return;
            lastEmotionSequences[playerSlot] = sequence;

            PlayerRole currentRole = gs != null ? gs.RoleForSlot(playerSlot) : PlayerRole.None;
            if (currentRole == PlayerRole.None || ui == null || ui.avatarStage == null) return;

            bool isPredictedLocal = gs != null && playerSlot == gs.LocalSlot &&
                                    hasPendingLocalEmotion && pendingLocalEmotion == emotionId &&
                                    Time.unscaledTime <= pendingLocalEmotionExpiry;
            if (isPredictedLocal)
            {
                hasPendingLocalEmotion = false;
                if (currentRole == pendingLocalEmotionRole) return;

                // 왕복 중 역할이 교대됐다면 예측 표시를 내리고 새 역할 위치로 옮긴다.
                ui.avatarStage.HideEmotion(pendingLocalEmotionRole);
            }

            if (ui.avatarStage.IsVisible(currentRole))
                ui.avatarStage.ShowEmotion(currentRole, emotionId);
        }

        /// <summary>
        /// 캐릭터 위치를 매 프레임 갱신한다. 화면 갱신 시그니처에는 넣지 않는다 —
        /// 초당 20회 바뀌는 값이라 시그니처에 섞으면 UI 전체가 그만큼 다시 그려진다.
        /// </summary>
        private void UpdateAvatars()
        {
            var stage = ui.avatarStage;
            if (stage == null) return;

            stage.SetLocalRole(gs.GetLocalPublicRole());
            stage.SetRemoteTarget(PlayerRole.PlayerA, gs.GetAvatarPosition(PlayerRole.PlayerA));
            stage.SetRemoteTarget(PlayerRole.PlayerB, gs.GetAvatarPosition(PlayerRole.PlayerB));
            stage.SetRemoteTarget(PlayerRole.Dealer,  gs.GetAvatarPosition(PlayerRole.Dealer));
        }


        private void Update()
        {
            if (returningToLobby) return;

            if (!networkUiInitialized)
            {
                gs = FusionGameState.LocalInstance;
                if (gs == null)
                {
                    if (sessionRunner == null || !sessionRunner.IsRunning)
                        ReturnToLobby("네트워크 세션을 초기화하지 못했습니다.");
                    return;
                }

                InitializeNetworkUi();
            }

            // Awake에서 네트워크 UI가 먼저 초기화될 수 있으므로 첫 Update까지 기다린다.
            // 이때는 ChatPanel.OnEnable이 이미 구독을 마쳐 안내가 즉시 또렷하게 나타난다.
            if (!gameChatGuideShown)
            {
                gameChatGuideShown = true;
                ChatLog.AddSystem(ZooJackText.Get(
                    "Chat.Guide.GameStarted",
                    "게임이 시작되었습니다."));
            }

            gs = FusionGameState.LocalInstance;
            if (gs == null)
            {
                // 세션 종료 (호스트 퇴장, 연결 끊김 등)
                ReturnToLobby("세션이 종료되었습니다.");
                return;
            }

            // 호스트: 누가 나가면 그 자리만 비운다. 방은 접지 않는다 —
            // 나간 사람만 나가고 남은 사람은 아래에서 같은 방의 로비로 간다.
            var runner = gs.Runner;
            if (runner != null && runner.IsServer
                && CountActivePlayers(runner) < FusionGameState.MaxPlayers)
                gs.HostReleaseMissingSeats();

            // 단계가 대기로 돌아왔다 — 전원이 '다시 플레이'를 눌렀거나, 방금 자리가 비었다.
            // 로비가 게임 씬을 열 때와 같은 방식으로, 단계를 보고 방 로비를 연다.
            if (gs.CurrentPhase == GamePhase.WaitingForPlayers)
            {
                ReturnToRoomLobby();
                return;
            }

            // 새 라운드 → 로컬 뇌물 상태 초기화
            if (gs.RoundIndex != lastSeenRound)
            {
                lastSeenRound = gs.RoundIndex;
                bribeSubmittedLocal = false;
                betSubmittedLocal = false;
                selectedBet = RoundSettlement.DefaultBet;
                localSubmittedBribe = 0;
            }

            RaiseApprovedActionNoticeIfNew();

            // 항복 뒤의 사이는 시계로 흐른다. 화면 갱신은 상태가 바뀔 때만 도는데
            // 여기서 바뀌는 것은 시간뿐이라, 때가 되면 한 번 깨워 준다.
            if (surrenderRevealAt >= 0f && !ui.IsMatchOverScreenUp
                && Time.unscaledTime >= surrenderRevealAt)
                ForceRender();

            long sig = ComputeSignature();
            if (sig != lastSignature)
            {
                lastSignature = sig;
                Render();
            }

            RefreshNetworkBribeStatusCard();
            UpdateAvatars();
            UpdateTurnTimer();
            UpdateMatchOverCountdown();
        }

        private void RefreshNetworkBribeStatusCard()
        {
            if (gs == null || ui == null) return;

            PlayerRole viewer = gs.GetLocalPublicRole();
            int bribeA = viewer == PlayerRole.Dealer
                ? (gs.BribeSubmittedA ? gs.LocalDealerBribeA : 0)
                : viewer == PlayerRole.PlayerA ? localSubmittedBribe : 0;
            int bribeB = viewer == PlayerRole.Dealer
                ? (gs.BribeSubmittedB ? gs.LocalDealerBribeB : 0)
                : viewer == PlayerRole.PlayerB ? localSubmittedBribe : 0;
            ui.SetBribeStatusCard(viewer, bribeA, bribeB);
        }

        // ── 버튼 연결 ─────────────────────────────────────────────────

        private void WireButtons()
        {
            if (ui.sliderBet != null)
            {
                GameDirector.DisableDirectionalNavigation(ui.sliderBet);
                // 네트워크 진입 시에도 씬의 직렬화 값보다 공용 기본 판돈(50)을 우선한다.
                ui.sliderBet.wholeNumbers = true;
                ui.sliderBet.minValue = RoundSettlement.BetToSteps(RoundSettlement.MinBet);
                ui.sliderBet.maxValue = RoundSettlement.BetToSteps(RoundSettlement.SeedMoney);
                ui.sliderBet.SetValueWithoutNotify(
                    RoundSettlement.BetToSteps(RoundSettlement.DefaultBet));
                ui.sliderBet.onValueChanged.AddListener(OnBetSliderChanged);
            }
            betPresets = new BetPresetRow(ui.btnBetPresets, ui.sliderBet);
            bribeSelector = new BribeSelector(
                ui.btnBribePresets, ui.btnBribeMinus, ui.btnBribePlus,
                ui.txtBribeAmount, RefreshBribeSummary);
            if (ui.btnBribeConfirm != null) ui.btnBribeConfirm.onClick.AddListener(ConfirmBribe);
            if (ui.btnBetConfirm != null) ui.btnBetConfirm.onClick.AddListener(ConfirmBet);

            for (int i = 0; i < ui.btnCandidates.Length; i++)
            {
                int index = i; // 클로저가 루프 변수를 붙잡지 않도록 자리마다 복사한다
                if (ui.btnCandidates[i] != null)
                    ui.btnCandidates[i].onClick.AddListener(() => OnDealerCardClicked(index));
            }
            if (ui.btnDealerConfirm != null)
                ui.btnDealerConfirm.onClick.AddListener(ConfirmDealerSelection);

            if (ui.btnHit   != null) ui.btnHit.onClick.AddListener(() => gs?.Rpc_RequestHit());
            if (ui.btnStand != null) ui.btnStand.onClick.AddListener(() => gs?.Rpc_RequestStand());
            if (ui.btnDie   != null) ui.btnDie.onClick.AddListener(() => gs?.Rpc_RequestDie());
            if (ui.btnDoubleDown != null)
                ui.btnDoubleDown.onClick.AddListener(() => gs?.Rpc_RequestDoubleDown());

            if (ui.btnAccuse != null) ui.btnAccuse.onClick.AddListener(() => gs?.Rpc_SubmitAccusation(AccusationChoice.Accuse));
            if (ui.btnAccept != null) ui.btnAccept.onClick.AddListener(() => gs?.Rpc_SubmitAccusation(AccusationChoice.AcceptResult));

            if (ui.btnNextRound != null) ui.btnNextRound.onClick.AddListener(() => gs?.Rpc_RequestNextRound());

            // 결과 공개의 '계속'은 네트워크에서 준비 버튼이 된다. 앉아 있는 모두가 눌러야
            // 넘어가고, 어디로 넘어갈지는 단계가 정한다(OnResultContinue).
            if (ui.btnContinue != null) ui.btnContinue.onClick.AddListener(OnResultContinue);

            // 매치 종료 화면의 두 출구.
            //   다시 플레이 — 전원이 눌러야 방 로비로 돌아간다(캐릭터부터 다시 고른다).
            //   나가기     — 혼자서도 즉시 나간다. 한 명이 자리를 비워 재대결이 성립하지
            //                않을 때 남은 사람이 갇히지 않도록, 이쪽에는 합의를 두지 않는다.
            if (ui.btnRestartMatch != null)
                ui.btnRestartMatch.onClick.AddListener(() => gs?.Rpc_RequestRematch());
            if (ui.btnLeaveMatch != null)
                ui.btnLeaveMatch.onClick.AddListener(() => ReturnToLobby("매치에서 나갔습니다."));

            // 네트워크 모드에서 쓰지 않는 핫시트 전용 버튼
            if (ui.btnStart  != null) ui.btnStart.gameObject.SetActive(false);
            if (ui.btnReveal != null) ui.btnReveal.gameObject.SetActive(false);

            // 클릭음과 슬라이더 소리. 어디에 붙일지는 위젯 필드를 가진 GameDirector가 정한다 —
            // 이쪽에서 목록을 다시 적으면 핫시트와 빌드가 소리부터 갈라진다.
            ui.WireUiSounds();
        }

        /// <summary>
        /// 결과 카드의 '계속'. 같은 버튼이 두 곳으로 간다 — 승패가 갈린 판에서는 고발
        /// 단계로, 비긴 판에서는 카드 재배분으로. 버튼을 둘로 나누지 않은 이유는 자리가
        /// 하나뿐이기도 하고, 사람에게는 둘 다 "다 봤다"는 같은 뜻이기 때문이다.
        /// </summary>
        private void OnResultContinue()
        {
            if (gs == null) return;

            if (gs.CurrentPhase == GamePhase.TieRedeal) gs.Rpc_RequestTieRedeal();
            else gs.Rpc_RequestAccusation();
        }

        private void OnBetSliderChanged(float raw)
        {
            selectedBet = RoundSettlement.StepsToBet(Mathf.RoundToInt(raw));
            RefreshBetSummary();
        }

        private void ConfirmBribe()
        {
            if (gs == null || bribeSubmittedLocal) return;
            bribeSubmittedLocal = true;
            int bribe = bribeSelector != null ? bribeSelector.Value : 0;
            localSubmittedBribe = bribe;
            gs.Rpc_SubmitBribe(bribe);
            ForceRender();
        }

        private void ConfirmBet()
        {
            if (gs == null || betSubmittedLocal) return;
            betSubmittedLocal = true;
            int bet = Mathf.Min(selectedBet,
                RoundSettlement.MaxBetFor(bribePanelBalance, localSubmittedBribe));
            gs.Rpc_SubmitBet(bet);
            ForceRender();
        }

        private void OnDealerCardClicked(int index)
        {
            arranger?.HandleClick(index, RefreshDealerSelection);
        }

        private void ConfirmDealerSelection()
        {
            panels.LockDealerConfirmButton();
            arranger?.ConfirmCenter(SubmitDealerChoice);
        }

        private void SubmitDealerChoice(int index)
        {
            gs?.Rpc_SubmitDealerCardChoice(index);
        }

        private void ForceRender() => lastSignature = long.MinValue;

        // ── 렌더링 ────────────────────────────────────────────────────

        private void Render()
        {
            var phase = gs.CurrentPhase;
            var localRole = gs.GetLocalPublicRole();

            if (gs.RoleAssignmentVersion > 0 &&
                gs.RoleAssignmentVersion != lastRoleAssignmentVersion)
            {
                lastRoleAssignmentVersion = gs.RoleAssignmentVersion;
                roleRevealBlocking = true;
                ShowTableOnly();
                ui.ShowRoleReveal(localRole, gs.CharacterForRole(localRole), () =>
                {
                    roleRevealBlocking = false;
                    ForceRender();
                });
                return;
            }

            // 씬 로딩 중 서버의 RoleAssignment가 끝났더라도 로컬 공개 연출은 끝까지 보장한다.
            if (roleRevealBlocking)
            {
                ShowTableOnly();
                return;
            }

            // 항복은 단계를 가리지 않고 판을 접는다. 단계별 화면보다 먼저 가로챈다 —
            // 라운드 종료로 흘려보내면 있지도 않은 최종 판정의 두구두구가 한 번 더 돈다.
            //
            // 다만 곧바로 순위표를 올리지는 않는다. "누가 항복했다"는 알림을 읽을 사이를
            // 두어야 하고(GameDirector.SurrenderHoldSeconds), 그동안 화면은 방금까지
            // 굴러가던 판 그대로 둔다.
            if (gs.SurrenderedRole != PlayerRole.None)
            {
                if (surrenderRevealAt < 0f)
                    surrenderRevealAt = Time.unscaledTime + GameDirector.SurrenderHoldSeconds;
                if (Time.unscaledTime >= surrenderRevealAt) ShowMatchOver();
                return;
            }

            RefreshStatusBar(phase);
            RefreshCardSlots();
            // 좌석별 베팅은 두 사람이 모두 낸 뒤에야 0이 아니게 되므로, 그때부터 칩이 놓인다.
            // 더블다운을 선언한 쪽만 자기 액수의 2배가 테이블에 올라간다.
            // 더블다운 여부는 서명에 들어 있어 성립하는 순간 이 경로가 다시 돈다.
            ui.BetChips.Show(
                RoundSettlement.TableBet(gs.BetA, gs.HasDoubled(PlayerRole.PlayerA)),
                RoundSettlement.TableBet(gs.BetB, gs.HasDoubled(PlayerRole.PlayerB)),
                gs.RoundIndex, phase);
            // 무승부도 공개 구간이다. 같은 점수라는 것이 점수 카드에 보여야 "왜 비겼지"가
            // 되지 않는다.
            if (phase == GamePhase.ResultReveal || phase == GamePhase.Accusation ||
                phase == GamePhase.FinalJudgment || phase == GamePhase.RoundEnd ||
                phase == GamePhase.TieRedeal)
                RefreshRevealedScoreCards();

            switch (phase)
            {
                case GamePhase.WaitingForPlayers:
                    ShowTableOnly();
                    break;

                case GamePhase.RoleAssignment:
                    ShowTableOnly();
                    break;

                case GamePhase.BribeSelection:
                    bool needsBribe =
                        (localRole == PlayerRole.PlayerA && !gs.BribeSubmittedA) ||
                        (localRole == PlayerRole.PlayerB && !gs.BribeSubmittedB);
                    if (needsBribe && !bribeSubmittedLocal)
                        ShowBribePanel(localRole);
                    else
                        ShowTableOnly();
                    break;

                case GamePhase.BetSelection:
                    bool needsBet =
                        (localRole == PlayerRole.PlayerA && !gs.BetSubmittedA) ||
                        (localRole == PlayerRole.PlayerB && !gs.BetSubmittedB);
                    if (needsBet && !betSubmittedLocal)
                        ShowBetPanel(localRole);
                    else
                    {
                        ShowTableOnly();
                        ShowScoreCardsAfterInitialDeal();
                    }
                    break;

                case GamePhase.CardCandidateGeneration:
                case GamePhase.SystemResultGeneration:
                case GamePhase.BlackjackResultCalculation:
                    ShowTableOnly();
                    break;

                case GamePhase.TieRedeal:
                    ShowTiePanel();
                    break;

                case GamePhase.DealerCardDistribution:
                    if (localRole == PlayerRole.Dealer && gs.LocalDealerCandidates.Count > 0)
                        ShowDealerPanel();
                    else
                    {
                        ShowTableOnly();
                        ShowScoreCardsAfterInitialDeal();
                    }
                    break;

                case GamePhase.PlayerDecision:
                    if (localRole == gs.DecisionTurn)
                        ShowDecisionPanel(localRole);
                    else
                    {
                        ShowTableOnly();
                        ShowScoreCardsAfterInitialDeal();
                    }
                    break;

                case GamePhase.ResultReveal:
                    ShowResultPanel();
                    break;

                case GamePhase.Accusation:
                    if (gs.IsLocalPlayerAllowedToAccuse())
                        ShowAccusationPanel();
                    else
                        ShowTableOnly();
                    break;

                case GamePhase.FinalJudgment:
                    ShowFinalPanel(showNextButton: false);
                    break;

                case GamePhase.RoundEnd:
                    ShowFinalPanel(showNextButton: true);
                    break;
            }
        }

        /// <summary>
        /// 남을 기다리는 동안의 화면. <b>아무 패널도 띄우지 않는다</b> — 답할 것이 없는
        /// 사람에게 판을 가리고 설 상자가 필요 없기 때문이다.
        ///
        /// 예전에는 우상단에 작은 대기 상자를 띄웠는데, 그 상자를 없애자
        /// <c>ShowOnly(panelCover)</c>가 남아 <b>투명한 전체 화면 패널</b>만 켜졌다.
        /// 보이지는 않으면서 클릭은 전부 삼켜서, 기다리는 동안 채팅·설정·기록·설명서가
        /// 하나도 눌리지 않았다. 그래서 켤 패널을 아예 없앤다.
        ///
        /// 무슨 단계인지는 좌상단 표시줄이, 누구를 기다리는지는 그 사람 머리 위 명패가
        /// 말한다(<see cref="GameDirector.RefreshAvatarPlates(GamePhase, PlayerRole, PlayerRole, float)"/>).
        /// </summary>
        private void ShowTableOnly() => ShowOnly(null);

        /// <summary>
        /// 최초 두 장씩의 카드 배분이 끝난 뒤 대기 화면에 양쪽 점수 합계를 띄운다.
        /// 최초 배분 중에는 점수를 숨기고, 이후 히트/더블다운 배분 중에는 계속 표시한다.
        /// </summary>
        private void ShowScoreCardsAfterInitialDeal()
        {
            var handA = gs.GetHandCards(PlayerRole.PlayerA);
            var handB = gs.GetHandCards(PlayerRole.PlayerB);
            if (handA.Count < 2 || handB.Count < 2) return;

            ui.ShowScoreCardsWhileWaiting(
                gs.GetLocalPublicRole(),
                handA, ScoreOf(PlayerRole.PlayerA),
                handB, ScoreOf(PlayerRole.PlayerB));
        }

        /// <summary>
        /// 내 뇌물 판. <b>처음 여는 것과 이미 떠 있는 판을 다시 그리는 것은 다르다.</b>
        ///
        /// 이 경로는 화면 서명이 바뀔 때마다 돈다. 그런데 서명에는 <b>상대가 뇌물을 냈는가</b>가
        /// 들어 있어서(명패의 준비 표시가 그때 바뀌어야 하므로), 내가 아직 고르는 중이어도
        /// 상대가 먼저 내면 이 함수가 한 번 더 불린다. 그때마다 처음처럼 초기화하면
        /// 내가 올려 둔 금액이 0으로 떨어진다 — 남이 낸 것이 내 손을 건드리는 셈이다.
        ///
        /// 그래서 판이 이미 떠 있으면 금액은 두고 상한만 다시 잡는다. 판이 내려갔다가
        /// 다시 서는 것은 새 라운드뿐이므로, 라운드가 바뀌면 예전대로 0에서 시작한다.
        /// </summary>
        private void ShowBribePanel(PlayerRole role)
        {
            bool alreadyOpen = GameDirector.IsShowing(ui.panelBribe);

            if (ui.txtBribeTitle != null)
                ui.txtBribeTitle.text = ZooJackText.Get(
                    "Game.Bribe.LocalTitle", "{0} (나) · 뇌물 결정", RoleKo(role));

            bribePanelBalance = gs != null ? gs.LocalBalance : RoundSettlement.SeedMoney;

            // 뇌물은 카드 배분 전에 단독으로 확정한다.
            bribeSelector?.Begin(RoundSettlement.MaxBribeFor(bribePanelBalance),
                keepValue: alreadyOpen);
            RefreshBribeSummary();

            ShowOnly(ui.panelBribe);
        }

        private void RefreshBribeSummary() =>
            panels.RefreshBribeSummary(bribeSelector != null ? bribeSelector.Value : 0, bribePanelBalance);

        /// <summary>
        /// 내 판돈 판. 뇌물 판과 같은 이유로 <see cref="ShowBribePanel"/>처럼
        /// 이미 떠 있으면 고르던 금액을 지키고 상한만 다시 잡는다.
        /// </summary>
        private void ShowBetPanel(PlayerRole role)
        {
            bool alreadyOpen = GameDirector.IsShowing(ui.panelBet);

            bribePanelBalance = gs != null ? gs.LocalBalance : RoundSettlement.SeedMoney;
            if (!alreadyOpen) selectedBet = RoundSettlement.DefaultBet;
            RefreshBetLimit();
            if (ui.txtBetTitle != null)
                ui.txtBetTitle.text = ZooJackText.Get(
                    "Game.Bet.LocalTitle", "{0} (나) · 판돈 결정", RoleKo(role));
            RefreshCardSlots();
            ui.RefreshDecisionScoreCards(
                role,
                gs.GetHandCards(PlayerRole.PlayerA), ScoreOf(PlayerRole.PlayerA),
                gs.GetHandCards(PlayerRole.PlayerB), ScoreOf(PlayerRole.PlayerB));
            ShowOnly(ui.panelBet);
        }

        private void RefreshBetLimit() =>
            selectedBet = panels.ApplyBetLimit(
                selectedBet, localSubmittedBribe, bribePanelBalance, betPresets);

        private void RefreshBetSummary() =>
            panels.RefreshBetSummary(selectedBet, localSubmittedBribe, bribePanelBalance);

        private void ShowDealerPanel()
        {
            var cands = gs.LocalDealerCandidates;
            string target = ZooJackText.RoleName(gs.LocalDealerTargetRole);
            if (ui.txtDealerSelection != null)
                ui.txtDealerSelection.text = ZooJackText.Get(
                    "Game.Dealer.Selection", "{0}에게 줄 카드", target);

            // 딜러 화면에는 현재 양쪽 점수를 PanelDealer의 반투명 배경 위에 함께 표시한다.
            ui.RefreshRevealedScoreCards(
                gs.GetHandCards(PlayerRole.PlayerA), ScoreOf(PlayerRole.PlayerA),
                gs.GetHandCards(PlayerRole.PlayerB), ScoreOf(PlayerRole.PlayerB));

            ShowOnly(ui.panelDealer);
            arranger.Setup(cands, gs.LocalDealerCandidateVersion,
                gs.LocalDealerTargetRole == PlayerRole.PlayerA);
            if (ui.btnDealerConfirm != null) ui.btnDealerConfirm.interactable = true;
            RefreshDealerSelection(arranger.CurrentCenter);
        }

        private void RefreshDealerSelection(int index) =>
            panels.ShowDealerCandidateScore(
                gs.LocalDealerCandidates, index, gs.LocalDealerTargetRole,
                gs.GetHandCards(gs.LocalDealerTargetRole));

        private void ShowDecisionPanel(PlayerRole role)
        {
            var score = ScoreOf(role);
            var handA = gs.GetHandCards(PlayerRole.PlayerA);
            var handB = gs.GetHandCards(PlayerRole.PlayerB);
            var scoreA = ScoreOf(PlayerRole.PlayerA);
            var scoreB = ScoreOf(PlayerRole.PlayerB);

            if (ui.txtDecisionHand != null)
                ui.txtDecisionHand.text = ZooJackText.Get(
                    "Game.Decision.Turn",
                    "<b>{0}의 차례</b>\n<size=17><color={1}>행동을 선택하세요.</color></size>",
                    RoleKo(role), ZooJackPalette.TaupeTag);
            if (ui.txtDecisionScore != null)
                ui.txtDecisionScore.gameObject.SetActive(false);

            ui.RefreshDecisionScoreCards(role, handA, scoreA, handB, scoreB);

            int handCount = role == PlayerRole.PlayerA ? handA.Count : handB.Count;
            // 더블다운한 손은 한 장만 더 받으므로 상한이 3장으로 줄어든다.
            int maxCards = gs.HasDoubled(role)
                ? RoundEngine.DoubledMaxCardsPerHand
                : RoundEngine.MaxCardsPerHand;
            if (ui.btnHit != null)
                ui.btnHit.interactable = !score.IsBust && handCount < maxCards;
            if (ui.btnStand != null) ui.btnStand.interactable = true;
            // 다이는 자기 차례라면 언제든. 단 버스트한 뒤에는 막는다 —
            // 진 판을 절반 값에 사는 도피구가 된다. 최종 검증은 호스트의 RoundEngine.CanDie다.
            if (ui.btnDie != null)
                ui.btnDie.interactable = gs.FoldedRole == PlayerRole.None && !score.IsBust;
            // 더블다운도 초기 2장에서만, 라운드당 1회.
            // 잔액 검사는 '내 좌석'만 본다 — 호스트도 같은 검사를 하므로(HostCanAffordDoubleDown)
            // 버튼이 켜져 있으면 반드시 통과한다. 상대의 감당 여부는 보지 않는다:
            // 상대 뇌물이 비공개라 계산할 수도 없고, 못 내면 파산하는 것이 규칙이다.
            if (ui.btnDoubleDown != null)
                ui.btnDoubleDown.interactable =
                    gs.FoldedRole == PlayerRole.None
                    && !gs.HasDoubled(role)
                    && handCount == RoundEngine.DoubleDownMaxCards
                    && RoundSettlement.CanAffordStake(
                        gs.LocalBalance, localSubmittedBribe, gs.StakeAfterDoubleDown);
            ShowOnly(ui.panelDecision);
        }

        /// <summary>
        /// 호스트가 승인하고 복제한 공개 행동을 버전당 한 번만 표시한다.
        /// </summary>
        private void RaiseApprovedActionNoticeIfNew()
        {
            int version = gs.ActionAnnouncementVersion;
            if (version <= 0 || version == lastActionAnnouncementVersion) return;

            lastActionAnnouncementVersion = version;
            if (gs.GetLocalPublicRole() == PlayerRole.Dealer &&
                gs.LastApprovedActionActor == PlayerRole.Dealer &&
                gs.LastApprovedActionType == PlayerActionType.SubmitDealerCardChoice)
                return;

            if (ActionAnnouncementMapper.TryMap(
                    gs.LastApprovedActionActor,
                    gs.LastApprovedActionType,
                    out string actorText,
                    out string actionText))
                ui.ShowActionToast(gs.LastApprovedActionType, actorText, actionText);
        }

        private void ShowResultPanel()
        {
            bool aWon = gs.PublicOutcome == MatchOutcome.PlayerAWin;
            if (ui.btnContinue != null) ui.btnContinue.gameObject.SetActive(true);

            ShowOnly(ui.panelResult);

            // 승자 발표는 상대 손패가 다 열린 뒤로 미룬다. 카드를 한 장씩 뒤집는 동안
            // 결과가 먼저 떠 있으면 아무도 카드를 보지 않는다.
            // 버튼은 그 결과 카드 안에 있으므로 함께 늦게 나타난다 — 카드가 다 열리기
            // 전에 고발 단계로 넘기는 일이 구조적으로 막힌다.
            ui.PresentRoundResult(
                ZooJackText.RoundWinner(aWon ? PlayerRole.PlayerA : PlayerRole.PlayerB),
                aWon,
                foldedRole: gs.FoldedRole);

            // 남이 누르면 여기가 다시 돌며 숫자가 올라간다(서명에 ResultReadyMask가 들어 있다).
            ui.SetAccusationReadyState(
                gs.IsLocalReadyForAccusation, gs.AccusationReadyCount, gs.SeatedPlayerCount);
        }


        /// <summary>
        /// 비긴 판의 결과 화면. 승패가 갈린 판과 같은 카드를 쓴다 — 카드가 한 장씩 열리고
        /// 다 열린 뒤에 문구가 뜬다. 예전에는 우상단에 "다시 배분하는 중" 안내만 띄우고
        /// 곧바로 넘어가서, 무엇 때문에 비겼는지 볼 새가 없었다.
        /// </summary>
        private void ShowTiePanel()
        {
            if (ui.btnContinue != null) ui.btnContinue.gameObject.SetActive(true);

            ShowOnly(ui.panelResult);
            ui.PresentRoundResult(ZooJackText.Get(
                "Game.Result.Tie", "무승부! 서로의 점수가 같습니다"),
                playerAWon: false, tie: true);

            // 남이 누르면 여기가 다시 돌며 숫자가 올라간다(서명에 ResultReadyMask가 들어 있다).
            ui.SetTieRedealReadyState(
                gs.IsLocalReadyForAccusation, gs.AccusationReadyCount, gs.SeatedPlayerCount);
        }

        private void ShowAccusationPanel()
        {
            ui.RefreshAccusationCards();
            ShowOnly(ui.panelAccusation);
        }

        private void RefreshRevealedScoreCards()
        {
            var handA = gs.GetHandCards(PlayerRole.PlayerA);
            var handB = gs.GetHandCards(PlayerRole.PlayerB);
            ui.RefreshRevealedScoreCards(
                handA, ScoreOf(PlayerRole.PlayerA),
                handB, ScoreOf(PlayerRole.PlayerB));
        }

        private void ShowFinalPanel(bool showNextButton)
        {
            // ── 판정이 도착하기 전에는 아무것도 그리지 않는다 ──────────
            //
            // 단계(CurrentPhase)는 상태 복제로 오고 판정 결과는 RPC로 온다. 통로가 달라
            // 단계가 먼저 도착할 수 있는데, 그 사이 FinalJudgmentVersion은 아직 <b>지난
            // 라운드</b> 번호다. 그 번호는 이미 틀어 본 번호이므로 JudgmentSuspense가
            // "이미 본 판정"으로 보고 결과 뒤처리를 그 자리에서 실행했다 —
            // MatchOver는 상태 복제로 이미 도착해 있어서, <b>주사위가 구르기도 전에</b>
            // 매치 종료 화면과 팡파레가 터졌다. 매치가 끝난다는 사실이 그대로 새어 나갔다.
            //
            // 첫 라운드만 멀쩡했던 이유: 아직 아무것도 틀어 보지 않아 지난 번호(0)가
            // 무엇과도 같지 않았고, 그래서 "이미 본 판정" 길로 빠지지 않았다.
            //
            // 판정이 몇 번째 라운드 것인지 함께 실어 보내므로(LastFinalJudgmentRound)
            // 지금 라운드의 것이 아니면 기다린다. 다음 갱신에서 다시 온다.
            if (gs.CurrentPhase == GamePhase.FinalJudgment
                && gs.LastFinalJudgmentRound != gs.RoundIndex)
            {
                ShowTableOnly();
                return;
            }

            var winner = gs.FinalJudgmentVersion > 0 ? gs.LastFinalWinner : gs.VisibleFinalWinner;

            // 이 라운드를 기록 화면에 남길 재료. 위의 관문을 지난 자리라서 판정과 라운드
            // 번호가 서로 맞는다고 확신할 수 있고, 역할 교대도 이미 끝나 CharacterForRole이
            // 그 라운드의 사람을 가리킨다.
            //
            // <b>여기서 적지 않고 재료만 챙긴다.</b> 실제로 적는 것은 두구두구가 멈춰
            // 승자가 공개되는 순간이다(아래 콜백) — 이 자리에서 적으면 주사위가 아직
            // 구르는 동안 기록 화면에 결과가 먼저 떠서, 그 화면이 답을 미리 보는
            // 창구가 된다. 값을 지금 떠 두는 이유는 콜백이 도는 시점의 gs가 이미
            // 다음 라운드를 가리킬 수 있어서다.
            //
            // 딜러 자리의 뇌물은 '받은 것'이 아니라 '실제로 챙긴 것'이다. 반납된 몫까지
            // 더하면 잔액 변화와 맞지 않아 기록이 스스로를 반박한다.
            int recordRound = gs.RoundIndex;
            int keptTotal = gs.LastBribeKeptA + gs.LastBribeKeptB;
            var changeA = new MatchHistory.Change(gs.CharacterForRole(PlayerRole.PlayerA),
                PlayerRole.PlayerA, gs.LastPlayerADelta, gs.LastBribeA, gs.LastBribeKeptA);
            var changeDealer = new MatchHistory.Change(gs.CharacterForRole(PlayerRole.Dealer),
                PlayerRole.Dealer, gs.LastDealerDelta, keptTotal, keptTotal);
            var changeB = new MatchHistory.Change(gs.CharacterForRole(PlayerRole.PlayerB),
                PlayerRole.PlayerB, gs.LastPlayerBDelta, gs.LastBribeB, gs.LastBribeKeptB);
            bool recordManipulated = gs.LastRoundWasManipulated;
            var recordAccusation = gs.LastAccusationChoice;
            var recordAccusationResult = gs.LastAccusationResult;

            string winnerText = ZooJackText.FinalWinner(winner);

            ui.RefreshFinalPresentation(
                winner,
                gs.LastRoundWasManipulated,
                gs.LastPlayerADelta,
                gs.LastPlayerBDelta,
                gs.LastDealerDelta,
                gs.BalanceForRole(PlayerRole.PlayerA),
                gs.BalanceForRole(PlayerRole.PlayerB),
                gs.BalanceForRole(PlayerRole.Dealer));

            // 호스트는 이 단계에 들어서는 순간 이미 정산을 마쳤다. 그대로 두면 머니 바 숫자가
            // 주사위보다 먼저 결과를 말해 버리므로, 여기서 곧바로 정산 전 값으로 잠근다.
            // 다만 ±N 배지는 아직 붙이지 않는다 — 그것도 답을 흘리기 때문이다.
            ui.Money.HoldDelta(gs.CharacterForRole(PlayerRole.PlayerA),
                gs.BalanceForRole(PlayerRole.PlayerA), gs.LastPlayerADelta);
            ui.Money.HoldDelta(gs.CharacterForRole(PlayerRole.PlayerB),
                gs.BalanceForRole(PlayerRole.PlayerB), gs.LastPlayerBDelta);
            ui.Money.HoldDelta(gs.CharacterForRole(PlayerRole.Dealer),
                gs.BalanceForRole(PlayerRole.Dealer), gs.LastDealerDelta);

            // 두구두구 도중에는 버튼을 숨기고, 결과가 확정되는 순간에만 노출한다.
            if (ui.btnNextRound != null) ui.btnNextRound.gameObject.SetActive(false);

            ShowOnly(ui.panelFinal);
            ui.SetFinalStage(true);

            bool matchOver = gs.MatchOver; // 파산자가 나오면 다음 라운드가 없다

            // 처음 받은 판정이면 두구두구 연출, 재렌더면 결과만 그대로 유지.
            suspense.Show(gs.FinalJudgmentVersion, winner, winnerText, gs.LastFinalReason,
                winner == FinalWinner.Dealer,
                matchOver,
                () =>
                {
                    // 주사위가 승자를 보여 준 뒤에야 ±N이 뜬다.
                    ui.Money.RevealDeltas();

                    // 기록도 지금이 처음이다. 같은 라운드를 두 번 적어도 안전하다
                    // (MatchHistory가 걸러낸다) — 연출이 끝난 뒤의 재렌더도 이리로 온다.
                    // 순위(RecordRanking)보다 반드시 먼저다: 순위가 매치를 닫아 버리면
                    // 그 뒤에 오는 라운드 기록은 새 매치의 것으로 오해받는다.
                    MatchHistory.RecordRound(
                        recordRound, changeA, changeDealer, changeB,
                        recordManipulated, recordAccusation, recordAccusationResult, winner);

                    if (matchOver) { ShowMatchOver(); return; }
                    if (ui.btnNextRound != null) ui.btnNextRound.gameObject.SetActive(showNextButton);

                    // 모두가 누를 때까지 기다린다. 서명에 준비 상태가 들어 있어
                    // 남이 누르는 순간에도 이 경로가 다시 돌며 숫자가 올라간다.
                    if (showNextButton)
                        ui.SetNextRoundReadyState(
                            gs.IsLocalReadyForNextRound,
                            gs.NextRoundReadyCount,
                            gs.SeatedPlayerCount);
                });
        }

        // 매치 종료 화면: 슬롯 기준 최종 순위(파산자는 자동 최하위).
        // 잔액이 역할이 아니라 좌석에 귀속되므로 순위도 사람(슬롯) 기준으로 낸다.
        private void ShowMatchOver()
        {
            // 최종 순위는 정산 후 잔액으로 내므로 머니바도 같은 값이어야 한다.
            ui.Money.CommitDeltas();
            if (ui.btnNextRound != null) ui.btnNextRound.gameObject.SetActive(false);
            ui.SetFinalRoundDetailsVisible(false);

            // 슬롯 번호는 화면에 내지 않는다. 닉네임이 아직 네트워크로 넘어오지 않아
            // "플레이어 2"밖에 쓸 수 없는데, 사람들이 매치 내내 서로를 부르던 호칭은
            // 토끼·여우·악어다. 그래서 순위표는 캐릭터로 사람을 가리킨다.
            int[] slots = gs.FinalRankingSlots();
            var chars   = new CharacterId[slots.Length];
            var roles   = new PlayerRole[slots.Length];
            var ranked  = new int[slots.Length];
            for (int rank = 0; rank < slots.Length; rank++)
            {
                chars[rank]  = gs.CharacterForSlot(slots[rank]);
                roles[rank]  = gs.RoleForSlot(slots[rank]);
                ranked[rank] = gs.BalanceForSlot(slots[rank]);
            }

            // 종료 사유는 파산이 우선이고, 파산자가 없으면 최대 라운드 도달이다.
            CharacterId bankrupt = CharacterId.None;
            for (int slot = 1; slot <= FusionGameState.MaxPlayers; slot++)
                if (RoundSettlement.IsBankrupt(gs.BalanceForSlot(slot)))
                {
                    bankrupt = gs.CharacterForSlot(slot);
                    break;
                }

            ShowOnly(ui.panelFinal);
            ui.SetFinalStage(false);
            ui.SetFinalRoundDetailsVisible(false);

            // 지난 라운드 판정의 왕관·비석이 그대로 떠 있으면 낡은 정보다.
            // 이제 표식은 순위표 안에서만 뜬다.
            ui.Markers.RefreshFinal(FinalWinner.None);
            if (ui.sharedScoreCards != null) ui.sharedScoreCards.SetActive(false);

            CharacterId surrendered = gs.SurrenderedRole != PlayerRole.None
                ? gs.CharacterForRole(gs.SurrenderedRole)
                : CharacterId.None;

            ui.ShowMatchOverCard(bankrupt, gs.SurrenderedRole);
            ui.ShowFinalRanking(
                chars, roles, ranked, gs.CharacterForSlot(gs.LocalSlot), surrendered);

            // 기록 화면의 랭킹에 이 매치를 한 줄 쌓는다. 여러 번 불려도 한 번만 적힌다 —
            // 이 화면도 남이 '다시 플레이'를 누르면 다시 그려진다.
            var standings = new MatchHistory.Standing[slots.Length];
            for (int rank = 0; rank < slots.Length; rank++)
                standings[rank] = new MatchHistory.Standing(
                    chars[rank], roles[rank], ranked[rank],
                    RoundSettlement.IsBankrupt(ranked[rank]),
                    surrendered != CharacterId.None && chars[rank] == surrendered);
            MatchHistory.RecordRanking(standings);

            // 남이 누르면 여기가 다시 돌며 숫자가 올라간다(서명에 RematchReadyMask가 들어 있다).
            ui.SetRematchReadyState(
                gs.IsLocalReadyForRematch, gs.RematchReadyCount, gs.SeatedPlayerCount);
        }

        // ── 항복 ─────────────────────────────────────────────────────

        /// <summary>
        /// 앉아 있고, 판이 굴러가고 있고, 아직 아무도 접지 않았을 때만 항복할 수 있다.
        /// 환경설정 화면은 이 값 하나만 보고 버튼을 보이거나 숨긴다.
        /// </summary>
        bool MatchSurrender.IHost.CanSurrender =>
            gs != null
            && gs.Object != null && gs.Object.IsValid
            && !returningToLobby && !returningToRoomLobby
            && !gs.MatchOver
            && gs.SurrenderedRole == PlayerRole.None
            && gs.CurrentPhase != GamePhase.WaitingForPlayers
            && gs.GetLocalPublicRole() != PlayerRole.None;

        /// <summary>
        /// 항복을 호스트에게 올린다. 화면을 여기서 바꾸지 않는다 — 항복한 사람만 먼저
        /// 순위표로 넘어가면 다른 둘은 아직 판이 굴러가는 화면을 보고 있게 된다.
        /// 호스트가 상태를 되돌려 주면 셋이 같은 순간에 같은 화면으로 간다.
        /// </summary>
        void MatchSurrender.IHost.Surrender() => gs?.Rpc_RequestSurrender();

        // ── 상시 표시 (상태바 + 카드 슬롯) ────────────────────────────

        private void RefreshStatusBar(GamePhase phase)
        {
            // 최종 판정을 벗어나는 순간이 곧 '다음 라운드'가 눌린 시점이다. 버튼을 누른
            // 피어가 누구든 모든 화면에서 같은 타이밍에 숫자가 굴러가야 하므로,
            // 클릭이 아니라 단계 전환으로 잡는다. 예고가 없으면 아무 일도 하지 않는다.
            if (phase != GamePhase.FinalJudgment) ui.Money.CommitDeltas();

            // 매치가 끝나도 단계는 최종 판정 → 라운드 종료로 흘러간다. 상태바가 그 이름을
            // 그대로 쓰면 "다음 라운드를 준비해 주세요"가 뜨는데 다음 라운드는 없다.
            // 화면이 실제로 매치 종료로 바뀐 뒤부터 덮는다 — 두구두구가 도는 동안에는
            // 아직 최종 판정이 맞다.
            bool over = ui.IsMatchOverScreenUp;
            if (ui.txtPhase != null)
                ui.txtPhase.text = over ? GameDirector.MatchOverPhase : ZooJackText.PhaseName(phase);
            if (ui.txtPhaseAction != null)
                ui.txtPhaseAction.text = over ? GameDirector.MatchOverPhaseAction : PhaseActionKo(phase);
            if (ui.txtRound != null)
                ui.txtRound.text = ZooJackText.Get(
                    "Game.Round.Counter", "라운드 {0} / {1}",
                    gs.RoundIndex, RoundSettlement.MaxRounds);

            // 머니 바는 슬롯(=사람) 단위다. 슬롯은 매치 내내 고정이므로 초상화와 잔액이
            // 자리를 옮기지 않고, 역할 교대는 직군 라벨 텍스트로만 나타난다.
            for (int slot = 1; slot <= 3; slot++)
                ui.Money.SetCard(gs.CharacterForSlot(slot), gs.RoleForSlot(slot), gs.BalanceForSlot(slot));

            ui.Money.HighlightLocal(gs.CharacterForRole(gs.GetLocalPublicRole()));

            // 테이블 위 아바타는 반대로 역할 자리에 고정이라, 역할이 교대되면
            // 그 자리에 선 사람의 그림이 바뀐다.
            ui.RefreshCharacterVisuals(
                gs.CharacterForRole(PlayerRole.PlayerA),
                gs.CharacterForRole(PlayerRole.PlayerB),
                gs.CharacterForRole(PlayerRole.Dealer));
        }

        private string PhaseActionKo(GamePhase phase)
        {
            PlayerRole localRole = gs.GetLocalPublicRole();
            return phase switch
            {
                GamePhase.WaitingForPlayers => ZooJackText.Get(
                    "Game.Action.Waiting.Network", "모든 플레이어의 참가를 기다리고 있습니다."),
                GamePhase.RoleAssignment => ZooJackText.Get(
                    "Game.Action.RoleAssignment", "배정된 역할을 확인해 주세요."),
                GamePhase.BribeSelection         =>
                    ((localRole == PlayerRole.PlayerA && !gs.BribeSubmittedA) ||
                     (localRole == PlayerRole.PlayerB && !gs.BribeSubmittedB)) &&
                    !bribeSubmittedLocal
                        ? ZooJackText.Get("Game.Action.Bribe.Local", "딜러에게 줄 뇌물을 결정하세요.")
                        : ZooJackText.Get("Game.Action.OtherDecision", "다른 플레이어의 결정을 기다려 주세요."),
                GamePhase.BetSelection           =>
                    ((localRole == PlayerRole.PlayerA && !gs.BetSubmittedA) ||
                     (localRole == PlayerRole.PlayerB && !gs.BetSubmittedB)) &&
                    !betSubmittedLocal
                        ? ZooJackText.Get("Game.Action.Bet.Local", "카드를 확인하고 판돈을 결정하세요.")
                        : ZooJackText.Get("Game.Action.Bet.Other", "다른 플레이어의 판돈 결정을 기다려 주세요."),
                GamePhase.SystemResultGeneration => ZooJackText.Get(
                    "Game.Action.ResultGeneration", "게임 결과를 생성하고 있습니다."),
                GamePhase.CardCandidateGeneration => ZooJackText.Get(
                    "Game.Action.CardCandidates", "딜러의 카드 후보를 준비하고 있습니다."),
                GamePhase.DealerCardDistribution => localRole == PlayerRole.Dealer
                    ? ZooJackText.Get("Game.Action.Dealer.NetworkLocal", "배분할 카드를 선택하세요.")
                    : ZooJackText.Get("Game.Action.Dealer.Other", "딜러의 카드 배분을 기다려 주세요."),
                GamePhase.PlayerDecision         => localRole == gs.DecisionTurn
                    ? ZooJackText.Get("Game.Action.Player.Local", "히트 또는 스탠드를 선택하세요.")
                    : ZooJackText.Get("Game.Action.Player.Other", "{0}의 결정을 기다려 주세요.", RoleKo(gs.DecisionTurn)),
                GamePhase.BlackjackResultCalculation => ZooJackText.Get(
                    "Game.Action.ResultCalculation", "카드 결과를 계산하고 있습니다."),
                GamePhase.DealerDecision         => localRole == PlayerRole.Dealer
                    ? ZooJackText.Get("Game.Action.DealerDecision.NetworkLocal", "결과를 확인하고 결정을 내려 주세요.")
                    : ZooJackText.Get("Game.Action.DealerDecision.Other", "딜러의 결정을 기다려 주세요."),
                GamePhase.ResultReveal => ZooJackText.Get(
                    "Game.Action.ResultReveal", "공개된 라운드 결과를 확인하세요."),
                GamePhase.Accusation             => gs.IsLocalPlayerAllowedToAccuse()
                    ? ZooJackText.Get("Game.Action.Accusation.Local", "고발 여부를 결정하세요.")
                    : ZooJackText.Get("Game.Action.Accusation.Other", "패배한 플레이어의 결정을 기다려 주세요."),
                GamePhase.FinalJudgment => ZooJackText.Get(
                    "Game.Action.FinalJudgment", "최종 판정 결과를 확인하세요."),
                GamePhase.TieRedeal => ZooJackText.Get(
                    "Game.Action.TieRedeal.Network", "무승부입니다. 모두 확인하면 카드를 다시 나눕니다."),
                GamePhase.RoundEnd => ZooJackText.Get(
                    "Game.Action.RoundEnd", "다음 라운드를 준비해 주세요."),
                _                                => string.Empty
            };
        }

        private void RefreshCardSlots()
        {
            var viewer = gs.GetLocalPublicRole();
            var aCards = gs.GetHandCards(PlayerRole.PlayerA);
            var bCards = gs.GetHandCards(PlayerRole.PlayerB);
            bool reveal = GameDirector.IsRevealPhase(gs.CurrentPhase);
            bool dealerView = viewer == PlayerRole.Dealer;
            int aFaceUp = reveal || dealerView || viewer == PlayerRole.PlayerA
                ? GameDirector.AllFaceUp
                : GameDirector.UpcardOnly;
            int bFaceUp = reveal || dealerView || viewer == PlayerRole.PlayerB
                ? GameDirector.AllFaceUp
                : GameDirector.UpcardOnly;

            // 역할이 바뀌어도 물리 슬롯은 고정한다. A는 왼쪽, B는 오른쪽이다.
            ui.RenderFixedRoles(aCards, aFaceUp, bCards, bFaceUp);
        }

        // 시계는 단계마다 주인과 길이가 다르다. 카드 배분은 딜러, 히트/스탠드는 지금 차례인
        // 플레이어, 고발은 겉보기 패자가 쥔다. 뇌물·판돈만 주인이 둘이다 — A와 B가 동시에
        // 고르고 마감도 하나이므로, 아직 내지 않은 사람 모두에게 같은 숫자가 붙는다.
        //
        // 계산 방식은 단계마다 다르지만(경과 시간 누적 / 마감 틱), 여기서
        // (주인, 남은 초, 전체 길이)로 통일해 아래 표시 코드가 하나로 유지된다.
        // 매치 종료 카드의 남은 시간. 화면 갱신 시그니처에는 넣지 않는다 — 매 프레임
        // 바뀌는 값이라 넣으면 UI 전체가 초당 수십 번 다시 그려진다(아바타 좌표와 같은 이유).
        private void UpdateMatchOverCountdown()
        {
            if (!ui.IsMatchOverScreenUp) return;
            ui.SetMatchOverCountdown(gs.MatchOverSecondsLeft);
        }

        private void UpdateTurnTimer()
        {
            PlayerRole owner = PlayerRole.None, coOwner = PlayerRole.None;
            float left = 0f, limit = 0f;

            // 주인 없는 시계. 전원이 함께 누르는 구간이라 명패에는 아무것도 붙지 않지만
            // 가운데 시계는 돌아야 한다.
            bool ownerless = false;

            switch (gs.CurrentPhase)
            {
                case GamePhase.BribeSelection:
                    // 낸 사람은 시계를 내린다. 남은 사람만 기다림의 대상이다.
                    owner   = gs.IsBribePending(PlayerRole.PlayerA) ? PlayerRole.PlayerA : PlayerRole.None;
                    coOwner = gs.IsBribePending(PlayerRole.PlayerB) ? PlayerRole.PlayerB : PlayerRole.None;
                    limit = gs.BribeSelectionLimitSeconds;
                    left  = gs.BribeSecondsLeft;
                    break;

                case GamePhase.BetSelection:
                    owner   = gs.IsBetPending(PlayerRole.PlayerA) ? PlayerRole.PlayerA : PlayerRole.None;
                    coOwner = gs.IsBetPending(PlayerRole.PlayerB) ? PlayerRole.PlayerB : PlayerRole.None;
                    limit = gs.BetSelectionLimitSeconds;
                    left  = gs.BetSecondsLeft;
                    break;

                case GamePhase.DealerCardDistribution:
                    owner = PlayerRole.Dealer;
                    limit = gs.DealerDecisionLimitSeconds;
                    left  = limit - gs.DealerDecisionElapsedTime;
                    break;

                case GamePhase.PlayerDecision:
                    owner = gs.DecisionTurn;
                    limit = gs.PlayerDecisionLimitSeconds;
                    left  = limit - gs.PlayerDecisionElapsedTime;
                    break;

                case GamePhase.Accusation:
                    owner = gs.AccuserRole;   // 이미 답했으면 None → 시계가 멈춘다
                    limit = gs.AccusationLimitSeconds;
                    left  = gs.AccusationSecondsLeft;
                    break;

                // 아래 둘은 특정한 한 사람의 차례가 아니라 전원이 버튼을 누르는 구간이다.
                // 머리 위 명패에 시계를 붙일 대상이 없으므로 가운데 시계만 띄운다.
                case GamePhase.ResultReveal:
                    ownerless = true;
                    limit = gs.ResultRevealLimitSeconds;
                    left  = gs.ResultRevealSecondsLeft;
                    break;

                // 무승부 결과도 전원이 누르는 구간이다. 같은 마감을 쓰므로 같은 시계다.
                case GamePhase.TieRedeal:
                    ownerless = true;
                    limit = gs.ResultRevealLimitSeconds;
                    left  = gs.TieRedealSecondsLeft;
                    break;

                case GamePhase.RoundEnd:
                    // 같은 단계에서 두 화면이 나온다. 매치가 끝났으면 매치 종료 마감을,
                    // 아니면 다음 라운드 마감을 건다. 시계 카드는 어느 쪽이든 같은 자리다.
                    ownerless = true;
                    limit = gs.MatchOver
                        ? gs.MatchOverLimitSeconds
                        : gs.RoundEndLimitSeconds;
                    left = gs.MatchOver ? gs.MatchOverSecondsLeft : gs.RoundEndSecondsLeft;
                    break;
            }

            bool running = limit > 0f
                && (owner != PlayerRole.None || coOwner != PlayerRole.None || ownerless);
            left = Mathf.Max(0f, left);

            // 명패는 남의 남은 시간을 알리는 것이 목적이라 로컬 역할과 무관하게 갱신한다.
            ui.RefreshAvatarPlates(gs.CurrentPhase,
                                   running ? owner : PlayerRole.None,
                                   running ? coOwner : PlayerRole.None, left);

            RefreshReadyBadges();
            ui.SetTurnTimerVisible(running);
            TurnCountdownSound.Update(running, left);
            if (running) SubmitSelectionIfTimeIsUp(left);

            if (!running || ui.txtDealerTimer == null) return;

            arranger?.StyleTimer(ui.txtDealerTimer, left);
            if (ui.dealerTimerHourglass != null)
                ui.dealerTimerHourglass.SandProgress = 1f - Mathf.Clamp01(left / limit);
        }

        /// <summary>
        /// 낼 것을 낸 사람 발밑에 <see cref="AvatarStage.ReadyBadge"/>를 붙인다.
        /// 어느 단계에서 뜻이 있는지는 <see cref="GameDirector.SetReadyBadge"/> 쪽 설명 참고.
        ///
        /// 시계 주인을 고르는 것과 <b>같은 물음</b>을 본다 — 아직 안 낸 사람에게는 시계가,
        /// 낸 사람에게는 준비 표시가 붙는다. 그래서 둘이 한 사람 머리 위에 같이 뜨는 일이 없다.
        /// </summary>
        private void RefreshReadyBadges()
        {
            bool bribeStage = gs.CurrentPhase == GamePhase.BribeSelection;
            bool betStage   = gs.CurrentPhase == GamePhase.BetSelection;

            ui.SetReadyBadge(PlayerRole.PlayerA, HasSubmitted(PlayerRole.PlayerA, bribeStage, betStage));
            ui.SetReadyBadge(PlayerRole.PlayerB, HasSubmitted(PlayerRole.PlayerB, bribeStage, betStage));
            ui.SetReadyBadge(PlayerRole.Dealer, false);
        }

        private bool HasSubmitted(PlayerRole role, bool bribeStage, bool betStage) =>
            bribeStage ? !gs.IsBribePending(role)
          : betStage   ? !gs.IsBetPending(role)
                       : false;

        /// <summary>
        /// 뇌물·판돈 시계가 0에 닿으면 지금 화면에 고른 값을 그대로 낸다.
        ///
        /// 호스트에도 같은 마감이 있지만 그쪽은 기본값(뇌물 0 / 최소 판돈)으로 채운다.
        /// 화면 앞에 앉아 금액까지 골라 놓고 확인만 못 누른 사람의 선택을 버리지 않으려면
        /// 각자 자기 것을 먼저 내야 한다. 호스트의 마감은 아무도 없는 자리를 위한 것이다.
        ///
        /// 이 경로는 자기 몫에만 관여한다 — 남의 미제출은 호스트가 처리한다.
        /// </summary>
        private void SubmitSelectionIfTimeIsUp(float secondsLeft)
        {
            if (secondsLeft > 0f) return;

            var localRole = gs.GetLocalPublicRole();
            if (localRole != PlayerRole.PlayerA && localRole != PlayerRole.PlayerB) return;

            if (gs.CurrentPhase == GamePhase.BribeSelection && !bribeSubmittedLocal
                && gs.IsBribePending(localRole))
                ConfirmBribe();
            else if (gs.CurrentPhase == GamePhase.BetSelection && !betSubmittedLocal
                && gs.IsBetPending(localRole))
                ConfirmBet();
        }

        // ── 갱신 감지 ─────────────────────────────────────────────────

        private long ComputeSignature()
        {
            long sig = 17;
            sig = sig * 31 + (int)gs.CurrentPhase;
            sig = sig * 31 + gs.RoundIndex;
            sig = sig * 31 + gs.RoleAssignmentVersion;
            sig = sig * 31 + (int)gs.DecisionTurn;
            sig = sig * 31 + (int)gs.FoldedRole;
            sig = sig * 31 + (gs.HasDoubled(PlayerRole.PlayerA) ? 1024 : 0);
            sig = sig * 31 + (gs.HasDoubled(PlayerRole.PlayerB) ? 2048 : 0);
            // 역할 교대로 초상화가 옮겨 가는 순간을 놓치지 않으려면 서명에 넣어야 한다
            for (int slot = 1; slot <= FusionGameState.MaxPlayers; slot++)
                sig = sig * 31 + (int)gs.RoleForSlot(slot);
            sig = sig * 31 + gs.Stake; // 더블다운 수락 시 2배로 바뀐다
            sig = sig * 31 + gs.BetA;
            sig = sig * 31 + gs.BetB;
            sig = sig * 31 + (gs.BribeSubmittedA ? 1 : 0);
            sig = sig * 31 + (gs.BribeSubmittedB ? 2 : 0);
            sig = sig * 31 + (gs.BetSubmittedA ? 4 : 0);
            sig = sig * 31 + (gs.BetSubmittedB ? 8 : 0);
            sig = sig * 31 + (gs.CanAccuse ? 4 : 0);
            sig = sig * 31 + (bribeSubmittedLocal ? 8 : 0);
            sig = sig * 31 + (betSubmittedLocal ? 16 : 0);
            sig = sig * 31 + (int)gs.PublicOutcome;
            sig = sig * 31 + gs.RevealedWinnerPlayerId;
            sig = sig * 31 + gs.LocalDealerCandidateVersion;
            sig = sig * 31 + gs.FinalJudgmentVersion;
            // 남이 준비를 누르면 버튼의 인원수가 바뀌어야 한다 (고발 단계 · 다음 라운드)
            sig = sig * 31 + gs.NextRoundReadyMask;
            sig = sig * 31 + gs.ResultReadyMask;
            sig = sig * 31 + gs.RematchReadyMask;
            sig = sig * 31 + (int)gs.GetLocalPublicRole();
            for (int slot = 1; slot <= FusionGameState.MaxPlayers; slot++)
                sig = sig * 31 + gs.BalanceForSlot(slot);
            sig = sig * 31 + (gs.MatchOver ? 16 : 0);
            sig = sig * 31 + (int)gs.SurrenderedRole;

            for (int i = 0; i < FusionGameState.MaxHandCards; i++)
                sig = sig * 131 + gs.HandACards[i] * 53 + gs.HandBCards[i];
            return sig;
        }

        // ── 세션 종료 처리 ────────────────────────────────────────────

        private static int CountActivePlayers(NetworkRunner runner)
        {
            int count = 0;
            foreach (var _ in runner.ActivePlayers) count++;
            return count;
        }

        /// <summary>
        /// 세션을 살린 채 방 로비로 되돌린다. 다시 플레이의 출구다.
        ///
        /// <see cref="ReturnToLobby"/>와 다르다 — 저쪽은 러너를 내리고 메인 메뉴로 가지만
        /// 여기는 러너와 <see cref="FusionGameState"/>를 그대로 두고 씬만 바꾼다. 그래야
        /// 세 사람이 같은 방에 머문 채 캐릭터만 다시 고를 수 있다.
        ///
        /// 호스트만 씬을 연다. Fusion이 나머지에게 전환을 복제하므로 클라이언트는
        /// 기다리기만 하면 된다. Unity의 SceneManager.LoadScene을 쓰면 네트워크
        /// 씬 오브젝트가 파괴되어 세션이 무너지므로 절대 쓰지 않는다.
        /// </summary>
        private void ReturnToRoomLobby()
        {
            if (returningToRoomLobby) return;
            returningToRoomLobby = true;
            ui?.emotionWheel?.CancelSelection();

            var runner = gs != null ? gs.Runner : null;
            if (runner == null || !runner.IsServer) return;   // 클라이언트는 복제를 기다린다

            var sceneRef = ResolveSceneRef(ZooJackScenes.Lobby);
            if (!sceneRef.IsValid)
            {
                Debug.LogError($"[NetworkGameDirector] '{ZooJackScenes.Lobby}' 씬을 빌드 설정에서 찾지 못했습니다.");
                return;
            }

            Debug.Log("[NetworkGameDirector] 방 로비로 돌아갑니다 (다시 플레이).");
            runner.LoadScene(sceneRef, LoadSceneMode.Single);
        }

        // 씬 이름으로 빌드 인덱스 기반 SceneRef를 해석한다 (빌드 인덱스 하드코딩 방지).
        private static SceneRef ResolveSceneRef(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                    return SceneRef.FromIndex(i);
            }
            return SceneRef.None;
        }

        private async void ReturnToLobby(string reason)
        {
            if (returningToLobby) return;
            returningToLobby = true;
            ui?.emotionWheel?.CancelSelection();
            Debug.Log($"[NetworkGameDirector] 로비 복귀: {reason}");

            var runner = UnityEngine.Object.FindFirstObjectByType<NetworkRunner>();
            if (runner != null)
            {
                await runner.Shutdown();
                if (runner != null && runner.gameObject != null)
                    Destroy(runner.gameObject);
            }
            SceneManager.LoadScene(ZooJackScenes.Lobby);
        }

        // ── UI/문자열 헬퍼 ────────────────────────────────────────────

        private void ShowOnly(GameObject panel) =>
            panels.ShowOnly(panel, gs != null && gs.CurrentPhase == GamePhase.Accusation);

        private BlackjackScore ScoreOf(PlayerRole role) =>
            BlackjackScoreCalculator.Calculate(new BlackjackHand { Cards = gs.GetHandCards(role) });

        /// <summary>
        /// 역할 이름. <see cref="GameDirector.RoleKo"/>와 <b>일부러 다르다</b> — 이쪽은 아직
        /// 배역이 정해지지 않은 자리를 "미정"이라고 적고, 핫시트 쪽은 빈 문자열을 준다.
        /// 핫시트에서는 시작 순간에 셋의 배역이 모두 정해져 None이 화면에 나올 일이 없지만,
        /// 네트워크에서는 남이 들어오기 전의 빈자리를 그려야 한다.
        /// 같아 보인다고 합치면 로비에 빈칸이 뜬다.
        /// </summary>
        private static string RoleKo(PlayerRole role) => role == PlayerRole.None
            ? ZooJackText.Get("Game.Role.Unknown", "미정")
            : ZooJackText.RoleName(role);

        private static string CardStr(BlackjackCard c)
        {
            if (c == null) return "?";
            string r = (int)c.Rank switch
            {
                1  => "A",
                11 => "J",
                12 => "Q",
                13 => "K",
                _  => ((int)c.Rank).ToString()
            };
            string s = c.Suit switch
            {
                BlackjackCardSuit.Hearts   => "♥",
                BlackjackCardSuit.Diamonds => "♦",
                BlackjackCardSuit.Clubs    => "♣",
                _                          => "♠"
            };
            return r + s;
        }

        private static string HandStr(List<BlackjackCard> cards)
        {
            if (cards == null || cards.Count == 0)
                return ZooJackText.Get("Common.None.Parenthesized", "(없음)");
            var sb = new System.Text.StringBuilder();
            foreach (var c in cards)
            {
                if (sb.Length > 0) sb.Append(" ");
                sb.Append(CardStr(c));
            }
            return sb.ToString();
        }
    }
}
#else
using UnityEngine;

namespace ZooJack
{
    public class NetworkGameDirector : MonoBehaviour { }
}
#endif
