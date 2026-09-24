using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// PanelFinal의 두 화면 — <b>최종 판정</b>과 <b>매치 종료</b> — 을 만들고 규격을 맞춘다.
    ///
    /// 하는 일은 셋이다.
    ///
    /// 1. <b>규격 통일.</b> 겉보기 승리 카드(PanelResult/ResultCard)를 기준으로 읽어
    ///    최종 판정 카드의 크기와 글자 크기를 맞춘다. 세 화면이 같은 카드로 보여야
    ///    한 게임의 화면으로 읽힌다. 기준을 코드에 박지 않고 씬에서 읽는 이유는,
    ///    나중에 겉보기 카드를 손봐도 이 도구를 다시 돌리면 따라오게 하기 위해서다.
    ///
    /// 2. <b>매치 종료 카드 분리.</b> 지금까지 두 화면이 카드 하나를 돌려 썼다. 그러면
    ///    머리말·제목·버튼 라벨을 매번 갈아 끼워야 하고, 한 군데라도 빠뜨리면 매치가
    ///    끝난 화면에 "다음 라운드"가 남는다. 그래서 판정 카드를 복제해 매치 종료
    ///    전용 카드를 따로 세운다 — 복제라서 생김새는 자동으로 같다.
    ///
    /// 3. <b>순위표.</b> 카드 3장이 테이블 위쪽에 가로로 선다.
    ///
    /// 여러 번 눌러도 안전하다 — 만들어 둔 것은 지우고 다시 만들고, 기존 카드는
    /// 값을 덮어쓴다. 자리나 크기를 바꿀 일이 생기면 씬이 아니라 여기를 고쳐야 한다.
    /// 씬에서 직접 고치면 다음에 이 도구를 돌렸을 때 조용히 되돌아간다.
    /// </summary>
    public static class FinalScreenSetupTool
    {
        // ── 씬 경로 ─────────────────────────────────────────────────

        private const string TemplateCardPath = "PanelResult/ResultCard";
        private const string TemplateTitle = "txtResultOutcome";
        private const string TemplateBody = "ResultDescription";
        private const string TemplateButton = "btnContinue";

        private const string JudgmentCardPath = "FinalOutcomeGroup/FinalVictoryCard";
        private const string JudgmentKicker = "FinalKicker";
        private const string JudgmentTitle = "txtFinalWinner";
        private const string JudgmentBody = "txtFinalReason";
        private const string JudgmentButton = "btnNextRound";

        private const string MatchOverCardName = "ZJ_MatchOverCard";
        private const string BoardName = "ZJ_FinalRanking";

        /// <summary>머니바를 못 찾았을 때 물러설 금화 스프라이트 경로.</summary>
        private const string CoinSpritePath =
            "Assets/ImportedAsset/kenney_boardgame-pack/PNG/Pieces (Yellow)/pieceYellow_border10.asset";

        // ── 최종 카드 안쪽 자리 ──────────────────────────────────────
        //
        // 크기는 겉보기 카드에서 가져오지만 자리는 조금 다르다. 최종 카드에는 겉보기에
        // 없는 머리말 한 줄이 더 있어서, 제목을 그만큼 내려야 넷이 모두 들어간다.
        // 카드 높이가 220이므로 위아래 끝은 ±110이다.
        private static readonly Vector2 KickerSize = new Vector2(260f, 22f);
        private const float KickerY = 92f;    //  81 ~ 103
        private const float TitleY = 48f;     //  17 ~  79
        private const float BodyY = -4f;      // -26 ~  18
        private const float ButtonY = -64f;   // -93 ~ -35

        /// <summary>
        /// 최종 카드의 사유 칸 높이. 겉보기 카드(32)보다 높다 — 판정 사유는
        /// "고발 실패! 조작 기록이 없어 무고 벌금 100 을(를) 물었습니다."처럼 길어
        /// 18pt에서 두 줄이 되는데, 32px면 둘째 줄이 잘린다.
        /// </summary>
        private const float BodyHeight = 44f;

        // ── 매치 종료의 두 버튼 ─────────────────────────────────────
        //
        // 값은 씬에서 손으로 맞춘 것을 그대로 옮겼다. 이 도구가 카드를 매번 새로 만들기
        // 때문에, 여기 적어 두지 않으면 다음 실행에서 조용히 되돌아간다.
        //
        // 크기를 같게 둔 것은 의도다 — 나가기는 한 명이 자리를 비웠을 때 남은 사람이
        // 갇히지 않는 유일한 출구라, 작게 만들어 물러난 선택처럼 보이게 하지 않는다.
        // 대신 색으로 가른다: 다시 플레이는 초록, 나가기는 적갈색(다이 버튼과 같은 색).
        private const float ButtonHeight = 58f;
        private const float ButtonWidth = 230.4f;
        private const float ButtonGap = 56f;

        private static readonly Color RestartFill = ZooJackPalette.Forest.Alpha(0.98f);
        private static readonly Color LeaveFill = ZooJackPalette.Maroon.Alpha(0.98f);
        private static readonly Color ButtonBorder = ZooJackPalette.Gold;
        private const float ButtonBorderThickness = 2f;
        private static readonly Color RestartLabel = ZooJackPalette.Cream;
        private static readonly Color LeaveLabel = ZooJackPalette.Sand;

        // ── 순위표 ──────────────────────────────────────────────────

        private const int RowCount = 3;
        private const float CardWidth = 400f;
        private const float CardHeight = 100f;
        private const float CardGap = 16f;

        /// <summary>
        /// 테이블 위쪽 띠. 캔버스 y 230~330을 쓴다.
        ///
        /// 시계 카드(y 346~418) <b>아래</b>로 내려앉는다. 매치 종료에도 다른 단계와 같은
        /// 자리에 같은 시계가 떠야 하는데, 그 자리를 순위표가 덮고 있었다.
        /// 아래쪽 아바타와는 겹칠 수 있지만 PanelFinal이 테이블보다 나중에 그려지므로
        /// 순위표가 위에 온다 — 서로 뚫고 나오지 않는다.
        /// </summary>
        private static readonly Vector2 BoardPosition = new Vector2(0f, 280f);

        private const float BadgeSize = 44f;
        private const float BadgeX = 38f;
        private const float PortraitSize = 68f;
        private const float PortraitX = 102f;
        private const float TextX = 146f;
        private const float TextWidth = 145f;
        private const float NameSize = 25f;
        private const float SubtitleSize = 15f;
        private const float BalanceSize = 30f;
        private const float BalanceRight = -18f;
        private const float BalanceWidth = 140f;

        // 순위 카드의 색은 실행 중에 <see cref="FinalRankingBoard"/>가 그 사람의 캐릭터
        // 색으로 다시 칠한다(머니바 카드와 같은 색). 여기 값은 에디터에서 열어 봤을 때
        // 깨져 보이지 않게 하는 밑칠일 뿐이다.
        private static readonly Color BaseFill = ZooJackPalette.Ink;
        private static readonly Color BaseBorder = ZooJackPalette.Steel;
        private static readonly Color BaseBadgeFill = ZooJackPalette.Graphite;

        private static readonly Color NameColor = ZooJackPalette.Cream;
        private static readonly Color SubtitleColor = ZooJackPalette.Stone;
        private static readonly Color Shadow = ZooJackPalette.Shadow;

        private const float BorderThickness = 2.5f;

        // ── 기준 규격 ───────────────────────────────────────────────

        /// <summary>
        /// 겉보기 승리 카드에서 읽어 온 규격. 색은 담지 않는다 — 화면마다 정해 둔
        /// 색이 있고, 맞춰야 하는 것은 크기다.
        /// </summary>
        private struct CardSpec
        {
            public Vector2 CardSize;
            public Vector2 TitleSize;
            public float TitleFont;
            public FontStyles TitleStyle;
            public Color TitleColor;
            public Vector2 BodySize;
            public float BodyFont;
            public Vector2 ButtonSize;
            public float ButtonFont;
        }

        [MenuItem("ZooJack/게임/최종 화면 만들기")]
        public static void Setup()
        {
            var director = Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                Fail("열려 있는 씬에서 GameDirector를 찾지 못했습니다. GameScene을 먼저 여세요.");
                return;
            }

            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            var panel = FindPanelFinal(director, canvas);
            if (panel == null) { Fail("PanelFinal을 찾지 못했습니다."); return; }

            var templateCard = canvas != null ? canvas.transform.Find(TemplateCardPath) : null;
            if (templateCard == null)
            {
                Fail($"기준이 되는 겉보기 승리 카드를 찾지 못했습니다: {TemplateCardPath}");
                return;
            }
            if (!ReadSpec(templateCard, out CardSpec spec)) return;

            var judgment = panel.Find(JudgmentCardPath);
            if (judgment == null) { Fail($"최종 판정 카드를 찾지 못했습니다: {JudgmentCardPath}"); return; }

            NormalizeFinalCard(judgment, spec, JudgmentKicker, JudgmentTitle, JudgmentBody, JudgmentButton);
            var matchOver = BuildMatchOverCard(panel, judgment);

            // 글꼴은 씬에서 빌려 온다. 경로로 불러오면 나중에 글꼴을 바꿨을 때
            // 순위표만 옛 글꼴로 남는다.
            var board = BuildBoard(panel, BorrowFont(panel));

            Wire(director, board, matchOver);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[FinalScreenSetupTool] 최종 판정 카드를 겉보기 승리 카드 규격"
                + $"({spec.CardSize.x}x{spec.CardSize.y}, 제목 {spec.TitleFont}pt)으로 맞추고, "
                + "매치 종료 카드와 순위표를 만들었습니다.");
        }

        private static void Fail(string message) =>
            EditorUtility.DisplayDialog("최종 화면", message, "확인");

        private static Transform FindPanelFinal(GameDirector director, Canvas canvas)
        {
            // GameDirector가 들고 있는 것을 먼저 믿는다. 이름으로만 찾으면 패널이
            // 다른 곳으로 옮겨졌을 때 조용히 엉뚱한 오브젝트를 잡는다.
            var so = new SerializedObject(director);
            var prop = so.FindProperty("panelFinal");
            if (prop != null && prop.objectReferenceValue is GameObject go) return go.transform;
            return canvas != null ? canvas.transform.Find("PanelFinal") : null;
        }

        private static TMP_FontAsset BorrowFont(Transform panel)
        {
            foreach (var text in panel.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.font != null) return text.font;
            return null;
        }

        // ── 규격 읽기 · 맞추기 ───────────────────────────────────────

        private static bool ReadSpec(Transform card, out CardSpec spec)
        {
            spec = default;

            var title = card.Find(TemplateTitle)?.GetComponent<TextMeshProUGUI>();
            var body = card.Find(TemplateBody)?.GetComponent<TextMeshProUGUI>();
            var button = card.Find(TemplateButton) as RectTransform;
            var buttonLabel = button != null ? button.GetComponentInChildren<TextMeshProUGUI>(true) : null;

            if (title == null || body == null || button == null || buttonLabel == null)
            {
                Fail($"겉보기 승리 카드의 구성을 알아보지 못했습니다. "
                    + $"{TemplateTitle} / {TemplateBody} / {TemplateButton}이 모두 있어야 합니다.");
                return false;
            }

            spec = new CardSpec
            {
                CardSize = ((RectTransform)card).sizeDelta,
                TitleSize = ((RectTransform)title.transform).sizeDelta,
                TitleFont = title.fontSize,
                TitleStyle = title.fontStyle,
                TitleColor = title.color,
                BodySize = ((RectTransform)body.transform).sizeDelta,
                BodyFont = body.fontSize,
                ButtonSize = button.sizeDelta,
                ButtonFont = buttonLabel.fontSize
            };
            return true;
        }

        /// <summary>
        /// 최종 카드 한 장을 기준 규격에 맞춘다. 최종 승리 제목은 일반 승리 제목과
        /// 같은 의미 단계이므로 크기뿐 아니라 색도 동일하게 맞춘다.
        /// </summary>
        private static void NormalizeFinalCard(
            Transform card, CardSpec spec,
            string kickerName, string titleName, string bodyName, string buttonName)
        {
            var rect = (RectTransform)card;
            rect.sizeDelta = spec.CardSize;

            var kicker = card.Find(kickerName);
            if (kicker != null)
                Place((RectTransform)kicker, KickerSize, KickerY);

            var title = card.Find(titleName);
            if (title != null)
            {
                Place((RectTransform)title, spec.TitleSize, TitleY);
                var tmp = title.GetComponent<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.fontSize = spec.TitleFont;
                    tmp.fontStyle = spec.TitleStyle;
                    tmp.color = spec.TitleColor;
                }
            }

            var body = card.Find(bodyName);
            if (body != null)
            {
                Place((RectTransform)body, new Vector2(spec.BodySize.x, BodyHeight), BodyY);
                var tmp = body.GetComponent<TextMeshProUGUI>();
                if (tmp != null) tmp.fontSize = spec.BodyFont;
            }

            var button = card.Find(buttonName);
            if (button == null) return;

            Place((RectTransform)button, spec.ButtonSize, ButtonY);
            var label = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null) label.fontSize = spec.ButtonFont;
        }

        // 최종 카드의 부품은 모두 카드 한가운데 기준이라 x는 0이다.
        private static void Place(RectTransform rect, Vector2 size, float y)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(0f, y);
        }

        // ── 매치 종료 카드 ──────────────────────────────────────────

        /// <summary>
        /// 최종 판정 카드를 복제해 매치 종료 전용 카드를 만든다. 복제이므로 색·테두리·
        /// 그림자가 자동으로 같고, 앞서 맞춘 규격도 그대로 따라온다.
        ///
        /// 자리는 판정 카드와 같다. 둘은 동시에 뜨지 않으므로 겹쳐도 되고, 오히려 같은
        /// 자리에 서야 화면이 바뀔 때 카드가 튀지 않는다.
        /// </summary>
        private static GameObject BuildMatchOverCard(Transform panel, Transform judgment)
        {
            var existing = panel.Find(MatchOverCardName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var card = Object.Instantiate(judgment.gameObject, panel, false);
            card.name = MatchOverCardName;
            card.SetActive(false);   // 매치가 끝날 때만 올라온다

            // 판정 카드는 패널 바닥에 앵커된 그룹(FinalOutcomeGroup) 안에 있다.
            // 그 그룹을 거치지 않으므로 같은 앵커를 여기서 직접 준다.
            var rect = (RectTransform)card.transform;
            var judgmentRect = (RectTransform)judgment;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = judgmentRect.sizeDelta;
            rect.anchoredPosition = judgmentRect.anchoredPosition;

            Rename(card.transform, JudgmentKicker, "Kicker", "최종 순위");
            Rename(card.transform, JudgmentTitle, "Title", "매치 종료");
            Rename(card.transform, JudgmentBody, "Reason", "매치 종료 사유가 이곳에 표시됩니다.");

            BuildMatchOverButtons(card.transform);
            return card;
        }

        /// <summary>
        /// 매치 종료의 두 출구를 나란히 놓는다.
        ///
        /// 크기를 다르게 준 것은 의도다 — 다시 플레이가 주된 선택이고 나가기는 물러나는
        /// 길이다. 나가기를 같은 크기·같은 색으로 두면 판을 끝내는 쪽이 절반의 무게로
        /// 보인다. 그래도 확실히 눌리는 크기는 지킨다 — 한 명이 자리를 비웠을 때
        /// 남은 사람이 갇히지 않는 유일한 출구이기 때문이다.
        /// </summary>
        private static void BuildMatchOverButtons(Transform card)
        {
            var primary = card.Find(JudgmentButton);
            if (primary == null)
            {
                Debug.LogWarning($"[FinalScreenSetupTool] 매치 종료 카드에 {JudgmentButton}이 없습니다.");
                return;
            }

            float x = (ButtonWidth + ButtonGap) * 0.5f;

            primary.name = "btnRestartMatch";
            ShapeButton((RectTransform)primary, -x, "다시 플레이", RestartFill, RestartLabel);

            var leave = Object.Instantiate(primary.gameObject, card, false);
            leave.name = "btnLeaveMatch";
            ShapeButton((RectTransform)leave.transform, x, "나가기", LeaveFill, LeaveLabel);
        }

        // 복제본이 판정 버튼의 인스펙터 연결을 물고 오면, 매치 종료 버튼이 다음 라운드까지
        // 함께 부른다. 두 버튼 모두 코드에서 연결하므로 여기서 비운다.
        private static void ShapeButton(
            RectTransform rect, float x, string text, Color fill, Color labelColor)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
            rect.anchoredPosition = new Vector2(x, ButtonY);

            var frame = rect.GetComponent<RoundedPanelGraphic>();
            if (frame != null)
            {
                frame.SetBackground(fill);
                frame.SetBorder(ButtonBorder, ButtonBorderThickness);
            }

            var label = rect.GetComponentInChildren<TextMeshProUGUI>(true);
            if (label != null)
            {
                label.text = text;
                label.color = labelColor;
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(6f, 6f);
                labelRect.offsetMax = new Vector2(-6f, -6f);
            }

            var button = rect.GetComponent<Button>();
            if (button == null) return;

            var so = new SerializedObject(button);
            so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls").ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Rename(Transform card, string from, string to, string text)
        {
            var child = card.Find(from);
            if (child == null) return;

            child.name = to;
            var tmp = child.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;
        }

        // ── 순위표 ──────────────────────────────────────────────────

        private static FinalRankingBoard BuildBoard(Transform panel, TMP_FontAsset font)
        {
            var existing = panel.Find(BoardName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            float total = CardWidth * RowCount + CardGap * (RowCount - 1);

            var root = CreateChild(panel, BoardName);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = rootRect.anchorMax = rootRect.pivot = new Vector2(0.5f, 0.5f);
            rootRect.sizeDelta = new Vector2(total, CardHeight);
            rootRect.anchoredPosition = BoardPosition;

            var board = root.AddComponent<FinalRankingBoard>();
            var rows = new FinalRankingBoard.Row[RowCount];

            // 왼쪽부터 쌓는다. 1등이 맨 왼쪽이라야 순위표가 좌우로 읽힌다.
            float step = CardWidth + CardGap;
            float firstX = -(total - CardWidth) * 0.5f;
            for (int i = 0; i < RowCount; i++)
                rows[i] = BuildRow(rootRect, i, firstX + step * i, font);

            var so = new SerializedObject(board);
            var prop = so.FindProperty("rows");
            prop.arraySize = rows.Length;
            for (int i = 0; i < rows.Length; i++) WriteRow(prop.GetArrayElementAtIndex(i), rows[i]);
            so.FindProperty("borderThickness").floatValue = BorderThickness;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return board;
        }

        private static void WriteRow(SerializedProperty element, FinalRankingBoard.Row row)
        {
            element.FindPropertyRelative("Root").objectReferenceValue = row.Root;
            element.FindPropertyRelative("Frame").objectReferenceValue = row.Frame;
            element.FindPropertyRelative("AccentBar").objectReferenceValue = row.AccentBar;
            element.FindPropertyRelative("BadgeFrame").objectReferenceValue = row.BadgeFrame;
            element.FindPropertyRelative("Rank").objectReferenceValue = row.Rank;
            element.FindPropertyRelative("Portrait").objectReferenceValue = row.Portrait;
            element.FindPropertyRelative("Name").objectReferenceValue = row.Name;
            element.FindPropertyRelative("Subtitle").objectReferenceValue = row.Subtitle;
            element.FindPropertyRelative("Balance").objectReferenceValue = row.Balance;
        }

        private static FinalRankingBoard.Row BuildRow(
            RectTransform parent, int index, float centerX, TMP_FontAsset font)
        {
            var rowGo = CreateChild(parent, "Row" + (index + 1));
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            rowRect.anchoredPosition = new Vector2(centerX, 0f);

            var frame = rowGo.AddComponent<RoundedPanelGraphic>();
            frame.Configure(
                background: BaseFill,
                outerBorder: BaseBorder,
                radius: 12f,
                outerThickness: BorderThickness,
                innerBorder: new Color(0f, 0f, 0f, 0f),
                innerInset: 0f,
                innerThickness: 0f,
                shadow: Shadow,
                shadowPosition: new Vector2(0f, -4f),
                shadowSize: 3f);
            frame.raycastTarget = false;

            // 왼쪽 끝 색 띠. 캐릭터 색이라 머니바 라벨과 같은 색이 되고, 등수 색(금·은·동)과
            // 겹치지 않아 "몇 등인가"와 "누구인가"를 따로 읽을 수 있다.
            var accent = CreateChild(rowGo.transform, "Accent");
            var accentRect = (RectTransform)accent.transform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.offsetMin = new Vector2(7f, 12f);
            accentRect.offsetMax = new Vector2(13f, -12f);
            var accentImage = accent.AddComponent<Image>();
            accentImage.raycastTarget = false;

            var badge = BuildBadge(rowGo.transform, font, out RoundedPanelGraphic badgeFrame);

            var portrait = CreateChild(rowGo.transform, "Portrait");
            Anchor((RectTransform)portrait.transform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(PortraitX, 0f), new Vector2(PortraitSize, PortraitSize));
            var portraitImage = portrait.AddComponent<Image>();
            portraitImage.preserveAspect = true;
            portraitImage.raycastTarget = false;

            var name = Label(rowGo.transform, "Name", NameSize, NameColor,
                TextAlignmentOptions.Left, font);
            Anchor((RectTransform)name.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, 13f), new Vector2(TextWidth, NameSize + 8f));

            var subtitle = Label(rowGo.transform, "Subtitle", SubtitleSize, SubtitleColor,
                TextAlignmentOptions.Left, font);
            Anchor((RectTransform)subtitle.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(TextX, -17f), new Vector2(TextWidth, SubtitleSize + 8f));

            // 잔액은 오른쪽 끝에 붙인다. 세 카드의 숫자가 같은 선에 서야 서로 비교된다.
            var balance = Label(rowGo.transform, "Balance", BalanceSize, Color.white,
                TextAlignmentOptions.Right, font);

            // 금화 아이콘(<sprite index=0>)을 머니바와 같은 그림으로 맞춘다. 지정하지 않으면
            // TMP 전역 기본값인 EmojiOne이 쓰여 index 0이 웃는 얼굴로 나온다.
            balance.spriteAsset = LoadCoinSprite();
            Anchor((RectTransform)balance.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(BalanceRight, 0f), new Vector2(BalanceWidth, BalanceSize + 8f));

            return new FinalRankingBoard.Row
            {
                Root = rowGo,
                Frame = frame,
                AccentBar = accentImage,
                BadgeFrame = badgeFrame,
                Rank = badge,
                Portrait = portraitImage,
                Name = name,
                Subtitle = subtitle,
                Balance = balance
            };
        }

        // 등수 숫자를 담은 동그란 배지. 반지름을 지름의 절반으로 주어 원이 된다.
        // 색은 실행 중에 그 사람의 캐릭터 색으로 다시 칠해진다.
        private static TextMeshProUGUI BuildBadge(
            Transform parent, TMP_FontAsset font, out RoundedPanelGraphic frame)
        {
            var go = CreateChild(parent, "RankBadge");
            Anchor((RectTransform)go.transform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(BadgeX, 0f), new Vector2(BadgeSize, BadgeSize));

            var graphic = go.AddComponent<RoundedPanelGraphic>();
            frame = graphic;
            graphic.Configure(
                background: BaseBadgeFill,
                outerBorder: BaseBorder,
                radius: BadgeSize * 0.5f,
                outerThickness: 2f,
                innerBorder: new Color(0f, 0f, 0f, 0f),
                innerInset: 0f,
                innerThickness: 0f,
                shadow: new Color(0f, 0f, 0f, 0f),
                shadowPosition: Vector2.zero,
                shadowSize: 0f);
            graphic.raycastTarget = false;

            var label = Label(go.transform, "Rank", BadgeSize * 0.52f, Color.white,
                TextAlignmentOptions.Center, font);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(0f, -2f);
            labelRect.offsetMax = new Vector2(0f, -2f);
            label.fontStyle = FontStyles.Bold;
            return label;
        }

        // ── 연결 ─────────────────────────────────────────────────────

        private static void Wire(GameDirector director, FinalRankingBoard board, GameObject matchOver)
        {
            var so = new SerializedObject(director);

            if (!Assign(so, "finalRanking", board)) return;
            Assign(so, "matchOverCard", matchOver);
            Assign(so, "txtMatchOverTitle", Find<TextMeshProUGUI>(matchOver, "Title"));
            Assign(so, "txtMatchOverReason", Find<TextMeshProUGUI>(matchOver, "Reason"));
            Assign(so, "btnRestartMatch", Find<Button>(matchOver, "btnRestartMatch"));
            Assign(so, "btnLeaveMatch", Find<Button>(matchOver, "btnLeaveMatch"));

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T Find<T>(GameObject root, string child) where T : Component
        {
            var t = root != null ? root.transform.Find(child) : null;
            return t != null ? t.GetComponent<T>() : null;
        }

        private static bool Assign(SerializedObject so, string field, Object value)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogError($"[FinalScreenSetupTool] GameDirector에 {field} 칸이 없습니다. "
                    + "스크립트가 컴파일됐는지 확인하세요.");
                return false;
            }
            if (value == null)
                Debug.LogWarning($"[FinalScreenSetupTool] {field}에 넣을 오브젝트를 찾지 못했습니다.");

            prop.objectReferenceValue = value;
            return true;
        }

        // ── 잡일 ─────────────────────────────────────────────────────

        /// <summary>
        /// 머니바가 쓰는 금화 스프라이트. 경로로 불러오지 않고 <b>머니바에서 빌려 온다</b> —
        /// 같은 그림이어야 하는 것이 요점이라, 한쪽이 바뀌면 따라와야 한다.
        /// 머니바를 못 찾으면 에셋 경로로 물러선다.
        /// </summary>
        private static TMP_SpriteAsset LoadCoinSprite()
        {
            var director = Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include);
            if (director != null)
            {
                var so = new SerializedObject(director);
                var cards = so.FindProperty("moneyCards");
                for (int i = 0; cards != null && i < cards.arraySize; i++)
                {
                    var balance = cards.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("Balance").objectReferenceValue as TextMeshProUGUI;
                    if (balance != null && balance.spriteAsset != null) return balance.spriteAsset;
                }
            }

            var fallback = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(CoinSpritePath);
            if (fallback == null)
                Debug.LogWarning($"[FinalScreenSetupTool] 금화 스프라이트를 찾지 못했습니다: {CoinSpritePath}. "
                    + "잔액 옆에 이모지가 나올 수 있습니다.");
            return fallback;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Anchor(
            RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static TextMeshProUGUI Label(
            Transform parent, string name, float size, Color color,
            TextAlignmentOptions align, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            label.text = string.Empty;
            return label;
        }
    }
}
