using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 기록 화면(<see cref="HistoryPanel"/>)을 열려 있는 씬의 Canvas에 만든다.
    ///
    /// '설정' 버튼 바로 아래에 '기록' 버튼을 두고, 누르면 화면 가운데 카드가 뜬다.
    /// 카드 위에는 칸 두 개(라운드·랭킹)가 붙고, 그 아래는 줄이 쌓이는 스크롤 영역이다.
    ///
    /// <b>줄은 만들지 않는다.</b> 라운드는 아홉 판까지, 랭킹은 끝이 없으므로 미리 깔 수 없다.
    /// 여기서는 <b>본보기 줄 하나씩</b>만 만들어 꺼 두고, 실행 중에 패널이 복제해 쓴다.
    ///
    /// 이미 있으면 지우고 새로 만든다 — 값이 반쯤 바뀐 옛 화면이 남는 것보다 낫다.
    /// </summary>
    public static class HistoryPanelSetupTool
    {
        private const string RootName = "ZJ_History";

        // ── 자리와 크기 ──────────────────────────────────────────────
        // 왼쪽 위 여는 버튼의 자리는 OpenButtonColumn이 정한다 — 셋이 한 열에 선다.

        private static readonly Vector2 CardSize = new Vector2(1240f, 760f);
        private const float CardPadding = 28f;

        private static readonly Vector2 TabSize = new Vector2(168f, 52f);
        private const float TabGap = 6f;

        /// <summary>칸이 카드 위쪽 선 아래로 파고드는 깊이. 이만큼이 카드에 가려 각져 보인다.</summary>
        private const float TabOverlap = 14f;

        // ── 줄 ───────────────────────────────────────────────────────

        /// <summary>카드 띠 · 뇌물 줄 · 판정 줄, 세 층이 들어간다.</summary>
        private const float RoundRowHeight = 166f;
        private const float RankRowHeight = 96f;
        private const float RowSpacing = 12f;

        /// <summary>줄 안쪽에서 왼쪽 제목 칸이 차지하는 너비.</summary>
        private const float TitleWidth = 168f;

        /// <summary>자리 교대 표식. 제목 칸 안에서 '라운드 N' 글자 바로 뒤에 선다.</summary>
        private const float RotateBadgeX = 130f;
        private const float RotateBadgeSize = 30f;

        private static readonly Vector2 PersonCardSize = new Vector2(232f, 66f);
        private const float PersonCardGap = 12f;
        private const float PortraitSize = 48f;

        // ── 색 ───────────────────────────────────────────────────────
        // 환경설정·순위표와 같은 계열이다. 이 화면만 다른 톤이면 게임에서 튀어나온
        // 창처럼 보인다.

        private static readonly Color CardFill = ZooJackPalette.Ink.Alpha(0.99f);
        private static readonly Color CardBorder = ZooJackPalette.Gold;
        private static readonly Color CardInner = ZooJackPalette.Steel.Alpha(0.5f);
        private static readonly Color Shadow = ZooJackPalette.Shadow;
        private static readonly Color ScrimColor = ZooJackPalette.Scrim;

        private static readonly Color RowFill = ZooJackPalette.Hex(0x141719, 0.92f);
        private static readonly Color RowBorder = ZooJackPalette.Hex(0x3C4247, 1f);

        /// <summary>자리 교대 표식. 금화·왕관과 같은 금색이라 '판이 넘어간다'로 읽힌다.</summary>
        private static readonly Color RotateBadgeColor = ZooJackPalette.Hex(0xE8B45C, 1f);

        private static readonly Color TitleColor = ZooJackPalette.Cream;
        private static readonly Color LabelColor = ZooJackPalette.Sand;
        private static readonly Color HintColor = ZooJackPalette.Hex(0x8E8878, 1f);

        private static readonly Color ButtonFill = ZooJackPalette.Graphite.Alpha(0.98f);
        private static readonly Color CloseFill = ZooJackPalette.Forest.Alpha(0.98f);

        private static readonly Color TabActive = ZooJackPalette.Ink.Alpha(0.99f);
        private static readonly Color TabIdle = ZooJackPalette.Hex(0x0F1214, 0.92f);

        private const float TitleSize = 30f;
        private const float TabSizeText = 24f;
        private const float RoundTitleSize = 28f;
        private const float RankTitleSize = 24f;
        private const float RoleSize = 16f;
        private const float AmountSize = 22f;
        private const float HintSize = 15f;
        private const float VerdictSize = 19f;
        private const float BribeSize = 16f;
        private const float ButtonTextSize = 22f;

        /// <summary>
        /// 금액 뒤에 붙는 금화(<c>&lt;sprite index=0&gt;</c>)가 든 그림 묶음.
        ///
        /// 이것을 지정하지 않으면 TMP가 프로젝트 기본값(EmojiOne)을 쓰는데, 그쪽 0번은
        /// 금화가 아니라 웃는 얼굴이다 — 실제로 잔액 옆에 🙂가 붙어 있었다.
        /// 머니바가 쓰는 것을 그대로 빌려 온다.
        /// </summary>
        private static TMP_SpriteAsset coinSprites;

        [MenuItem("ZooJack/게임/기록 화면 만들기")]
        public static void Setup()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("기록 화면",
                    "열려 있는 씬에 Canvas가 없습니다. 게임 씬을 먼저 여세요.", "확인");
                return;
            }

            var font = BorrowFont(canvas.transform);
            var borrowed = BorrowFromGame();
            coinSprites = borrowed.Coin;

            var host = PanelHost.For(canvas);
            var existing = host.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = Child(host, RootName);
            Stretch((RectTransform)root.transform);
            root.transform.SetAsLastSibling();

            var panel = root.AddComponent<HistoryPanel>();

            var wired = default(Wired);
            wired.Open = BuildOpenButton(root.transform, font);
            var window = BuildWindow(root.transform, font, ref wired);

            Wire(panel, window, wired, borrowed);
            window.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[HistoryPanelSetupTool] '{EditorSceneManager.GetActiveScene().name}' 씬에 " +
                      "기록 화면을 만들었습니다. 줄은 실행 중에 본보기를 복제해 만듭니다.");

            Selection.activeGameObject = root;
        }

        /// <summary>도구가 만든 위젯 묶음. 이름으로 다시 찾지 않는다.</summary>
        private struct Wired
        {
            public Button Open, Close, Scrim, RoundTab, RankTab;
            public RoundedPanelGraphic RoundTabFrame, RankTabFrame;
            public TextMeshProUGUI RoundTabLabel, RankTabLabel;
            public ScrollRect RoundScroll, RankScroll;
            public RectTransform RoundContent, RankContent;
            public HistoryRoundRow RoundTemplate;
            public HistoryRankRow RankTemplate;
            public GameObject RoundEmpty, RankEmpty;
        }

        // ── 여는 버튼 ────────────────────────────────────────────────

        private static Button BuildOpenButton(Transform parent, TMP_FontAsset font)
        {
            var go = Child(parent, "OpenButton");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = OpenButtonColumn.Size;
            rect.anchoredPosition = OpenButtonColumn.PositionFor(
                OpenButtonColumn.Slot.History);

            var frame = Frame(go, ButtonFill, CardBorder, 10f, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;

            var label = Label(go.transform, "Label", ButtonTextSize, TitleColor,
                TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            label.text = "기록";
            OpenButtonIconUtility.Apply(
                go.transform, label,
                "Assets/ImportedAsset/UI Image/records.png",
                Vector2.zero);

            return button;
        }

        // ── 창 ───────────────────────────────────────────────────────

        private static GameObject BuildWindow(
            Transform parent, TMP_FontAsset font, ref Wired wired)
        {
            var window = Child(parent, "Window");
            Stretch((RectTransform)window.transform);

            var scrimGo = Child(window.transform, "Scrim");
            Stretch((RectTransform)scrimGo.transform);
            var scrimImage = scrimGo.AddComponent<Image>();
            scrimImage.color = ScrimColor;
            wired.Scrim = scrimGo.AddComponent<Button>();
            wired.Scrim.targetGraphic = scrimImage;
            wired.Scrim.transition = Selectable.Transition.None;

            // 칸과 카드를 함께 담는 자리. 칸이 카드 위로 삐져나와야 하므로
            // (카드에 붙은 종이처럼) 둘을 형제로 두고 이 안에서 자리를 잡는다.
            var body = Child(window.transform, "Body");
            Place((RectTransform)body.transform, Vector2.zero,
                new Vector2(CardSize.x, CardSize.y + TabSize.y));

            var card = Child(body.transform, "Card");
            Place((RectTransform)card.transform,
                new Vector2(0f, -TabSize.y * 0.5f), CardSize);

            var frame = card.AddComponent<RoundedPanelGraphic>();
            frame.Configure(CardFill, CardBorder, 20f, 2.4f, CardInner, 10f, 1f,
                Shadow, new Vector2(0f, -6f), 4f);
            frame.raycastTarget = true;   // 카드 위를 눌렀을 때 뒤의 판이 반응하지 않게

            BuildTabs(body.transform, font, ref wired);

            // 아래 좌표는 모두 카드 안쪽 기준이다(스크롤과 닫기 버튼이 카드의 자식이므로).
            float cardTop = CardSize.y * 0.5f;
            const float CloseHeight = 44f;

            wired.Close = TextButton(card.transform, font, "btnClose", "닫기", CloseFill,
                new Vector2(CardSize.x * 0.5f - CardPadding - 60f,
                            cardTop - CardPadding - CloseHeight * 0.5f),
                new Vector2(120f, CloseHeight));

            // 스크롤 영역. 두 칸이 같은 자리를 쓰고 하나만 켜진다.
            // 위로는 닫기 버튼 아래까지, 아래로는 카드 안쪽 여백까지 꽉 채운다.
            float viewTop = cardTop - CardPadding - CloseHeight - 12f;
            float viewBottom = -cardTop + CardPadding;
            var viewSize = new Vector2(CardSize.x - CardPadding * 2f, viewTop - viewBottom);
            var viewCenter = new Vector2(0f, (viewTop + viewBottom) * 0.5f);

            wired.RoundScroll = BuildScroll(card.transform, "RoundList", viewCenter, viewSize,
                RowSpacing, out wired.RoundContent);
            wired.RankScroll = BuildScroll(card.transform, "RankList", viewCenter, viewSize,
                RowSpacing, out wired.RankContent);

            wired.RoundTemplate = BuildRoundTemplate(wired.RoundContent, font, viewSize.x);
            wired.RankTemplate = BuildRankTemplate(wired.RankContent, font, viewSize.x);

            wired.RoundEmpty = BuildEmpty(wired.RoundScroll.transform, font,
                "아직 끝난 라운드가 없습니다.\n라운드가 끝나면 금액 변화와 조작·고발이 여기에 쌓입니다.");
            wired.RankEmpty = BuildEmpty(wired.RankScroll.transform, font,
                "아직 끝난 게임이 없습니다.\n매치가 끝날 때마다 그 판의 순위가 한 줄씩 쌓입니다.");

            return window;
        }

        private static void BuildTabs(Transform body, TMP_FontAsset font, ref Wired wired)
        {
            // 카드 왼쪽 위 모서리에 맞춰 두 칸을 나란히. 아래쪽 모서리를 각지게 두면
            // 카드와 한 장으로 이어져 보이지만 RoundedPanelGraphic은 네 모서리를 함께
            // 굴리므로, 대신 카드와 살짝 겹쳐 아래쪽 둥근 부분을 카드가 덮게 한다.
            //
            // body 안에서 카드 위쪽 끝은 (CardSize.y - TabSize.y) * 0.5 다. 칸의 아래쪽
            // TabOverlap 만큼이 그 선 아래로 들어가야 겹친다.
            float left = -CardSize.x * 0.5f + CardPadding + TabSize.x * 0.5f;
            float cardTopInBody = (CardSize.y - TabSize.y) * 0.5f;
            float y = cardTopInBody - TabOverlap + TabSize.y * 0.5f;

            wired.RoundTab = Tab(body, font, "btnRoundTab", "라운드", true,
                new Vector2(left, y), out wired.RoundTabFrame, out wired.RoundTabLabel);
            wired.RankTab = Tab(body, font, "btnRankTab", "랭킹", false,
                new Vector2(left + TabSize.x + TabGap, y), out wired.RankTabFrame, out wired.RankTabLabel);

            // 고른 칸이 카드와 이어져 보이려면 카드보다 뒤에 있어야 한다. 카드가
            // 칸의 아래쪽 둥근 모서리를 덮는다.
            wired.RoundTab.transform.SetAsFirstSibling();
            wired.RankTab.transform.SetAsFirstSibling();
        }

        private static Button Tab(
            Transform parent, TMP_FontAsset font, string name, string text, bool active,
            Vector2 position, out RoundedPanelGraphic frame, out TextMeshProUGUI label)
        {
            var go = Child(parent, name);
            Place((RectTransform)go.transform, position, TabSize);

            frame = Frame(go, active ? TabActive : TabIdle, CardBorder, 12f, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;

            label = Label(go.transform, "Label", TabSizeText,
                active ? TitleColor : HintColor, TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            label.text = text;
            if (active) label.fontStyle = FontStyles.Bold;

            // 칸의 아래쪽 TabOverlap 만큼은 카드에 가린다. 글자는 보이는 부분의
            // 가운데에 와야 하므로 그만큼 올려 둔다.
            var labelRect = (RectTransform)label.transform;
            labelRect.offsetMin = new Vector2(0f, TabOverlap);

            return button;
        }

        /// <summary>
        /// 줄이 쌓이는 스크롤 영역. 자리는 <see cref="VerticalLayoutGroup"/>이 잡고
        /// 전체 높이는 <see cref="ContentSizeFitter"/>가 정한다 — 줄이 몇 개일지
        /// 미리 알 수 없으므로 좌표를 손으로 계산할 수 없다.
        /// </summary>
        private static ScrollRect BuildScroll(
            Transform parent, string name, Vector2 position, Vector2 size,
            float spacing, out RectTransform content)
        {
            var go = Child(parent, name);
            Place((RectTransform)go.transform, position, size);

            // 넘치는 줄을 잘라낸다. Mask는 이 그림의 알파로 스텐실을 찍으므로
            // 색은 안 그려도 알파는 1이어야 한다.
            var viewportImage = go.AddComponent<Image>();
            viewportImage.color = Color.white;
            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var contentGo = Child(go.transform, "Content");
            content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = false;
            layout.childControlHeight = false;
            layout.childControlWidth = false;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = go.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = (RectTransform)go.transform;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 28f;

            return scroll;
        }

        private static GameObject BuildEmpty(Transform parent, TMP_FontAsset font, string text)
        {
            var label = Label(parent, "Empty", 20f, HintColor, TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            label.text = text;
            label.lineSpacing = 12f;
            return label.gameObject;
        }

        // ── 라운드 줄 ────────────────────────────────────────────────

        private static HistoryRoundRow BuildRoundTemplate(
            RectTransform content, TMP_FontAsset font, float width)
        {
            var go = Child(content, "RoundRowTemplate");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, RoundRowHeight);

            var frame = go.AddComponent<RoundedPanelGraphic>();
            frame.Configure(RowFill, RowBorder, 14f, 1.5f, new Color(0f, 0f, 0f, 0f), 0f, 0f,
                new Color(0f, 0f, 0f, 0f), Vector2.zero, 0f);

            var row = go.AddComponent<HistoryRoundRow>();

            float rowLeft = -width * 0.5f;

            var title = Label(go.transform, "Title", RoundTitleSize, TitleColor,
                TextAlignmentOptions.Left, font);
            title.text = "라운드 0";
            Place((RectTransform)title.transform,
                new Vector2(rowLeft + 24f + TitleWidth * 0.5f, 0f),
                new Vector2(TitleWidth, 40f));

            // 카드 셋이 놓이는 띠. 그 왼쪽에 '금액 변화'를 작게 붙인다.
            float cardsWidth = PersonCardSize.x * 3f + PersonCardGap * 2f;
            float cardsRight = width * 0.5f - 24f;
            float cardsLeft = cardsRight - cardsWidth;

            // 세 층의 높이. 카드 아래에 뇌물 한 줄이 끼어들었으므로 판정 줄이 더 내려간다.
            const float CardsY = 40f;
            const float BribeY = -14f;
            const float VerdictY = -50f;

            var hint = Label(go.transform, "ChangeHint", HintSize, HintColor,
                TextAlignmentOptions.Right, font);
            hint.text = "금액 변화";
            Place((RectTransform)hint.transform,
                new Vector2(cardsLeft - 12f - 40f, CardsY), new Vector2(80f, 24f));

            var cards = new HistoryCard[3];
            var bribes = new TextMeshProUGUI[3];
            for (int i = 0; i < 3; i++)
            {
                float x = cardsLeft + PersonCardSize.x * 0.5f
                          + i * (PersonCardSize.x + PersonCardGap);
                cards[i] = PersonCard(go.transform, font, "Card" + i, new Vector2(x, CardsY));

                // 뇌물은 낸 사람 카드 바로 아래에 붙는다 — 이름을 다시 적지 않아도
                // 어느 칸이 누구 것인지 자리로 이어진다.
                bribes[i] = Label(go.transform, "Bribe" + i, BribeSize, HintColor,
                    TextAlignmentOptions.Center, font);
                bribes[i].text = "뇌물 없음";
                Place((RectTransform)bribes[i].transform,
                    new Vector2(x, BribeY), new Vector2(PersonCardSize.x, 24f));
            }

            // 조작·고발은 카드 아래 같은 폭에 둘로 나눠 놓는다. 이미지의 배치와 같다.
            var manipulation = Label(go.transform, "Manipulation", VerdictSize, LabelColor,
                TextAlignmentOptions.Left, font);
            manipulation.text = "조작 여부:  -";
            Place((RectTransform)manipulation.transform,
                new Vector2(cardsLeft + cardsWidth * 0.25f, VerdictY),
                new Vector2(cardsWidth * 0.5f, 28f));

            var accusation = Label(go.transform, "Accusation", VerdictSize, LabelColor,
                TextAlignmentOptions.Left, font);
            accusation.text = "고발 여부:  -";
            Place((RectTransform)accusation.transform,
                new Vector2(cardsLeft + cardsWidth * 0.75f, VerdictY),
                new Vector2(cardsWidth * 0.5f, 28f));

            // 배역이 바뀐 줄에 서는 표식. 라운드 번호 바로 오른쪽에 둔다 — 이름표가 왜
            // 달라졌는지 묻는 사람의 눈은 라운드 번호에서 카드로 넘어가는 길목에 있다.
            var badgeGo = Child(go.transform, "RotateBadge");
            Place((RectTransform)badgeGo.transform,
                new Vector2(rowLeft + 24f + RotateBadgeX, 0f),
                new Vector2(RotateBadgeSize, RotateBadgeSize));
            var badge = badgeGo.AddComponent<RoleRotationBadge>();
            badge.color = RotateBadgeColor;
            badge.raycastTarget = false;
            badgeGo.SetActive(false);

            var so = new SerializedObject(row);
            so.FindProperty("title").objectReferenceValue = title;
            so.FindProperty("rotationBadge").objectReferenceValue = badgeGo;
            so.FindProperty("manipulation").objectReferenceValue = manipulation;
            so.FindProperty("accusation").objectReferenceValue = accusation;
            WriteCards(so.FindProperty("cards"), cards);

            var bribeArray = so.FindProperty("bribes");
            bribeArray.arraySize = bribes.Length;
            for (int i = 0; i < bribes.Length; i++)
                bribeArray.GetArrayElementAtIndex(i).objectReferenceValue = bribes[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            return row;
        }

        // ── 랭킹 줄 ──────────────────────────────────────────────────

        private static HistoryRankRow BuildRankTemplate(
            RectTransform content, TMP_FontAsset font, float width)
        {
            var go = Child(content, "RankRowTemplate");
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, RankRowHeight);

            var frame = go.AddComponent<RoundedPanelGraphic>();
            frame.Configure(RowFill, RowBorder, 14f, 1.5f, new Color(0f, 0f, 0f, 0f), 0f, 0f,
                new Color(0f, 0f, 0f, 0f), Vector2.zero, 0f);

            var row = go.AddComponent<HistoryRankRow>();

            float rowLeft = -width * 0.5f;

            var title = Label(go.transform, "Title", RankTitleSize, TitleColor,
                TextAlignmentOptions.Left, font);
            title.text = "0번째 게임";
            Place((RectTransform)title.transform,
                new Vector2(rowLeft + 24f + TitleWidth * 0.5f, 0f),
                new Vector2(TitleWidth, 40f));

            float cardsWidth = PersonCardSize.x * 3f + PersonCardGap * 2f;
            float cardsRight = width * 0.5f - 24f;
            float cardsLeft = cardsRight - cardsWidth;

            var cards = new HistoryCard[3];
            for (int i = 0; i < 3; i++)
            {
                float x = cardsLeft + PersonCardSize.x * 0.5f
                          + i * (PersonCardSize.x + PersonCardGap);
                cards[i] = PersonCard(go.transform, font, "Card" + i, new Vector2(x, 0f));
            }

            var so = new SerializedObject(row);
            so.FindProperty("title").objectReferenceValue = title;
            WriteCards(so.FindProperty("cards"), cards);
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            return row;
        }

        /// <summary>
        /// 사람 카드 한 장 — 초상화 · 역할 · 금액. 오른쪽 위 머니바 카드와 같은 모습이다.
        /// 색은 실행 중에 그 사람의 캐릭터 색으로 칠해지므로(<see cref="HistoryCard"/>)
        /// 여기서 넣는 색은 자리만 잡아 주는 값이다.
        /// </summary>
        private static HistoryCard PersonCard(
            Transform parent, TMP_FontAsset font, string name, Vector2 position)
        {
            var go = Child(parent, name);
            Place((RectTransform)go.transform, position, PersonCardSize);

            var frame = go.AddComponent<RoundedPanelGraphic>();
            frame.Configure(ZooJackPalette.Hex(0x16202B, 1f), ZooJackPalette.Hex(0x4899CC, 1f), 12f, 2f,
                new Color(0f, 0f, 0f, 0f), 0f, 0f,
                new Color(0f, 0f, 0f, 0f), Vector2.zero, 0f);

            var portraitGo = Child(go.transform, "Portrait");
            Place((RectTransform)portraitGo.transform,
                new Vector2(-PersonCardSize.x * 0.5f + 8f + PortraitSize * 0.5f, 0f),
                new Vector2(PortraitSize, PortraitSize));
            var portrait = portraitGo.AddComponent<Image>();
            portrait.preserveAspect = true;
            portrait.raycastTarget = false;

            // 초상화 오른쪽부터 카드 오른쪽 여백까지가 글자 자리다.
            float textLeft = -PersonCardSize.x * 0.5f + 8f + PortraitSize + 8f;
            float textWidth = PersonCardSize.x * 0.5f - 10f - textLeft;

            var role = Label(go.transform, "Role", RoleSize, Color.white,
                TextAlignmentOptions.Left, font);
            role.text = "역할";
            Place((RectTransform)role.transform,
                new Vector2(textLeft + textWidth * 0.5f, 14f), new Vector2(textWidth, 22f));

            var amount = Label(go.transform, "Amount", AmountSize, Color.white,
                TextAlignmentOptions.Left, font);
            amount.text = "±0";
            Place((RectTransform)amount.transform,
                new Vector2(textLeft + textWidth * 0.5f, -12f), new Vector2(textWidth, 28f));

            return new HistoryCard
            {
                Frame = frame,
                Portrait = portrait,
                RoleLabel = role,
                Amount = amount
            };
        }

        private static void WriteCards(SerializedProperty array, HistoryCard[] cards)
        {
            array.arraySize = cards.Length;
            for (int i = 0; i < cards.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("Frame").objectReferenceValue = cards[i].Frame;
                element.FindPropertyRelative("Portrait").objectReferenceValue = cards[i].Portrait;
                element.FindPropertyRelative("RoleLabel").objectReferenceValue = cards[i].RoleLabel;
                element.FindPropertyRelative("Amount").objectReferenceValue = cards[i].Amount;
            }
        }

        // ── 배선 ─────────────────────────────────────────────────────

        private static void Wire(
            HistoryPanel panel, GameObject window, Wired w, Borrowed borrowed)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("window").objectReferenceValue = window;
            so.FindProperty("scrim").objectReferenceValue = w.Scrim;
            so.FindProperty("btnOpen").objectReferenceValue = w.Open;
            so.FindProperty("btnClose").objectReferenceValue = w.Close;

            so.FindProperty("btnRoundTab").objectReferenceValue = w.RoundTab;
            so.FindProperty("btnRankTab").objectReferenceValue = w.RankTab;
            so.FindProperty("roundTabFrame").objectReferenceValue = w.RoundTabFrame;
            so.FindProperty("rankTabFrame").objectReferenceValue = w.RankTabFrame;
            so.FindProperty("roundTabLabel").objectReferenceValue = w.RoundTabLabel;
            so.FindProperty("rankTabLabel").objectReferenceValue = w.RankTabLabel;

            so.FindProperty("roundContent").objectReferenceValue = w.RoundContent;
            so.FindProperty("roundTemplate").objectReferenceValue = w.RoundTemplate;
            so.FindProperty("roundEmpty").objectReferenceValue = w.RoundEmpty;
            so.FindProperty("roundScroll").objectReferenceValue = w.RoundScroll;

            so.FindProperty("rankContent").objectReferenceValue = w.RankContent;
            so.FindProperty("rankTemplate").objectReferenceValue = w.RankTemplate;
            so.FindProperty("rankEmpty").objectReferenceValue = w.RankEmpty;
            so.FindProperty("rankScroll").objectReferenceValue = w.RankScroll;

            so.FindProperty("portraitRabbit").objectReferenceValue = borrowed.Rabbit;
            so.FindProperty("portraitFox").objectReferenceValue = borrowed.Fox;
            so.FindProperty("portraitCroc").objectReferenceValue = borrowed.Croc;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>게임에서 빌려 오는 그림들. 로비에서 만들 때도 같은 것을 써야 한다.</summary>
        private struct Borrowed
        {
            public TMP_SpriteAsset Coin;
            public Sprite Rabbit, Fox, Croc;
        }

        /// <summary>
        /// 초상화와 금화 그림 묶음을 <b>게임이 쓰는 그 자리</b>에서 빌린다. 경로로 다시 찾으면
        /// 그림을 갈아 끼울 때 이 화면만 옛 그림으로 남는다.
        ///
        /// 로비에서 만드는 경우에는 게임 씬이 열려 있지 않으므로 <see cref="GameSceneLoan"/>이
        /// 잠깐 겹쳐 열어 준다.
        /// </summary>
        private static Borrowed BorrowFromGame()
        {
            var got = default(Borrowed);

            GameSceneLoan.While(() =>
            {
                var director = Object.FindFirstObjectByType<GameDirector>(FindObjectsInactive.Include);
                if (director == null)
                {
                    Debug.LogWarning("[HistoryPanelSetupTool] GameDirector를 찾지 못해 초상화와 " +
                                     "금화 그림을 비워 두었습니다. 인스펙터에서 직접 넣어 주세요.");
                    return;
                }

                var from = new SerializedObject(director);
                got.Rabbit = from.FindProperty("portraitRabbit")?.objectReferenceValue as Sprite;
                got.Fox = from.FindProperty("portraitFox")?.objectReferenceValue as Sprite;
                got.Croc = from.FindProperty("portraitCroc")?.objectReferenceValue as Sprite;
                got.Coin = BorrowSpriteAsset(from);
            });

            return got;
        }

        // ── 잔손질 ───────────────────────────────────────────────────

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static RoundedPanelGraphic Frame(
            GameObject go, Color fill, Color border, float radius, float thickness)
        {
            var frame = go.AddComponent<RoundedPanelGraphic>();
            frame.Configure(fill, border, radius, thickness,
                new Color(0f, 0f, 0f, 0f), 0f, 0f,
                Shadow, new Vector2(0f, -3f), 2f);
            frame.raycastTarget = true;
            return frame;
        }

        private static Button TextButton(
            Transform parent, TMP_FontAsset font, string name, string text, Color fill,
            Vector2 position, Vector2 size)
        {
            var go = Child(parent, name);
            Place((RectTransform)go.transform, position, size);

            var frame = Frame(go, fill, CardBorder, 8f, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;

            var label = Label(go.transform, "Label", ButtonTextSize, TitleColor,
                TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            label.text = text;

            return button;
        }

        private static TextMeshProUGUI Label(
            Transform parent, string name, float size, Color color,
            TextAlignmentOptions align, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            if (coinSprites != null) label.spriteAsset = coinSprites;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// 씬이 이미 쓰고 있는 글꼴을 그대로 빌린다. 경로로 지정해 두면 글꼴을 바꿀 때
        /// 이 화면만 옛 글꼴로 남는다.
        /// </summary>
        private static TMP_FontAsset BorrowFont(Transform canvas)
        {
            foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.font != null) return text.font;
            return null;
        }

        /// <summary>
        /// 금화가 든 그림 묶음을 <b>머니바의 잔액 글자에서</b> 빌린다. 그쪽이 이 화면과
        /// 똑같은 표기(<c>1,000 &lt;sprite index=0&gt;</c>)를 쓰므로 0번이 금화임이 보장된다.
        ///
        /// 아무 글자에서나 빌리면 안 된다 — 씬에는 칩 그림 묶음을 쓰는 글자도 있어서,
        /// 처음 찾은 것을 집으면 금액 옆에 녹색 칩이 붙는다(실제로 그랬다).
        /// </summary>
        private static TMP_SpriteAsset BorrowSpriteAsset(SerializedObject director)
        {
            var cards = director.FindProperty("moneyCards");
            for (int i = 0; cards != null && i < cards.arraySize; i++)
            {
                var balance = cards.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("Balance").objectReferenceValue as TextMeshProUGUI;
                if (balance != null && balance.spriteAsset != null) return balance.spriteAsset;
            }

            Debug.LogWarning("[HistoryPanelSetupTool] 머니바에서 금화 그림 묶음을 찾지 못했습니다. " +
                             "금액 옆에 엉뚱한 그림이 붙으면 머니바 잔액 글자의 Sprite Asset을 " +
                             "이 화면의 금액 글자에도 넣어 주세요.");
            return null;
        }
    }
}
