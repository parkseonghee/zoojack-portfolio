using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace ZooJack.Editor
{
    /// <summary>한국어 원문 테이블과 상황 메타데이터를 만들고, 새 기본 항목만 안전하게 보충한다.</summary>
    public static class ZooJackTextCatalogSetupTool
    {
        public const string Root = "Assets/Localization/ZooJack";
        public const string SettingsPath = Root + "/ZooJackLocalizationSettings.asset";
        public const string LocalePath = Root + "/Korean (ko).asset";

        private sealed class Seed
        {
            public readonly string Key;
            public readonly string Text;
            public readonly string Category;
            public readonly string Scene;
            public readonly string Target;
            public readonly string Trigger;
            public readonly string Variables;
            public readonly string Preview;
            public readonly int MaxLines;

            public Seed(string key, string text, string category, string scene, string target,
                string trigger, string variables = "", string preview = "", int maxLines = 0)
            {
                Key = key;
                Text = text;
                Category = category;
                Scene = scene;
                Target = target;
                Trigger = trigger;
                Variables = variables;
                Preview = preview;
                MaxLines = maxLines;
            }
        }

        // 첫 구축용 원문이다. 재실행 시 이미 존재하는 번역 값은 절대 덮어쓰지 않는다.
        private static readonly Seed[] Seeds =
        {
            S("Common.Role.PlayerA", "플레이어 A", "공통", "전체", "직군명", "플레이어 A를 표시할 때", max: 1),
            S("Common.Role.PlayerB", "플레이어 B", "공통", "전체", "직군명", "플레이어 B를 표시할 때", max: 1),
            S("Common.Role.Dealer", "딜러", "공통", "전체", "직군명", "딜러를 표시할 때", max: 1),
            S("Common.Role.Spectator", "관전자", "공통", "전체", "직군명", "조작 역할이 없을 때", max: 1),
            S("Common.Character.Rabbit", "토끼", "공통", "전체", "캐릭터명", "토끼 캐릭터 이름을 표시할 때", max: 1),
            S("Common.Character.Fox", "여우", "공통", "전체", "캐릭터명", "여우 캐릭터 이름을 표시할 때", max: 1),
            S("Common.Character.Croc", "악어", "공통", "전체", "캐릭터명", "악어 캐릭터 이름을 표시할 때", max: 1),

            S("Lobby.Status.Start", "서버를 만들거나 찾아서 참가하세요.", "로비", "LobbyScene", "Status", "메인 로비에 처음 들어왔을 때", max: 2),
            S("Lobby.Status.CreateHint", "방 공개 여부와 이름을 설정하세요.", "로비", "LobbyScene", "Status", "방 만들기 화면을 열었을 때", max: 2),
            S("Lobby.Create.PublicRoom", "공개방", "로비", "LobbyScene", "CreatePanel/Visibility", "공개방 선택지", max: 1),
            S("Lobby.Create.PrivateRoom", "비공개방", "로비", "LobbyScene", "CreatePanel/Visibility", "비공개방 선택지", max: 1),
            S("Lobby.Create.PasswordPlaceholder", "비밀번호를 입력하세요", "로비", "LobbyScene", "CreatePanel/Password", "비공개방 비밀번호 입력란", max: 1),
            S("Lobby.Status.BrowseLoading", "방 목록을 불러오는 중...", "로비", "LobbyScene", "Status", "방 검색을 시작했을 때", max: 1),
            S("Lobby.Status.BrowseSelect", "방을 선택해 참가하세요.", "로비", "LobbyScene", "Status", "방 검색을 완료했을 때", max: 1),
            S("Lobby.Button.Join", "참가", "로비", "LobbyScene", "BrowsePanel/JoinButton", "선택한 방에 참가할 때", max: 1),
            S("Lobby.Button.Close", "닫기", "로비", "LobbyScene", "CreatePanel/BrowsePanel/CloseButton", "서버 생성 또는 서버 찾기 화면을 닫을 때", max: 1),
            S("Lobby.Status.RoomSelected", "'{0}' 방을 선택했습니다. 참가 버튼을 눌러 주세요.", "로비", "LobbyScene", "Status", "방 목록에서 참가할 방을 선택했을 때", "{0}=방 이름", "'동물의 숲' 방을 선택했습니다. 참가 버튼을 눌러 주세요.", 2),
            S("Lobby.Status.SelectRoomFirst", "참가할 방을 먼저 선택하세요.", "로비", "LobbyScene", "Status", "방을 선택하지 않고 참가를 시도했을 때", max: 1),
            S("Lobby.Status.RoomUnavailable", "선택한 방에는 지금 참가할 수 없습니다.", "로비", "LobbyScene", "Status", "선택한 방이 닫히거나 정원이 찼을 때", max: 1),
            S("Lobby.Status.NoRooms", "열려 있는 방이 없습니다. 직접 만들어 보세요.", "로비", "LobbyScene", "BrowseEmpty", "검색된 방이 없을 때", max: 2),
            S("Lobby.Status.SelectCharacter", "아래에서 캐릭터를 고르세요. 고르면 WASD·방향키로 움직일 수 있습니다.", "로비", "LobbyScene", "Status", "방에 들어왔지만 캐릭터를 고르지 않았을 때", max: 2),
            S("Lobby.Room.ControlHint", "WASD · 방향키로 이동    E: 감정표현", "로비", "LobbyScene", "RoomPanel/TopBar/Hint", "방 로비에서 이동과 감정표현 조작을 안내할 때", max: 1),
            S("Lobby.Status.Ready", "준비 완료! 다른 플레이어를 기다리는 중...", "로비", "LobbyScene", "Status", "내가 준비를 완료했을 때", max: 2),
            S("Lobby.Status.ReadyCanceled", "준비 취소됨.", "로비", "LobbyScene", "Status", "준비를 취소했을 때", max: 1),
            S("Lobby.Status.AllReady", "모든 플레이어 준비 완료! 게임 시작 중...", "로비", "LobbyScene", "Status", "모든 접속자가 준비했을 때", max: 2),
            S("Lobby.Button.Ready", "준비", "로비", "LobbyScene", "ReadyButton", "준비하지 않은 상태", max: 1),
            S("Lobby.Button.CancelReady", "준비 취소", "로비", "LobbyScene", "ReadyButton", "이미 준비한 상태", max: 1),
            S("Lobby.Card.Free", "빈자리", "로비", "LobbyScene", "CharacterCard/State", "아무도 캐릭터를 고르지 않았을 때", max: 1),
            S("Lobby.Card.Mine", "내 캐릭터", "로비", "LobbyScene", "CharacterCard/State", "내가 선택한 캐릭터", max: 1),
            S("Lobby.Card.Ready", "✓ 준비 완료", "로비", "LobbyScene", "CharacterCard/State", "해당 플레이어가 준비했을 때", max: 1),
            S("Lobby.Card.Taken", "다른 플레이어", "로비", "LobbyScene", "CharacterCard/State", "다른 사람이 선택한 캐릭터", max: 1),
            S("Lobby.Room.Info", "방: {0}  [{1}]", "로비", "LobbyScene", "RoomInfo", "방에 접속한 동안", "{0}=방 이름, {1}=방장 또는 참가자", "방: 동물의 숲  [방장]", 1),
            S("Lobby.Role.Unselected", "미선택", "로비", "LobbyScene", "직군명", "캐릭터를 고르지 않은 상태", max: 1),
            S("Lobby.Status.RoomCount", "{0}개의 방을 찾았습니다.", "로비", "LobbyScene", "Status", "검색된 방이 한 개 이상일 때", "{0}=방 개수", "3개의 방을 찾았습니다.", 1),
            S("Lobby.Room.Host", "방장", "로비", "LobbyScene", "RoomInfo", "내가 방장일 때", max: 1),
            S("Lobby.Room.Guest", "참가자", "로비", "LobbyScene", "RoomInfo", "다른 사람의 방에 참가했을 때", max: 1),
            S("Lobby.Status.MatchEnded", "매치가 끝났습니다. 캐릭터를 다시 고르세요.", "로비", "LobbyScene", "Status", "재대결을 위해 방 로비로 돌아왔을 때", max: 1),
            S("Lobby.Status.Initializing", "서버 초기화 중...", "로비", "LobbyScene", "Status", "FusionGameState를 기다릴 때", max: 1),
            S("Lobby.Status.InitializingRetry", "서버 초기화 중... 잠시 후 다시 시도하세요.", "로비", "LobbyScene", "Status", "초기화 전에 입력했을 때", max: 1),
            S("Lobby.Status.GameLoading", "게임 시작! 로딩 중...", "로비", "LobbyScene", "Status", "게임 Scene으로 전환할 때", max: 1),
            S("Lobby.Status.Settings", "환경 설정", "로비", "LobbyScene", "Status", "환경 설정 화면을 열었을 때", max: 1),
            S("Lobby.Status.EnterRoomName", "방 이름을 입력하세요.", "로비", "LobbyScene", "Status", "빈 방 이름으로 만들기를 눌렀을 때", max: 1),
            S("Lobby.Status.EnterCreatePassword", "비공개방 비밀번호를 입력하세요.", "로비", "LobbyScene", "Status", "비공개방에서 비밀번호 없이 만들기를 눌렀을 때", max: 1),
            S("Lobby.Status.BrowseFailed", "방 목록을 불러오지 못했습니다: {0}", "로비", "LobbyScene", "Status", "방 목록 연결 실패", "{0}=Fusion 종료 사유", max: 2),
            S("Lobby.Password.RoomTitle", "'{0}' 비밀 방", "로비", "LobbyScene", "PasswordPanel/Title", "비밀번호가 있는 방을 선택했을 때", "{0}=방 이름", max: 1),
            S("Lobby.Status.EnterPassword", "비밀번호를 입력하세요.", "로비", "LobbyScene", "Status", "비밀 방 참가 화면", max: 1),
            S("Lobby.Status.WrongPassword", "비밀번호가 일치하지 않습니다.", "로비", "LobbyScene", "Status", "틀린 비밀번호를 입력했을 때", max: 1),
            S("Lobby.Status.CreatingRoom", "방을 만드는 중...", "로비", "LobbyScene", "Status", "방 생성 요청 중", max: 1),
            S("Lobby.Status.JoiningRoom", "방에 참가하는 중...", "로비", "LobbyScene", "Status", "방 참가 요청 중", max: 1),
            S("Lobby.Status.ConnectionFailed", "연결 실패: {0}", "로비", "LobbyScene", "Status", "방 생성 또는 참가 실패", "{0}=Fusion 종료 사유", max: 1),
            S("Lobby.Status.CancelReadyToChange", "준비를 취소해야 캐릭터를 바꿀 수 있습니다.", "로비", "LobbyScene", "Status", "준비 상태에서 캐릭터를 바꾸려 할 때", max: 2),
            S("Lobby.Status.CharacterSelected", "{0} 선택 — {1}. WASD 또는 방향키로 움직여 보세요.", "로비", "LobbyScene", "Status", "캐릭터 선택 직후", "{0}=캐릭터명, {1}=직군명", max: 2),
            S("Lobby.Status.SelectCharacterFirst", "캐릭터를 먼저 고르세요.", "로비", "LobbyScene", "Status", "캐릭터 없이 준비를 누를 때", max: 1),
            S("Lobby.Status.LeavingRoom", "방에서 나가는 중...", "로비", "LobbyScene", "Status", "방 나가기 처리 중", max: 1),
            S("Lobby.Status.LeftRoom", "방에서 나왔습니다.", "로비", "LobbyScene", "Status", "방에서 정상적으로 나온 뒤", max: 1),
            S("Lobby.Status.RoomNotFound", "방을 찾을 수 없습니다.", "로비", "LobbyScene", "Status", "선택한 방이 사라졌을 때", max: 1),
            S("Lobby.Status.RoomFull", "방이 꽉 찼습니다.", "로비", "LobbyScene", "Status", "정원이 찬 방에 참가할 때", max: 1),
            S("Lobby.Status.Disconnected", "연결 종료: {0}", "로비", "LobbyScene", "Status", "기타 이유로 연결이 종료됐을 때", "{0}=종료 사유", max: 1),
            S("Lobby.Status.HostLeft", "방장이 나갔습니다.", "로비", "LobbyScene", "Status", "방장 연결이 끊겼을 때", max: 1),
            S("Lobby.Status.JoinFailed", "참가 실패 ({0}). 비밀번호가 틀렸거나 방이 닫혔습니다.", "로비", "LobbyScene", "Status", "Fusion 접속 실패", "{0}=접속 실패 사유", max: 2),
            S("Lobby.Avatar.ReadyBadge", "준비 ✓", "로비", "LobbyScene", "Avatar/PlateAction", "해당 플레이어가 준비했을 때", max: 1),
            S("Lobby.SelectPrompt", "역할을 선택하세요", "로비", "LobbyScene", "RoomPanel/TopBar/SelectPrompt", "방에 들어왔지만 아직 캐릭터를 고르지 않았을 때", max: 1),
            S("Lobby.Room.Count", "{0} / {1}", "로비", "LobbyScene", "RoomPanel/TopBar/RoomCount", "방에 들어온 인원을 표시할 때", "{0}=현재 인원, {1}=정원", "2 / 3", 1),
            S("Lobby.Status.CharacterCleared", "선택을 취소했습니다. 아래에서 역할을 고르세요.", "로비", "LobbyScene", "Status", "고른 캐릭터를 다시 눌러 취소했을 때", max: 2),

            S("Settings.Nickname", "닉네임", "환경설정", "전체", "Settings/Row_Nickname/Name", "환경설정을 열었을 때", max: 1),
            S("Settings.Nickname.Placeholder", "이름을 지어 주세요", "환경설정", "전체", "Settings/Row_Nickname/Input/Placeholder", "닉네임을 아직 짓지 않았을 때", max: 1),

            S("RoleReveal.Eyebrow", "새로운 역할", "역할 공개", "GameScene", "RoleReveal/Eyebrow", "첫 배정 또는 역할 교대 직후", max: 1),
            S("RoleReveal.Title", "당신은 {0}입니다", "역할 공개", "GameScene", "RoleReveal/Title", "역할 공개 카드가 나타날 때", "{0}=직군명", "당신은 딜러입니다", 1),
            S("RoleReveal.PlayerA.Description", "카드를 확인하고 판돈을 걸어 플레이어 B를 이기세요.", "역할 공개", "GameScene", "RoleReveal/Description", "플레이어 A 역할 공개", max: 2),
            S("RoleReveal.PlayerB.Description", "카드를 확인하고 판돈을 걸어 플레이어 A를 이기세요.", "역할 공개", "GameScene", "RoleReveal/Description", "플레이어 B 역할 공개", max: 2),
            S("RoleReveal.Dealer.Description", "비공개 뇌물을 확인하고 두 플레이어에게 카드를 배분하세요.", "역할 공개", "GameScene", "RoleReveal/Description", "딜러 역할 공개", max: 2),
            S("RoleReveal.Spectator.Description", "라운드의 진행을 지켜보세요.", "역할 공개", "GameScene", "RoleReveal/Description", "관전자 역할 공개", max: 2),

            S("Game.Role.Unknown", "미정", "게임 진행", "GameScene", "직군명", "아직 역할이 정해지지 않았을 때", max: 1),
            S("Game.Phase.Waiting", "대기 중", "게임 단계", "GameScene", "Phase", "플레이어 참가 대기", max: 1),
            S("Game.Phase.RoleAssignment", "역할 배정", "게임 단계", "GameScene", "Phase", "역할 배정 및 공개", max: 1),
            S("Game.Phase.Bribe", "뇌물 선택", "게임 단계", "GameScene", "Phase", "뇌물 선택 단계", max: 1),
            S("Game.Phase.Bet", "판돈 선택", "게임 단계", "GameScene", "Phase", "판돈 선택 단계", max: 1),
            S("Game.Phase.ResultGeneration", "결과 생성", "게임 단계", "GameScene", "Phase", "시스템 결과 생성 단계", max: 1),
            S("Game.Phase.CardCandidates", "카드 후보 생성", "게임 단계", "GameScene", "Phase", "딜러 후보 카드 준비", max: 1),
            S("Game.Phase.DealerCards", "딜러 카드 선택", "게임 단계", "GameScene", "Phase", "딜러 카드 배분 단계", max: 1),
            S("Game.Phase.PlayerDecision", "플레이어 결정", "게임 단계", "GameScene", "Phase", "히트·스탠드 등 행동 선택", max: 1),
            S("Game.Phase.ResultCalculation", "결과 계산", "게임 단계", "GameScene", "Phase", "블랙잭 결과 계산", max: 1),
            S("Game.Phase.DealerDecision", "딜러 결정", "게임 단계", "GameScene", "Phase", "딜러의 결과 결정", max: 1),
            S("Game.Phase.ResultReveal", "결과 공개", "게임 단계", "GameScene", "Phase", "겉보기 결과 공개", max: 1),
            S("Game.Phase.Accusation", "고발", "게임 단계", "GameScene", "Phase", "고발 선택", max: 1),
            S("Game.Phase.FinalJudgment", "최종 판정", "게임 단계", "GameScene", "Phase", "고발 반영 후 최종 판정", max: 1),
            S("Game.Phase.TieRedeal", "무승부 재배분", "게임 단계", "GameScene", "Phase", "무승부로 카드를 다시 받을 때", max: 1),
            S("Game.Phase.RoundEnd", "라운드 종료", "게임 단계", "GameScene", "Phase", "다음 라운드를 준비할 때", max: 1),
            S("Game.Phase.MatchOver", "매치 종료", "게임 진행", "GameScene", "Phase", "매치가 완전히 끝났을 때", max: 1),
            S("Game.Phase.MatchOverAction", "최종 순위를 확인하세요.", "게임 진행", "GameScene", "PhaseAction", "매치 종료 화면", max: 1),
            S("Game.Action.Waiting.Hotseat", "게임 시작을 기다려 주세요.", "단계 안내", "GameScene", "PhaseAction", "핫시트 게임 시작 대기", max: 1),
            S("Game.Action.Waiting.Network", "모든 플레이어의 참가를 기다리고 있습니다.", "단계 안내", "GameScene", "PhaseAction", "Fusion 플레이어 참가 대기", max: 1),
            S("Game.Action.RoleAssignment", "배정된 역할을 확인해 주세요.", "단계 안내", "GameScene", "PhaseAction", "역할 공개 단계", max: 1),
            S("Game.Action.Bribe.Hotseat", "{0}는 딜러에게 줄 뇌물을 결정하세요.", "단계 안내", "GameScene", "PhaseAction", "핫시트 뇌물 선택", "{0}=현재 직군", "플레이어 A는 딜러에게 줄 뇌물을 결정하세요.", 1),
            S("Game.Action.Bribe.Local", "딜러에게 줄 뇌물을 결정하세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 내가 뇌물을 결정할 때", max: 1),
            S("Game.Action.OtherDecision", "다른 플레이어의 결정을 기다려 주세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 다른 플레이어의 입력을 기다릴 때", max: 1),
            S("Game.Action.Bet.Hotseat", "{0}는 카드를 확인하고 판돈을 결정하세요.", "단계 안내", "GameScene", "PhaseAction", "핫시트 판돈 선택", "{0}=현재 직군", "플레이어 B는 카드를 확인하고 판돈을 결정하세요.", 1),
            S("Game.Action.Bet.Local", "카드를 확인하고 판돈을 결정하세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 내가 판돈을 결정할 때", max: 1),
            S("Game.Action.Bet.Other", "다른 플레이어의 판돈 결정을 기다려 주세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 다른 플레이어의 판돈을 기다릴 때", max: 1),
            S("Game.Action.ResultGeneration", "게임 결과를 생성하고 있습니다.", "단계 안내", "GameScene", "PhaseAction", "시스템 결과 생성", max: 1),
            S("Game.Action.CardCandidates", "딜러의 카드 후보를 준비하고 있습니다.", "단계 안내", "GameScene", "PhaseAction", "딜러 후보 카드 생성", max: 1),
            S("Game.Action.Dealer.Local", "딜러는 배분할 카드를 선택하세요.", "단계 안내", "GameScene", "PhaseAction", "핫시트 딜러 카드 배분", max: 1),
            S("Game.Action.Dealer.NetworkLocal", "배분할 카드를 선택하세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 내가 딜러일 때", max: 1),
            S("Game.Action.Dealer.Other", "딜러의 카드 배분을 기다려 주세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 딜러의 카드 배분을 기다릴 때", max: 1),
            S("Game.Action.Player.Hotseat", "{0}는 히트 또는 스탠드를 선택하세요.", "단계 안내", "GameScene", "PhaseAction", "핫시트 플레이어 행동 선택", "{0}=현재 직군", "플레이어 A는 히트 또는 스탠드를 선택하세요.", 1),
            S("Game.Action.Player.Local", "히트 또는 스탠드를 선택하세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 내 행동 차례", max: 1),
            S("Game.Action.Player.Other", "{0}의 결정을 기다려 주세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 다른 플레이어의 행동을 기다릴 때", "{0}=행동 차례의 직군", "플레이어 B의 결정을 기다려 주세요.", 1),
            S("Game.Action.ResultCalculation", "카드 결과를 계산하고 있습니다.", "단계 안내", "GameScene", "PhaseAction", "블랙잭 결과 계산", max: 1),
            S("Game.Action.DealerDecision.Local", "딜러는 결과를 확인하고 결정하세요.", "단계 안내", "GameScene", "PhaseAction", "핫시트 딜러 결정", max: 1),
            S("Game.Action.DealerDecision.NetworkLocal", "결과를 확인하고 결정을 내려 주세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 내가 딜러일 때의 결정", max: 1),
            S("Game.Action.DealerDecision.Other", "딜러의 결정을 기다려 주세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 딜러 결정을 기다릴 때", max: 1),
            S("Game.Action.ResultReveal", "공개된 라운드 결과를 확인하세요.", "단계 안내", "GameScene", "PhaseAction", "겉보기 결과 공개", max: 1),
            S("Game.Action.Accusation.Hotseat", "패배한 플레이어는 고발 여부를 결정하세요.", "단계 안내", "GameScene", "PhaseAction", "핫시트 고발 선택", max: 1),
            S("Game.Action.Accusation.Local", "고발 여부를 결정하세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 내가 고발할 수 있을 때", max: 1),
            S("Game.Action.Accusation.Other", "패배한 플레이어의 결정을 기다려 주세요.", "단계 안내", "GameScene", "PhaseAction", "Fusion에서 다른 플레이어의 고발 결정을 기다릴 때", max: 1),
            S("Game.Action.FinalJudgment", "최종 판정 결과를 확인하세요.", "단계 안내", "GameScene", "PhaseAction", "최종 판정 공개", max: 1),
            S("Game.Action.TieRedeal.Hotseat", "카드를 다시 배분하고 있습니다.", "단계 안내", "GameScene", "PhaseAction", "핫시트 무승부 재배분", max: 1),
            S("Game.Action.TieRedeal.Network", "무승부입니다. 모두 확인하면 카드를 다시 나눕니다.", "단계 안내", "GameScene", "PhaseAction", "Fusion 무승부 확인 대기", max: 2),
            S("Game.Action.RoundEnd", "다음 라운드를 준비해 주세요.", "단계 안내", "GameScene", "PhaseAction", "라운드 종료", max: 1),
            S("Game.Round.Counter", "라운드 {0} / {1}", "게임 진행", "GameScene", "Round", "진행 중인 라운드를 표시할 때", "{0}=현재 라운드, {1}=최대 라운드", "라운드 4 / 9", 1),
            S("Game.Bribe.Title", "{0} · 뇌물 결정", "뇌물", "GameScene", "BribePanel/Title", "해당 역할의 뇌물 결정 차례", "{0}=직군명", "플레이어 A · 뇌물 결정", 1),
            S("Game.Bribe.LocalTitle", "{0} (나) · 뇌물 결정", "뇌물", "GameScene", "BribePanel/Title", "Fusion에서 내 뇌물 결정 차례", "{0}=직군명", "플레이어 A (나) · 뇌물 결정", 1),
            S("Game.Bet.Title", "{0} · 판돈 결정", "판돈", "GameScene", "BetPanel/Title", "해당 역할의 판돈 결정 차례", "{0}=직군명", "플레이어 A · 판돈 결정", 1),
            S("Game.Bet.LocalTitle", "{0} (나) · 판돈 결정", "판돈", "GameScene", "BetPanel/Title", "Fusion에서 내 판돈 결정 차례", "{0}=직군명", "플레이어 A (나) · 판돈 결정", 1),
            S("Game.Dealer.Selection", "{0}에게 줄 카드", "카드 배분", "GameScene", "DealerPanel/Selection", "딜러가 카드 수령자를 선택할 때", "{0}=직군명", "플레이어 B에게 줄 카드", 1),
            S("Game.Dealer.Score", "{0}의 점수 합계: <b>{1}</b>", "카드 배분", "GameScene", "DealerPanel/Scores", "딜러가 후보 카드를 확인할 때", "{0}=직군명, {1}=점수", "플레이어 A의 점수 합계: 17", 1),
            S("Game.Decision.Turn", "<b>{0}의 차례</b>\n<size=17><color={1}>행동을 선택하세요.</color></size>", "행동 선택", "GameScene", "DecisionPanel/Hand", "플레이어의 히트·스탠드 결정 차례", "{0}=직군명, {1}=부제색", "플레이어 A의 차례 / 행동을 선택하세요.", 2),
            S("Game.Decision.Hit.Title", "히트", "행동 선택", "GameScene", "DecisionHint", "히트 버튼에 마우스를 올렸을 때", max: 1),
            S("Game.Decision.Hit.Body", "카드를 한 장 더 받습니다.", "행동 선택", "GameScene", "DecisionHint", "히트 버튼에 마우스를 올렸을 때", max: 1),
            S("Game.Decision.Stand.Title", "스탠드", "행동 선택", "GameScene", "DecisionHint", "스탠드 버튼에 마우스를 올렸을 때", max: 1),
            S("Game.Decision.Stand.Body", "카드를 더 받지 않고\n턴을 종료합니다.", "행동 선택", "GameScene", "DecisionHint", "스탠드 버튼에 마우스를 올렸을 때", max: 2),
            S("Game.Decision.DoubleDown.Title", "더블다운", "행동 선택", "GameScene", "DecisionHint", "더블다운 버튼에 마우스를 올렸을 때", max: 1),
            S("Game.Decision.DoubleDown.Body", "판돈을 2배로 올리고\n카드 한 장만 받고 멈춥니다.", "행동 선택", "GameScene", "DecisionHint", "더블다운 버튼에 마우스를 올렸을 때", max: 2),
            S("Game.Decision.Die.Title", "다이", "행동 선택", "GameScene", "DecisionHint", "다이 버튼에 마우스를 올렸을 때", max: 1),
            S("Game.Decision.Die.Body", "포기하는 대신\n판돈의 절반만 잃습니다.", "행동 선택", "GameScene", "DecisionHint", "다이 버튼에 마우스를 올렸을 때", max: 2),
            S("Game.Decision.HintFormat", "<b>{0}</b>\n<size={1}><color=#{2}>{3}</color></size>", "행동 선택", "GameScene", "DecisionHint", "행동 버튼에 마우스를 올렸을 때의 전체 구성", "{0}=제목, {1}=글자 크기, {2}=색상, {3}=설명", max: 2),
            S("Game.Action.Deal", "카드 배분", "행동 알림", "GameScene", "Toast", "딜러가 카드를 배분했을 때", max: 1),
            S("Game.Action.SurrenderFollowUp", "매치를 종료합니다.", "행동 알림", "GameScene", "Toast", "누군가 항복했을 때의 두 번째 줄", max: 1),
            S("Game.Result.Tie", "무승부! 서로의 점수가 같습니다", "결과", "GameScene", "Result/Outcome", "두 플레이어의 결과가 동점일 때", max: 2),
            S("Game.Accusation.Headline", "딜러를 믿으시겠습니까?", "고발", "GameScene", "Accusation/Headline", "패배 플레이어가 고발 여부를 결정할 때", max: 1),
            S("Game.Accusation.Subline", "선택한 뒤에 진실이 밝혀집니다.", "고발", "GameScene", "Accusation/Subline", "고발 선택 화면", max: 1),
            S("Game.MatchOver.Title", "매치 종료", "최종 결과", "GameScene", "MatchOver/Title", "매치 종료 카드", max: 1),
            S("Game.Button.Continue", "계속", "버튼", "GameScene", "ContinueButton", "일반 결과를 확인한 뒤", max: 1),
            S("Game.Button.NextRound", "다음 라운드", "버튼", "GameScene", "NextRoundButton", "다음 라운드로 진행 가능할 때", max: 1),
            S("Game.Button.FinalResult", "최종 결과 보기", "버튼", "GameScene", "NextRoundButton", "파산 등으로 매치가 종료될 때", max: 1),
            S("Game.Button.Waiting", "기다리는 중  {0}/{1}", "버튼", "GameScene", "ReadyButton", "내 준비는 끝났고 다른 플레이어를 기다릴 때", "{0}=준비 수, {1}=전체 수", "기다리는 중  2/3", 1),

            S("Common.None", "-", "공통", "전체", "빈 값", "표시할 결과가 없을 때", max: 1),
            S("Common.None.Parenthesized", "(없음)", "공통", "전체", "빈 목록", "카드나 항목이 없을 때", max: 1),
            S("Common.Continue", "계속", "버튼", "GameScene", "ContinueButton", "결과 확인 후 계속할 때", max: 1),
            S("Common.LocalPlayer", "나", "공통", "GameScene", "FinalRanking/LocalTag", "내 순위 카드", max: 1),
            S("Game.Bribe.StatusTitle", "딜러에게 제출한 뇌물", "뇌물", "GameScene", "BribeStatus/Title", "제출 뇌물 현황을 표시할 때", max: 1),
            S("Game.Bribe.Summary", "<color={2}>뇌물</color>  <b>{0:N0}</b>     <color={2}>뇌물 후 잔액</color>  <b>{1:N0}</b>", "뇌물", "GameScene", "BribePanel/Summary", "뇌물 액수를 조절할 때", "{0}=뇌물, {1}=남은 잔액, {2}=강조색", "뇌물 100 / 뇌물 후 잔액 900", 1),
            S("Game.Bet.Summary", "<color={3}>판돈</color>  <b>{0:N0}</b>     <color={3}>제출한 뇌물</color>  <b>{1:N0}</b>     <color={3}>남은 잔액</color>  <b>{2:N0}</b>", "판돈", "GameScene", "BetPanel/Summary", "판돈을 조절할 때", "{0}=판돈, {1}=뇌물, {2}=잔액, {3}=강조색", "판돈 100 / 제출한 뇌물 50 / 남은 잔액 850", 1),
            S("Game.Bet.Total", "총 베팅", "판돈", "GameScene", "BetTotal/Label", "두 플레이어의 총 판돈 표시", max: 1),
            S("Game.Cover.PrivateTurn", "{0}의 차례\n다른 플레이어는 화면을 보지 마세요!", "화면 전환", "GameScene", "Cover", "핫시트에서 화면을 다음 사람에게 넘길 때", "{0}=직군명", "플레이어 A의 차례 / 다른 플레이어는 화면을 보지 마세요!", 2),
            S("Game.Cover.BetTurn", "{0}의 차례\n카드를 확인하고 판돈을 선택하세요.", "화면 전환", "GameScene", "Cover", "핫시트 판돈 선택 직전", "{0}=직군명", max: 2),
            S("Game.Cover.DealerCards", "딜러의 차례\n카드 후보를 선택하세요", "화면 전환", "GameScene", "Cover", "딜러의 최초 카드 배분", max: 2),
            S("Game.Cover.DealerHit", "딜러의 차례\n히트 카드 후보를 선택하세요", "화면 전환", "GameScene", "Cover", "플레이어 히트 후 딜러 배분", max: 2),
            S("Game.Cover.DecisionTurn", "{0}의 차례\n행동을 선택해 주세요.", "화면 전환", "GameScene", "Cover", "플레이어 행동 선택 직전", "{0}=직군명", max: 2),
            S("Game.Cover.AccusationTurn", "{0}의 차례\n딜러 고발 여부를 결정하세요", "화면 전환", "GameScene", "Cover", "패배 플레이어의 고발 선택 직전", "{0}=직군명", max: 2),
            S("Game.Cover.ContinueDetail", "화면을 확인한 뒤 계속해 주세요.", "화면 전환", "GameScene", "Cover/Detail", "핫시트 화면 전환 공통 안내", max: 1),
            S("Game.Result.Round.Winner", "{0} 승리!", "결과", "GameScene", "Result/Outcome", "일반 라운드 승리", "{0}=승리 직군", "플레이어 B 승리!", 1),
            S("Game.Result.Redeal", "카드 다시 받기", "결과", "GameScene", "ContinueButton", "무승부 결과 확인 후", max: 1),
            S("Game.Result.NextRound", "다음 라운드", "결과", "GameScene", "NextRoundButton", "다음 라운드 진행 가능", max: 1),
            S("Game.Result.ViewFinal", "최종 결과 보기", "결과", "GameScene", "NextRoundButton", "파산으로 매치가 종료됐을 때", max: 1),
            S("Game.Result.ToAccusation", "고발 단계로", "결과", "GameScene", "ContinueButton", "Fusion에서 결과 확인 준비", max: 1),
            S("Game.Result.Final.Winner", "{0} 최종 승리!", "최종 판정", "GameScene", "FinalResult/Winner", "고발과 딜러 배신을 반영한 최종 승자", "{0}=승리 직군", "딜러 최종 승리!", 1),
            S("Game.Result.Final.Default", "최종 결과", "최종 판정", "GameScene", "FinalResult/Winner", "최종 승자가 특정되지 않았을 때", max: 1),
            S("Game.Result.Description.Tie", "승자가 없습니다. 뇌물과 조작 기록은 그대로 두고 카드만 다시 나눕니다.", "결과", "GameScene", "Result/Description", "무승부 결과 설명", max: 2),
            S("Game.Result.Description.Decided", "공개된 카드 점수로 이번 라운드의 승자가 결정되었습니다.", "결과", "GameScene", "Result/Description", "승패가 결정된 결과 설명", max: 2),
            S("Game.Result.Description.Die", "{0}가 다이했습니다.", "결과", "GameScene", "Result/Description", "한 플레이어가 다이해 라운드 승자가 정해졌을 때", "{0}=다이한 직군", "플레이어 A가 다이했습니다.", 1),
            S("Game.MatchOver.BankruptReason", "{0} 파산으로 끝났습니다.", "최종 결과", "GameScene", "MatchOver/Reason", "파산으로 매치 종료", "{0}=캐릭터명", max: 1),
            S("Game.MatchOver.AllRoundsReason", "{0}라운드를 모두 마쳤습니다.", "최종 결과", "GameScene", "MatchOver/Reason", "최대 라운드를 모두 마침", "{0}=최대 라운드", max: 1),
            S("Game.MatchOver.Countdown", "<size=14><color={0}>{1}초 후 준비하지 않은 사람은 방에서 나갑니다</color></size>", "최종 결과", "GameScene", "MatchOver/Reason", "Fusion 재대결 준비 제한시간", "{0}=색상, {1}=초", max: 1),
            S("Game.MatchOver.Rematch", "다시 플레이", "최종 결과", "GameScene", "RematchButton", "매치 종료 후 재대결", max: 1),
            S("Game.Ready.WaitingCount", "기다리는 중  {0}/{1}", "버튼", "GameScene", "ReadyButton", "내 준비 완료 후 다른 사람을 기다릴 때", "{0}=준비 수, {1}=전체 수", max: 1),
            S("Game.Money.Balance", "잔액", "머니바", "GameScene", "MoneyCard/Role", "직군이 없는 잔액 카드", max: 1),
            S("Game.PlateAction.Bribe", "선택하는 중...", "Plate", "GameScene/LobbyScene", "Plate/Action", "뇌물 선택 중", max: 1),
            S("Game.PlateAction.Bet", "베팅하는 중...", "Plate", "GameScene/LobbyScene", "Plate/Action", "판돈 선택 중", max: 1),
            S("Game.PlateAction.Deal", "배분하는 중...", "Plate", "GameScene/LobbyScene", "Plate/Action", "딜러 카드 배분 중", max: 1),
            S("Game.PlateAction.Decision", "고민하는 중...", "Plate", "GameScene/LobbyScene", "Plate/Action", "행동 선택 중", max: 1),
            S("Game.PlateAction.Accusation", "고발하는 중...", "Plate", "GameScene/LobbyScene", "Plate/Action", "고발 선택 중", max: 1),
            S("Game.Score.Bust", "버스트", "점수", "GameScene", "ScoreCard", "점수가 21을 넘었을 때", max: 1),
            S("Game.Score.Soft", "소프트", "점수", "GameScene", "ScoreCard", "에이스를 11로 계산한 점수", max: 1),
            S("Game.Score.Hard", "하드", "점수", "GameScene", "ScoreCard", "일반 점수", max: 1),
            S("Game.Score.HiddenOpponent", "<size=15><color={2}>{0} 점수</color></size>\n<size=34><b>{1} + ?</b></size>\n<size=14><color={3}>상대 패</color></size>", "점수", "GameScene", "ScoreCard", "상대의 덮인 패를 표시할 때", "{0}=직군, {1}=공개 카드 점수, {2}=부제색, {3}=흐린 글자색", max: 3),
            S("Game.Score.Full", "<size=15><color={4}>{0} 점수</color></size>\n<size=38><b>{1}</b></size>\n<size=15><color={2}><b>{3}</b></color></size>", "점수", "GameScene", "ScoreCard", "전체 점수를 공개할 때", "{0}=직군, {1}=점수, {2}=점수 상태색, {3}=점수 종류, {4}=부제색", max: 3),
            S("Game.Dealer.Candidate.Fair", "공정 후보", "카드 배분", "GameScene", "DealerCandidate/Label", "공정 후보 카드", max: 1),
            S("Game.Dealer.Candidate.Cheated", "조작 후보", "카드 배분", "GameScene", "DealerCandidate/Label", "조작 후보 카드", max: 1),
            S("Game.Accusation.Accuse", "고발", "고발", "GameScene", "Accusation/Card", "고발 선택지 이름", max: 1),
            S("Game.Accusation.Accept", "승복", "고발", "GameScene", "Accusation/Card", "승복 선택지 이름", max: 1),
            S("Game.Accusation.Tag.Reversal", "승패 역전", "고발", "GameScene", "Accusation/Tag", "고발 성공 효과", max: 1),
            S("Game.Accusation.Tag.Deposit", "보증금 {0} <sprite index=0>", "고발", "GameScene", "Accusation/Tag", "고발 비용", "{0}=보증금", max: 1),
            S("Game.Accusation.Tag.Safe", "위험 없음", "고발", "GameScene", "Accusation/Tag", "승복 효과", max: 1),
            S("Game.Accusation.AccuseBody", "조작이 있었다면 <b>승패가 뒤집히고</b>\n딜러에게 벌금 {0} <sprite index=0>을 받습니다.\n아니라면 무고 벌금 {1} <sprite index=0>을 뭅니다.", "고발", "GameScene", "Accusation/Card", "고발 선택지 설명", "{0}=딜러 벌금, {1}=무고 벌금", max: 3),
            S("Game.Accusation.AcceptBody", "패배를 그대로 받아들입니다.\n더 잃지도, 더 얻지도 않습니다.", "고발", "GameScene", "Accusation/Card", "승복 선택지 설명", max: 2),
            S("Game.DoubleDown.NoticeTitle", "{0} 더블다운!", "행동 알림", "GameScene", "Toast", "더블다운 승인", "{0}=직군명", max: 1),
            S("Game.DoubleDown.NoticeBody", "건 돈 {0} → {1} (2배)\n{2} 카드를 한 장만 더 받고 멈춥니다.", "행동 알림", "GameScene", "Toast", "더블다운 결과 설명", "{0}=기존 판돈, {1}=새 판돈, {2}=직군명", max: 2),
            S("Game.Action.SurrenderNotice", "{0}가 항복했습니다.", "행동 알림", "GameScene", "Toast/MatchOver", "누군가 항복했을 때", "{0}=직군명", max: 1),
            S("Game.Judgment.Accepted", "{0}가 결과에 승복했습니다.", "최종 판정", "GameScene", "FinalResult/Reason", "고발하지 않고 결과를 받아들였을 때", "{0}=승복한 직군", "플레이어 B가 결과에 승복했습니다.", 1),
            S("Game.Judgment.AccusationSuccess", "고발 성공: 딜러의 카드 조작이 확인되었습니다.", "최종 판정", "GameScene", "FinalResult/Reason", "고발 판정 원본의 성공 사유", max: 1),
            S("Game.Judgment.AccusationFailed", "고발 실패: 조작 기록 없음. 고발자에게 페널티가 부여됩니다.", "최종 판정", "GameScene", "FinalResult/Reason", "고발 판정 원본의 실패 사유", max: 2),
            S("Game.Judgment.AccuseSuccessAfterDie", "고발 성공! 다이는 무효가 되고 딜러의 조작이 확인되었습니다. {0}는 벌금 {1}을 받고 승패를 뒤집었습니다.", "최종 판정", "GameScene", "FinalResult/Reason", "다이 뒤 고발이 성공했을 때", "{0}=고발한 직군, {1}=딜러 벌금", "플레이어 A는 벌금 150을 받고 승패를 뒤집었습니다.", 2),
            S("Game.Judgment.AccuseSuccess", "고발 성공! 딜러의 조작이 확인되었습니다. {0}는 벌금 {1}을 받고 승패를 뒤집었습니다.", "최종 판정", "GameScene", "FinalResult/Reason", "일반 고발이 성공했을 때", "{0}=고발한 직군, {1}=딜러 벌금", "플레이어 B는 벌금 150을 받고 승패를 뒤집었습니다.", 2),
            S("Game.Judgment.AccuseFailed", "고발 실패! 조작이 없었습니다. {0}는 무고 벌금 {1}을 냈습니다.", "최종 판정", "GameScene", "FinalResult/Reason", "고발이 실패했을 때", "{0}=고발한 직군, {1}=무고 벌금", "플레이어 A는 무고 벌금 100을 냈습니다.", 2),
            S("Game.Judgment.DealerSoloWin", "딜러 단독 승리! 더 많은 뇌물을 낸 쪽을 배신하고 판을 통째로 가져갔습니다.", "최종 판정", "GameScene", "FinalResult/Reason", "딜러 배신 조건으로 단독 승리", max: 2),
            S("Game.Judgment.DieAccepted", "{0}가 다이한 결과에 승복했습니다. 판돈의 절반만 잃습니다.", "최종 판정", "GameScene", "FinalResult/Reason", "다이 뒤 고발하지 않고 결과에 승복했을 때", "{0}=다이하고 승복한 직군", "플레이어 A가 다이한 결과에 승복했습니다. 판돈의 절반만 잃습니다.", 2),

            S("History.Round.Title", "라운드 {0}", "기록", "GameScene/LobbyScene", "History/Round/Title", "라운드 기록 한 줄", "{0}=라운드 번호", "라운드 3", 1),
            S("History.Manipulation.Fair", "조작 여부:  <b>공정</b>", "기록", "GameScene/LobbyScene", "History/Round/Manipulation", "딜러가 조작하지 않은 라운드", max: 1),
            S("History.Manipulation.Cheated", "조작 여부:  <b>조작함</b>", "기록", "GameScene/LobbyScene", "History/Round/Manipulation", "딜러가 조작한 라운드", max: 1),
            S("History.Accusation.Label", "고발 여부:  <b>{0}</b>", "기록", "GameScene/LobbyScene", "History/Round/Accusation", "라운드의 고발 결과", "{0}=고발 결과", "고발 여부: 고발 · 적중", 1),
            S("History.Rank.Title", "{0}번째 게임", "기록", "GameScene/LobbyScene", "History/Rank/Title", "완료된 매치 기록 한 줄", "{0}=매치 번호", "2번째 게임", 1),
            S("History.Result.Win", "승리", "기록", "GameScene/LobbyScene", "History/Rank/Card", "해당 매치의 1위", max: 1),
            S("History.Result.Bankrupt", "파산", "기록", "GameScene/LobbyScene", "History/Rank/Card", "파산으로 종료한 플레이어", max: 1),
            S("History.Result.Surrender", "항복", "기록", "GameScene/LobbyScene", "History/Rank/Card", "항복한 플레이어", max: 1),
            S("History.Bribe.DealerKept", "뇌물 챙김 {0}", "기록", "GameScene/LobbyScene", "History/Round/Bribe", "딜러가 뇌물을 실제로 챙겼을 때", "{0}=금액", max: 1),
            S("History.Bribe.DealerNone", "뇌물 챙김 없음", "기록", "GameScene/LobbyScene", "History/Round/Bribe", "딜러가 챙긴 뇌물이 없을 때", max: 1),
            S("History.Bribe.None", "뇌물 없음", "기록", "GameScene/LobbyScene", "History/Round/Bribe", "플레이어가 뇌물을 내지 않았을 때", max: 1),
            S("History.Bribe.Paid", "뇌물 {0}", "기록", "GameScene/LobbyScene", "History/Round/Bribe", "제출한 뇌물이 딜러에게 넘어갔을 때", "{0}=금액", max: 1),
            S("History.Bribe.Returned", "뇌물 {0} · 반납", "기록", "GameScene/LobbyScene", "History/Round/Bribe", "제출한 뇌물을 돌려받았을 때", "{0}=금액", max: 1),
            S("History.Accusation.Success", "고발 · 적중", "기록", "GameScene/LobbyScene", "History/Round/Accusation", "고발이 적중했을 때", max: 1),
            S("History.Accusation.Failed", "고발 · 실패", "기록", "GameScene/LobbyScene", "History/Round/Accusation", "고발이 실패했을 때", max: 1),
            S("History.Accusation.Accused", "고발", "기록", "GameScene/LobbyScene", "History/Round/Accusation", "고발 결과가 아직 없을 때", max: 1),
            S("History.Accusation.Accepted", "승복", "기록", "GameScene/LobbyScene", "History/Round/Accusation", "고발하지 않고 결과를 받아들였을 때", max: 1),
            S("History.Rank.PositionRole", "{0}위 · {1}", "기록", "GameScene/LobbyScene", "History/Rank/Card", "랭킹 카드의 등수와 직군", "{0}=등수, {1}=직군명", "1위 · 딜러", 1),
            S("History.Rank.ResultSuffix", " · {0}", "기록", "GameScene/LobbyScene", "History/Rank/Card", "랭킹 카드 뒤의 승리·파산·항복 표시", "{0}=결과", " · 승리", 1),

            S("Chat.Placeholder.Ready", "Enter로 보내기 · Esc로 취소", "채팅", "GameScene/LobbyScene", "Chat/Input/Placeholder", "채팅 연결이 가능한 상태", max: 1),
            S("Chat.Placeholder.Offline", "연결 전 — 나만 보입니다", "채팅", "GameScene/LobbyScene", "Chat/Input/Placeholder", "네트워크 연결 전", max: 1),
            S("Chat.Speaker.Unknown", "누군가", "채팅", "GameScene/LobbyScene", "Chat/Speaker", "보낸 사람 이름이 없을 때", max: 1),
            S("Chat.Speaker.LocalOnly", "나 (혼잣말)", "채팅", "GameScene/LobbyScene", "Chat/Speaker", "네트워크 연결 전 로컬 채팅", max: 1),
            S("Chat.Speaker.Slot", "플레이어 {0}", "채팅", "GameScene/LobbyScene", "Chat/Speaker", "네트워크 닉네임이 없을 때", "{0}=플레이어 슬롯", max: 1),
            S("Chat.Speaker.System", "[시스템]", "채팅", "GameScene/LobbyScene", "Chat/Speaker", "로컬 시스템 안내의 발신자", max: 1),
            S("Chat.Guide.RoomEntered", "\"{0}\" 로비에 입장하였습니다.", "채팅", "LobbyScene", "PanelRoom/ZJ_Chat", "방 로비에 입장한 직후 각 클라이언트에 표시", "{0}=방 이름", "\"주말방\" 로비에 입장하였습니다.", 1),
            S("Chat.Guide.GameStarted", "게임이 시작되었습니다.", "채팅", "GameScene", "ZJ_Chat", "게임 씬이 시작된 직후 각 클라이언트에 표시", max: 1),
            S("Chat.Guide.GameEnded", "게임이 종료되었습니다.", "채팅", "GameScene", "ZJ_Chat", "매치 종료 화면이 처음 표시될 때 각 클라이언트에 한 번 표시", max: 1),
            S("Settings.Title", "환경 설정", "환경설정", "GameScene/LobbyScene", "Settings/Title", "환경 설정 창 제목", max: 1),
            S("Settings.Close", "닫기", "환경설정", "GameScene/LobbyScene", "Settings/CloseButton", "환경설정 창을 닫을 때", max: 1),
            S("Settings.Surrender", "항복", "환경설정", "GameScene", "Settings/SurrenderButton", "진행 중인 게임에서 항복할 때", max: 1),
            S("Settings.Fullscreen", "전체 화면", "환경설정", "GameScene/LobbyScene", "Settings/WindowMode", "화면 모드 선택지", max: 1),
            S("Settings.Windowed", "창 모드", "환경설정", "GameScene/LobbyScene", "Settings/WindowMode", "화면 모드 선택지", max: 1),
            S("Settings.Percent", "{0}%", "환경설정", "GameScene/LobbyScene", "Settings/VolumeValue", "음량 값 표시", "{0}=0~100 정수", "75%", 1)
        };

        [MenuItem("ZooJack/텍스트/카탈로그 만들기·업데이트")]
        public static void Build()
        {
            EnsureFolder("Assets", "Localization");
            EnsureFolder("Assets/Localization", "ZooJack");

            LocalizationSettings settings = EnsureSettings();
            Locale korean = EnsureKoreanLocale();
            LocalizationSettings.ProjectLocale = korean;
            LocalizationSettings.InitializeSynchronously = true;
            EditorUtility.SetDirty(settings);

            StringTableCollection collection =
                LocalizationEditorSettings.GetStringTableCollection(ZooJackText.TableName)
                ?? LocalizationEditorSettings.CreateStringTableCollection(
                    ZooJackText.TableName, Root, new List<Locale> { korean });

            var table = collection.GetTable(korean.Identifier) as StringTable;
            if (table == null)
            {
                Debug.LogError("[ZooJackText] 한국어 String Table을 만들지 못했습니다.");
                return;
            }

            int added = 0;
            foreach (Seed seed in Seeds)
            {
                StringTableEntry entry = table.GetEntry(seed.Key);
                if (entry == null)
                {
                    entry = table.AddEntry(seed.Key, seed.Text);
                    added++;
                }

                var shared = collection.SharedData.GetEntry(seed.Key);
                if (shared == null) continue;

                var context = shared.Metadata.GetMetadata<ZooJackTextContext>();
                if (context == null)
                {
                    context = new ZooJackTextContext();
                    shared.Metadata.AddMetadata(context);
                }

                FillBlank(context, seed);

                var comment = shared.Metadata.GetMetadata<Comment>();
                if (comment == null)
                {
                    comment = new Comment();
                    shared.Metadata.AddMetadata(comment);
                }
                if (string.IsNullOrWhiteSpace(comment.CommentText))
                    comment.CommentText = $"{seed.Scene} / {seed.Target} — {seed.Trigger}";
            }

            EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ZooJackText] 카탈로그 준비 완료. 새 항목 {added}개, 전체 {collection.SharedData.Entries.Count}개.");
        }

        private static LocalizationSettings EnsureSettings()
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings != null) return settings;

            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "ZooJackLocalizationSettings";
            AssetDatabase.CreateAsset(settings, SettingsPath);
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
            return settings;
        }

        private static Locale EnsureKoreanLocale()
        {
            Locale locale = LocalizationEditorSettings.GetLocale("ko");
            if (locale != null) return locale;

            locale = Locale.CreateLocale(new LocaleIdentifier("ko"));
            locale.name = "Korean (ko)";
            AssetDatabase.CreateAsset(locale, LocalePath);
            LocalizationEditorSettings.AddLocale(locale);
            return locale;
        }

        private static void FillBlank(ZooJackTextContext context, Seed seed)
        {
            if (string.IsNullOrWhiteSpace(context.Category)) context.Category = seed.Category;
            if (string.IsNullOrWhiteSpace(context.Scene)) context.Scene = seed.Scene;
            if (string.IsNullOrWhiteSpace(context.Target)) context.Target = seed.Target;
            if (string.IsNullOrWhiteSpace(context.Trigger)) context.Trigger = seed.Trigger;
            if (string.IsNullOrWhiteSpace(context.Variables)) context.Variables = seed.Variables;
            if (string.IsNullOrWhiteSpace(context.Preview)) context.Preview = seed.Preview;
            if (context.MaxLines <= 0) context.MaxLines = seed.MaxLines;
        }

        private static Seed S(string key, string text, string category, string scene, string target,
            string trigger, string variables = "", string preview = "", int max = 0) =>
            new Seed(key, text, category, scene, target, trigger, variables, preview, max);

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
