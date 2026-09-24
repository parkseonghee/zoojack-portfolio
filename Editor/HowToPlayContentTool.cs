using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZooJack.Editor
{
    /// <summary>
    /// 설명서(<see cref="HowToPlayPanel"/>)의 <b>내용</b>을 채운다. 책 모양을 만드는 것은
    /// <see cref="HowToPlaySetupTool"/>이고 여기는 쪽에 무엇이 적히는지만 정한다.
    ///
    /// <b>왜 도구인가.</b> 인스펙터에서 손으로 채우면 규칙이 바뀔 때마다 씬을 열어 고쳐야 하고,
    /// 무엇이 바뀌었는지 커밋에 남지도 않는다. 여기 두면 규칙과 설명이 같은 곳에서 함께 움직인다.
    ///
    /// <b>숫자는 <see cref="RoundSettlement"/>에서 읽는다.</b> 설명서에 숫자를 손으로 적으면
    /// 상수를 바꿨을 때 설명서만 옛말이 된다 — 규칙을 가장 크게 오해하게 만드는 종류의 거짓말이다.
    /// </summary>
    public static class HowToPlayContentTool
    {
        [MenuItem("ZooJack/게임/설명서 내용 채우기")]
        public static void Fill()
        {
            var panel = Object.FindFirstObjectByType<HowToPlayPanel>(FindObjectsInactive.Include);
            if (panel == null)
            {
                EditorUtility.DisplayDialog("설명서",
                    "씬에 설명서가 없습니다. 'ZooJack/게임/설명서 만들기'를 먼저 실행하세요.", "확인");
                return;
            }

            Apply(panel);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[HowToPlayContentTool] 설명서에 내용을 채웠습니다.");
        }

        /// <summary>책을 만든 직후에도 불린다. 빈 책이 남지 않게 하려는 것이다.</summary>
        public static void Apply(HowToPlayPanel panel)
        {
            if (panel == null) return;

            // 초상화는 GameDirector가 들고 있다. 로비의 설명서를 채울 때는 게임 씬이
            // 열려 있지 않으므로 잠깐 빌려 온다.
            Sprite rabbit = null, fox = null, croc = null;
            GameSceneLoan.While(() =>
            {
                var director = Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include);
                rabbit = Portrait(director, "portraitRabbit");
                fox = Portrait(director, "portraitFox");
                croc = Portrait(director, "portraitCroc");
            });

            Sprite coin = FindSprite("pieceYellow_border10");

            HowToPlayPage[] pages =
            {
                // ── 1면: 목표와 승패 ────────────────────────────────
                Page(
                    Heading("목표"),
                    Row(CoinRowHeight, "모든 플레이어는 " + N(RoundSettlement.SeedMoney) + "골드로 시작합니다.",
                        Pic(coin, 0.62f), Pic(coin, 0.8f), Pic(coin, 1f)),
                    Note(RoundSettlement.MaxRounds + "라운드 종료 시 보유 골드가 가장 많은 플레이어가 승리합니다.\n" +
                         "보유 골드가 " + RoundSettlement.MinBet + "골드 미만이면 파산하며, " +
                         "매치가 즉시 종료됩니다.")),

                Page(
                    Heading("승부"),
                    Row("카드 점수가 21에 더 가까운 플레이어가 승리합니다.",
                        Card(BlackjackCardRank.Ace, BlackjackCardSuit.Spades),
                        Sym("+", 0.7f),
                        Card(BlackjackCardRank.King, BlackjackCardSuit.Hearts)),
                    Row(120f, "카드 점수가 21을 넘으면 버스트로 패배합니다.",
                        Card(BlackjackCardRank.Ten, BlackjackCardSuit.Clubs),
                        Sym("+", 0.6f),
                        Card(BlackjackCardRank.Nine, BlackjackCardSuit.Diamonds),
                        Sym("+", 0.6f),
                        Card(BlackjackCardRank.Five, BlackjackCardSuit.Hearts))),

                // ── 2면: 세 역할 ────────────────────────────────────
                Page(
                    Heading("세 자리"),
                    Row("플레이어 A와 플레이어 B가 카드로 승부합니다.",
                        Pic(rabbit), Sym("↔", 0.7f), Pic(fox)),
                    Row("딜러는 두 플레이어에게 카드를 배분합니다.",
                        Pic(croc)),
                    Note("딜러는 판돈을 걸지 않습니다.")),

                Page(
                    Heading("역할 교대"),
                    Note("3라운드마다 각자의 역할이 한 단계씩 교대됩니다."),
                    Turn(rabbit, "플레이어 B", "딜러"),
                    Turn(fox, "딜러", "플레이어 A"),
                    Turn(croc, "플레이어 A", "플레이어 B"),
                    Note("캐릭터는 그대로 유지되고 역할만 바뀝니다.\n" +
                         RoundSettlement.MaxRounds + "라운드 동안 모든 플레이어가 딜러를 " +
                         RoundSettlement.RoleRotationPeriod + "라운드씩 맡습니다.")),

                // ── 3면: 뇌물 ───────────────────────────────────────
                Page(
                    Heading("뇌물"),
                    Row("각 라운드마다 딜러에게 뇌물을 비공개로 제출할 수 있습니다.",
                        Pic(rabbit), Sym("→", 0.7f), Pic(coin, CoinInPortraitRow), Sym("→", 0.7f), Pic(croc)),
                    Note("뇌물은 최대 " + N(RoundSettlement.MaxBribe) + "골드이며, 액수는 딜러에게만 공개됩니다.\n" +
                         "상대 플레이어는 제출 여부와 액수를 확인할 수 없습니다.")),

                Page(
                    Heading("뇌물 반환"),
                    Row("조작이 발각되지 않으면 딜러는 승리한 쪽의 뇌물만 가져갑니다.",
                        Pic(coin, CoinInPortraitRow), Sym("→", 0.7f), Pic(croc)),
                    Row("패배한 쪽의 뇌물은 반환됩니다.",
                        Pic(croc), Sym("→", 0.7f), Pic(coin, CoinInPortraitRow), Sym("→", 0.7f), Pic(fox)),
                    Note("공정하게 진행하거나 조작이 발각되면 양쪽 뇌물이 모두 반환됩니다.")),

                // ── 4면: 조작 ───────────────────────────────────────
                Page(
                    Heading("카드 조작"),
                    Row(170f, "카드를 배분할 때마다 딜러에게 후보 카드 3장이 표시됩니다.",
                        Back(), Back(), Back()),
                    Note("첫 번째 카드를 선택하면 공정, 나머지 카드를 선택하면 조작입니다.\n" +
                         "딜러는 후보에 없는 카드를 선택할 수 없습니다.")),

                Page(
                    Heading("조작 기록"),
                    Row("한 번이라도 조작하면 해당 라운드는 조작으로 기록됩니다.",
                        Back(), Sym("→", 0.7f), Card(BlackjackCardRank.Two, BlackjackCardSuit.Clubs)),
                    Note("조작 결과가 플레이어에게 불리해도 조작으로 판정됩니다.\n" +
                         "조작 여부는 고발 선택이 끝난 뒤 공개됩니다.")),

                // ── 5면: 고발 ───────────────────────────────────────
                Page(
                    Heading("고발"),
                    Row("패배한 플레이어만 결과 공개 후 딜러를 고발할 수 있습니다.",
                        Pic(fox), Sym("→", 0.8f), Pic(croc)),
                    Note("고발 비용은 " + N(RoundSettlement.AccusationDeposit) + "골드이며, " +
                         "성공해도 반환되지 않습니다.\n" +
                         "고발하지 않고 승복하면 공개된 결과가 그대로 확정됩니다.")),

                Page(
                    Heading("고발 판정"),
                    Row("조작이 확인되면 승패가 뒤집히고 딜러가 벌금 " +
                        N(RoundSettlement.DealerFine) + "골드를 냅니다.",
                        Pic(coin, CoinInPortraitRow), Sym("→", 0.7f), Pic(fox)),
                    Note("조작이 없으면 무고 벌금 " + N(RoundSettlement.FalseAccuseFine) + "골드를 추가로 냅니다.\n" +
                         "성공하면 " + N(RoundSettlement.DealerFine - RoundSettlement.AccusationDeposit) +
                         "골드 이득, 실패하면 " +
                         N(RoundSettlement.AccusationDeposit + RoundSettlement.FalseAccuseFine) +
                         "골드 손해이며 판돈은 별도로 정산됩니다.")),

                // ── 6면: 돈 계산 ────────────────────────────────────
                Page(
                    Heading("판돈"),
                    Row(CoinRowHeight, "두 플레이어가 건 금액 중 더 적은 금액이 실제 판돈이 됩니다.",
                        Pic(coin, 0.62f), Sym("↔", 0.7f), Pic(coin, 1f)),
                    Note("승리한 플레이어가 판돈을 가져갑니다.\n" +
                         "다이는 판돈의 절반을 잃고, 더블다운은 판돈을 2배로 올립니다.")),

                Page(
                    Heading("딜러 보상"),
                    Row(CoinRowHeight, "공정하게 진행하면 딜러는 " + N(RoundSettlement.SafeReward) + "골드를 받습니다.",
                        Pic(coin, 0.72f)),
                    Row(CoinRowHeight, "뇌물을 적게 낸 쪽을 조작으로 이기게 하면 양쪽 판돈과 뇌물을 모두 가져갑니다.",
                        Pic(coin), Pic(coin)),
                    Note("이를 배신이라 하며, 두 플레이어의 뇌물 액수가 다를 때만 발생합니다."))
            };

            AssignTextKeys(pages);

            // 인스펙터에서 손으로 채우는 것과 같은 자리에 그대로 넣는다.
            // SerializedProperty로 쓰면 덩어리·낱개가 겹겹이라 배열을 손으로 펴야 하는데,
            // 그러면 내용보다 배선 코드가 길어져 무엇을 적었는지 읽히지 않는다.
            Undo.RecordObject(panel, "설명서 내용");
            typeof(HowToPlayPanel)
                .GetField("pages", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.SetValue(panel, pages);
            EditorUtility.SetDirty(panel);
        }

        /// <summary>
        /// 두 씬이 같은 설명서 ID를 사용하게 한다. 본문은 fallback으로 남아 있으므로
        /// 기존 씬과 이전 버전 데이터도 깨지지 않는다.
        /// </summary>
        private static void AssignTextKeys(HowToPlayPage[] pages)
        {
            if (pages == null) return;
            for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
            {
                var blocks = pages[pageIndex]?.Blocks;
                if (blocks == null) continue;
                for (int blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
                {
                    var block = blocks[blockIndex];
                    if (block == null) continue;
                    string prefix = $"HowToPlay.Page{pageIndex + 1:00}.Block{blockIndex + 1:00}";
                    if (!string.IsNullOrWhiteSpace(block.Text)) block.TextKey = prefix + ".Text";

                    if (block.Items == null) continue;
                    for (int itemIndex = 0; itemIndex < block.Items.Length; itemIndex++)
                    {
                        var item = block.Items[itemIndex];
                        if (item == null || item.Kind != HowToPlayItemKind.Symbol
                            || string.IsNullOrWhiteSpace(item.Symbol)
                            || !ContainsKorean(item.Symbol)) continue;
                        item.TextKey = prefix + $".Item{itemIndex + 1:00}";
                    }
                }
            }
        }

        private static bool ContainsKorean(string value)
        {
            foreach (char c in value)
                if (c >= '\uac00' && c <= '\ud7a3') return true;
            return false;
        }

        // ── 덩어리 만들기 ────────────────────────────────────────────

        private static HowToPlayPage Page(params HowToPlayBlock[] blocks) =>
            new HowToPlayPage { Blocks = blocks };

        private static HowToPlayBlock Heading(string text) => new HowToPlayBlock
        {
            Kind = HowToPlayBlockKind.Heading,
            Text = text
        };

        private static HowToPlayBlock Note(string text) => new HowToPlayBlock
        {
            Kind = HowToPlayBlockKind.Caption,
            Text = text
        };

        private static HowToPlayBlock Row(string caption, params HowToPlayItem[] items) =>
            Row(0f, caption, items);

        private static HowToPlayBlock Row(float height, string caption, params HowToPlayItem[] items) =>
            new HowToPlayBlock
            {
                Kind = HowToPlayBlockKind.Row,
                Text = caption,
                Items = items,
                Height = height
            };

        private static HowToPlayItem Card(
            BlackjackCardRank rank, BlackjackCardSuit suit, float scale = 1f) => new HowToPlayItem
        {
            Kind = HowToPlayItemKind.Card,
            Rank = rank,
            Suit = suit,
            Scale = scale
        };

        private static HowToPlayItem Back(float scale = 1f) => new HowToPlayItem
        {
            Kind = HowToPlayItemKind.CardBack,
            Scale = scale
        };

        private static HowToPlayItem Pic(Sprite sprite, float scale = 1f) => new HowToPlayItem
        {
            Kind = HowToPlayItemKind.Image,
            Image = sprite,
            Scale = scale
        };

        private static HowToPlayItem Sym(string symbol, float scale = 1f, float minWidth = 0f) =>
            new HowToPlayItem
            {
                Kind = HowToPlayItemKind.Symbol,
                Symbol = symbol,
                Scale = scale,
                MinWidth = minWidth
            };

        /// <summary>
        /// 자리가 옮겨 가는 줄 하나 — 동물 하나가 <b>지금 무엇이고 다음에 무엇이 되는지</b>.
        ///
        /// 셋을 나란히 놓아야 자리가 도는 것이 보인다. 딜러 자리만 화살표로 잇던 예전
        /// 그림은 나머지 둘이 어디로 가는지를 말해 주지 못했다.
        /// </summary>
        private static HowToPlayBlock Turn(Sprite portrait, string now, string next) =>
            Row(RotationRowHeight, null,
                Pic(portrait),
                Sym("→", RotationArrowScale),
                Sym(now, RoleWordScale, RoleWordMinWidth),
                Sym("→", RotationArrowScale),
                Sym(next, RoleWordScale, RoleWordMinWidth));

        /// <summary>자리가 도는 줄의 높이. 한 쪽에 셋이 들어가야 해서 기본값보다 낮다.</summary>
        private const float RotationRowHeight = 96f;

        /// <summary>역할 이름의 크기. 줄 아래 설명글과 비슷하게 읽히는 정도.</summary>
        private const float RoleWordScale = 0.45f;

        /// <summary>
        /// 역할 이름이 차지하는 자리("플레이어 A"가 들어갈 만큼). 셋 다 같은 값이라
        /// "딜러"처럼 짧은 이름이 섞여도 세 줄의 초상화와 화살표가 세로로 맞는다.
        /// </summary>
        private const float RoleWordMinWidth = 2.7f;

        /// <summary>자리가 도는 줄의 화살표. 이름보다 조금 작아야 이름이 먼저 읽힌다.</summary>
        private const float RotationArrowScale = 0.5f;

        /// <summary>
        /// 금화만 있는 줄의 높이. 금화 그림은 48px짜리라 카드와 같은 크기로 키우면
        /// 뭉개져 보인다 — 원래 크기에 가깝게 두는 편이 낫다.
        /// </summary>
        private const float CoinRowHeight = 96f;

        /// <summary>초상화와 한 줄에 설 때 금화의 크기. 사람보다 커 보이면 안 된다.</summary>
        private const float CoinInPortraitRow = 0.45f;

        /// <summary>돈은 화면 어디서나 세 자리마다 끊어 적는다.</summary>
        private static string N(int amount) => amount.ToString("N0");

        // ── 그림 찾기 ────────────────────────────────────────────────

        /// <summary>
        /// 초상화는 <b>머니바가 쓰는 것</b>을 그대로 빌린다. 파일에서 따로 불러오면
        /// 얼굴을 바꿨을 때 설명서만 옛 얼굴로 남아, 같은 사람이 두 얼굴이 된다.
        /// </summary>
        private static Sprite Portrait(GameDirector director, string field)
        {
            if (director == null) return null;
            var found = new SerializedObject(director).FindProperty(field);
            return found?.objectReferenceValue as Sprite;
        }

        /// <summary>
        /// 이름으로 찾는다. 경로로 박아 두면 그림을 옮겼을 때 설명서만 조용히 비어 버린다.
        /// </summary>
        private static Sprite FindSprite(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Sprite"))
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (sprite != null) return sprite;
            }

            Debug.LogWarning($"[HowToPlayContentTool] '{name}' 그림을 찾지 못했습니다. " +
                             "그 자리는 비어서 나옵니다.");
            return null;
        }
    }
}
