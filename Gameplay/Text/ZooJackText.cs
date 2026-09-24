using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization.Settings;

namespace ZooJack
{
    /// <summary>
    /// ZooJack의 사용자 노출 문구를 String Table에서 읽는 단일 진입점.
    /// 테이블이나 키가 아직 준비되지 않았을 때는 코드의 fallback을 사용하므로 단계적 이전이 가능하다.
    /// </summary>
    public static class ZooJackText
    {
        public const string TableName = "ZooJack";

        private static readonly HashSet<string> ReportedFailures = new HashSet<string>();

        public static string Get(string key, string fallback, params object[] arguments)
        {
            string safeFallback = FormatFallback(fallback, arguments);
            if (string.IsNullOrWhiteSpace(key) || !LocalizationSettings.HasSettings)
                return safeFallback;

            try
            {
                var args = arguments == null
                    ? (IList<object>)Array.Empty<object>()
                    : arguments;
                string localized = LocalizationSettings.StringDatabase.GetLocalizedString(
                    TableName, key, args);

                return string.IsNullOrEmpty(localized) ? safeFallback : localized;
            }
            catch (Exception exception)
            {
                if (ReportedFailures.Add(key))
                    Debug.LogWarning($"[ZooJackText] '{key}' 문구를 읽지 못해 기본 문구를 사용합니다. {exception.Message}");
                return safeFallback;
            }
        }

        public static string RoleName(PlayerRole role) => role switch
        {
            PlayerRole.PlayerA => Get("Common.Role.PlayerA", "플레이어 A"),
            PlayerRole.PlayerB => Get("Common.Role.PlayerB", "플레이어 B"),
            PlayerRole.Dealer => Get("Common.Role.Dealer", "딜러"),
            _ => Get("Common.Role.Spectator", "관전자")
        };

        public static string CharacterName(CharacterId character) => character switch
        {
            CharacterId.Rabbit => Get("Common.Character.Rabbit", "토끼"),
            CharacterId.Fox => Get("Common.Character.Fox", "여우"),
            CharacterId.Croc => Get("Common.Character.Croc", "악어"),
            _ => string.Empty
        };

        public static string RoundWinner(PlayerRole role) =>
            Get("Game.Result.Round.Winner", "{0} 승리!", RoleName(role));

        public static string FinalWinner(FinalWinner winner) => winner switch
        {
            global::ZooJack.FinalWinner.PlayerA => Get(
                "Game.Result.Final.Winner", "{0} 최종 승리!", RoleName(PlayerRole.PlayerA)),
            global::ZooJack.FinalWinner.PlayerB => Get(
                "Game.Result.Final.Winner", "{0} 최종 승리!", RoleName(PlayerRole.PlayerB)),
            global::ZooJack.FinalWinner.Dealer => Get(
                "Game.Result.Final.Winner", "{0} 최종 승리!", RoleName(PlayerRole.Dealer)),
            _ => Get("Game.Result.Final.Default", "최종 결과")
        };

        public static string PhaseName(GamePhase phase) => phase switch
        {
            GamePhase.WaitingForPlayers => Get("Game.Phase.Waiting", "대기 중"),
            GamePhase.RoleAssignment => Get("Game.Phase.RoleAssignment", "역할 배정"),
            GamePhase.BribeSelection => Get("Game.Phase.Bribe", "뇌물 선택"),
            GamePhase.BetSelection => Get("Game.Phase.Bet", "판돈 선택"),
            GamePhase.SystemResultGeneration => Get("Game.Phase.ResultGeneration", "결과 생성"),
            GamePhase.CardCandidateGeneration => Get("Game.Phase.CardCandidates", "카드 후보 생성"),
            GamePhase.DealerCardDistribution => Get("Game.Phase.DealerCards", "딜러 카드 선택"),
            GamePhase.PlayerDecision => Get("Game.Phase.PlayerDecision", "플레이어 결정"),
            GamePhase.BlackjackResultCalculation => Get("Game.Phase.ResultCalculation", "결과 계산"),
            GamePhase.DealerDecision => Get("Game.Phase.DealerDecision", "딜러 결정"),
            GamePhase.ResultReveal => Get("Game.Phase.ResultReveal", "결과 공개"),
            GamePhase.Accusation => Get("Game.Phase.Accusation", "고발"),
            GamePhase.FinalJudgment => Get("Game.Phase.FinalJudgment", "최종 판정"),
            GamePhase.TieRedeal => Get("Game.Phase.TieRedeal", "무승부 재배분"),
            GamePhase.RoundEnd => Get("Game.Phase.RoundEnd", "라운드 종료"),
            _ => phase.ToString()
        };

        private static string FormatFallback(string fallback, object[] arguments)
        {
            fallback ??= string.Empty;
            if (arguments == null || arguments.Length == 0) return fallback;

            try
            {
                return string.Format(CultureInfo.CurrentCulture, fallback, arguments);
            }
            catch (FormatException)
            {
                return fallback;
            }
        }
    }
}
