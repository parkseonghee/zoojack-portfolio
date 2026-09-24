using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 놀이법 설명서(<see cref="HowToPlayPanel"/>)를 열려 있는 씬의 Canvas에 만든다.
    ///
    /// '기록' 버튼 바로 아래에 '설명서' 버튼을 두고, 누르면 화면 가운데 펼친 책이 뜬다.
    /// 좌우 화살표로 장을 넘긴다.
    ///
    /// <b>쪽은 만들지 않는다.</b> 종이는 여섯 장뿐이고(펼친 두 장 + 넘어가는 장의 앞뒤
    /// 네 장) 내용은 패널의 <c>pages</c> 배열에서 갈아 끼운다. 쪽을 늘리려면 그 배열만
    /// 늘리면 되고 씬은 손대지 않아도 된다.
    ///
    /// 이미 있으면 지우고 새로 만든다 — 값이 반쯤 바뀐 옛 화면이 남는 것보다 낫다.
    /// 쪽 내용도 함께 다시 채운다(<see cref="HowToPlayContentTool"/>). 인스펙터에서
    /// 손으로 고쳐 둔 것이 있다면 그것만 사라지므로, 고칠 것은 그쪽 도구에 적는다.
    /// </summary>
    public static class HowToPlaySetupTool
    {
        private const string RootName = "ZJ_HowToPlay";

        // ── 자리와 크기 ──────────────────────────────────────────────
        // 왼쪽 위 여는 버튼의 자리는 OpenButtonColumn이 정한다 — 셋이 한 열에 선다.

        /// <summary>책 겉장. 화면(1920×1080) 가운데에 여백을 넉넉히 남긴다.</summary>
        private static readonly Vector2 BookSize = new Vector2(1180f, 720f);

        /// <summary>겉장이 종이 바깥으로 나온 폭. 책배 선이 이 띠 안에 들어간다.</summary>
        private const float CoverPadding = 34f;

        /// <summary>종이 안쪽 여백. 내용은 이 안에만 들어간다.</summary>
        private const float PagePadding = 30f;

        private const float SpineWidth = 8f;

        // 책배 — 아직 넘기지 않은 장을 옆에서 본 선.
        private const int EdgeLineCount = 5;
        private const float EdgeLineWidth = 3f;
        private const float EdgeLineGap = 4f;

        /// <summary>종이 끝에서 첫 선까지의 틈. 겉장 안쪽 테두리를 타고 넘지 않을 만큼.</summary>
        private const float EdgeInset = 4f;

        /// <summary>한 겹 바깥으로 갈 때마다 줄어드는 길이. 부챗살처럼 보이게 한다.</summary>
        private const float EdgeLineTaper = 16f;

        private static readonly Vector2 ArrowSize = new Vector2(72f, 96f);

        /// <summary>화살표를 겉장에서 얼마나 띄울지. 책 바깥에 두면 내용과 겹치지 않는다.</summary>
        private const float ArrowMargin = 46f;

        // ── 색 ───────────────────────────────────────────────────────
        // 겉장은 게임의 다른 카드와 같은 금빛 테두리를 쓰고 안쪽만 가죽색이다.
        // 종이는 반대로 밝다 — 책은 읽는 물건이라 어두운 종이는 글자를 밀어낸다.

        private static readonly Color BookBrown = ZooJackPalette.Hex(0x4A3524);
        private static readonly Color BookInk = ZooJackPalette.Hex(0x2A1E14);
        private static readonly Color CoverFill = BookBrown.Alpha(0.99f);
        private static readonly Color CoverBorder = ZooJackPalette.Gold;
        private static readonly Color CoverInner = BookInk.Alpha(0.9f);
        private static readonly Color Shadow = ZooJackPalette.Shadow;
        private static readonly Color ScrimColor = ZooJackPalette.Scrim;

        private static readonly Color PaperFill = ZooJackPalette.Hex(0xEDE3CC, 1f);
        private static readonly Color PaperBorder = ZooJackPalette.Hex(0xD3C4A2, 1f);
        private static readonly Color SpineColor = BookInk.Alpha(0.85f);
        private static readonly Color EdgeColor = ZooJackPalette.Hex(0xD8CCB4, 1f);

        private static readonly Color InkColor = ZooJackPalette.Hex(0x3A2E1F, 1f);
        private static readonly Color PageNumberColor = ZooJackPalette.Hex(0x9A8A6C, 1f);
        private static readonly Color RowCaptionColor = ZooJackPalette.Hex(0x5C4B33, 1f);

        private static readonly Color ButtonFill = ZooJackPalette.Graphite.Alpha(0.98f);
        private static readonly Color CloseFill = ZooJackPalette.Forest.Alpha(0.98f);
        private static readonly Color ArrowFill = BookBrown.Alpha(0.98f);
        private static readonly Color TitleColor = ZooJackPalette.Cream;

        // ── 글자 크기 ────────────────────────────────────────────────

        private const float ButtonTextSize = 22f;
        private const float ArrowTextSize = 34f;
        private const float PageTitleSize = 40f;
        private const float PageBodySize = 22f;
        private const float PageNumberSize = 17f;
        private const float CounterSize = 20f;
        private const float RowCaptionSize = 19f;
        private const float RowSymbolSize = 30f;

        /// <summary>덩어리 사이 틈.</summary>
        private const float BlockSpacing = 16f;

        /// <summary>그림 줄에서 것들 사이 틈.</summary>
        private const float ItemSpacing = 12f;

        /// <summary>그림 줄의 기본 높이. 카드 세 장이 한 줄에 넉넉히 들어간다.</summary>
        private const float DefaultRowHeight = 150f;

        /// <summary>쪽 아래에 쪽수를 위해 비워 두는 띠.</summary>
        private const float PageNumberBand = 26f;

        /// <summary>처음 만들어 두는 쪽 수. 두 쪽이 한 펼침면이라 네 면이 된다.</summary>
        private const int StartingPageCount = 8;

        [MenuItem("ZooJack/게임/설명서 만들기")]
        public static void Setup()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("설명서",
                    "열려 있는 씬에 Canvas가 없습니다. 게임 씬을 먼저 여세요.", "확인");
                return;
            }

            var font = BorrowFont(canvas.transform);
            var host = PanelHost.For(canvas);

            var existing = host.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = Child(host, RootName);
            Stretch((RectTransform)root.transform);
            root.transform.SetAsLastSibling();

            var panel = root.AddComponent<HowToPlayPanel>();

            var wired = default(Wired);
            wired.Open = BuildOpenButton(root.transform, font);
            var window = BuildWindow(root.transform, font, ref wired);

            Wire(panel, window, wired);
            window.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[HowToPlaySetupTool] '{EditorSceneManager.GetActiveScene().name}' 씬에 " +
                      $"설명서를 만들었습니다. 빈 쪽 {StartingPageCount}장이 들어 있습니다 — " +
                      "내용은 HowToPlayPanel의 '내용' 배열에 채우세요.");

            Selection.activeGameObject = root;
        }

        /// <summary>도구가 만든 위젯 묶음. 이름으로 다시 찾지 않는다.</summary>
        private struct Wired
        {
            public Button Open, Close, Scrim, Prev, Next;
            public HowToPlayPageView LeftPage, RightPage;
            public RectTransform FlipForward, FlipBackward;
            public HowToPlayPageView ForwardFront, ForwardBack, BackwardFront, BackwardBack;
            public RectTransform LeftEdges, RightEdges;
            public TextMeshProUGUI Counter;
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
                OpenButtonColumn.Slot.HowToPlay);

            var frame = Frame(go, ButtonFill, CoverBorder, 10f, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;

            var label = Label(go.transform, "Label", ButtonTextSize, TitleColor,
                TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            label.text = "설명서";
            OpenButtonIconUtility.Apply(
                go.transform, label,
                "Assets/ImportedAsset/UI Image/book.png",
                new Vector2(-2.6f, 0f));

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

            var book = Child(window.transform, "Book");
            Place((RectTransform)book.transform, Vector2.zero, BookSize);

            var cover = book.AddComponent<RoundedPanelGraphic>();
            cover.Configure(CoverFill, CoverBorder, 22f, 3f, CoverInner, 12f, 1.5f,
                Shadow, new Vector2(0f, -8f), 6f);
            cover.raycastTarget = true;   // 책 위를 눌렀을 때 뒤의 판이 닫히지 않게

            BuildPages(book.transform, font, ref wired);

            // 닫기는 책 위쪽 바깥에. 겉장 띠는 34뿐이라 버튼이 들어가지 않고,
            // 종이 안에 두면 나중에 내용이 들어올 자리를 먹는다.
            float bookTop = BookSize.y * 0.5f;
            wired.Close = TextButton(book.transform, font, "btnClose", "닫기", CloseFill,
                new Vector2(BookSize.x * 0.5f - 60f, bookTop + 36f),
                new Vector2(120f, 44f));

            // 화살표는 책 바깥 양옆. 표지 위에 얹으면 쪽 내용과 겹친다.
            float arrowX = BookSize.x * 0.5f + ArrowMargin + ArrowSize.x * 0.5f;
            wired.Prev = ArrowButton(book.transform, font, "btnPrev", "◀",
                new Vector2(-arrowX, 0f));
            wired.Next = ArrowButton(book.transform, font, "btnNext", "▶",
                new Vector2(arrowX, 0f));

            wired.Counter = Label(book.transform, "Counter", CounterSize, TitleColor,
                TextAlignmentOptions.Center, font);
            Place((RectTransform)wired.Counter.transform,
                new Vector2(0f, -bookTop - 34f), new Vector2(300f, 36f));
            wired.Counter.text = "1 / 1";

            return window;
        }

        // ── 종이 ─────────────────────────────────────────────────────

        private static void BuildPages(Transform book, TMP_FontAsset font, ref Wired wired)
        {
            var area = new Vector2(BookSize.x - CoverPadding * 2f, BookSize.y - CoverPadding * 2f);
            var pageSize = new Vector2(area.x * 0.5f, area.y);

            // 책배가 가장 뒤다 — 종이가 그 위에 얹혀야 옆에서 본 선처럼 보인다.
            // 종이 바깥 끝(pageSize.x)과 겉장 사이 띠 안에 들어가야 한다.
            // 넘치면 겉장의 안쪽 테두리를 타고 넘어 스크롤바처럼 보인다.
            float edgeX = pageSize.x + EdgeInset + (EdgeLineCount - 1) * EdgeLineGap * 0.5f;
            wired.LeftEdges = EdgeStack(book, "LeftEdges", -edgeX, pageSize.y - 40f, -1f);
            wired.RightEdges = EdgeStack(book, "RightEdges", edgeX, pageSize.y - 40f, 1f);

            // 펼쳐 놓은 두 장. 책등(x=0)에서 맞닿는다 — 사이를 벌려 두면 넘어가는 장이
            // 도는 축과 어긋나 마지막에 종이가 한 뼘 튄다.
            wired.LeftPage = Page(book, font, "LeftPage", pageSize, left: true);
            wired.RightPage = Page(book, font, "RightPage", pageSize, left: false);

            var spine = Child(book, "Spine");
            Place((RectTransform)spine.transform, Vector2.zero, new Vector2(SpineWidth, area.y));
            var spineImage = spine.AddComponent<Image>();
            spineImage.color = SpineColor;
            spineImage.raycastTarget = false;

            // 넘어가는 장. 책등을 축으로 돌아야 하므로 pivot이 책등 쪽 모서리에 있고,
            // 자리는 정확히 x=0이다.
            wired.FlipBackward = FlipLeaf(book, font, "FlipBackward", pageSize, left: true,
                out wired.BackwardFront, out wired.BackwardBack);
            wired.FlipForward = FlipLeaf(book, font, "FlipForward", pageSize, left: false,
                out wired.ForwardFront, out wired.ForwardBack);
        }

        /// <summary>
        /// 도는 장 하나. 앞면과 뒷면이 겹쳐 있고, <b>뒷면은 좌우로 뒤집어</b> 둔다 —
        /// 90도를 넘으면 종이 자체가 뒤집혀 보이므로 그대로 두면 글자가 거울처럼 나온다.
        /// </summary>
        private static RectTransform FlipLeaf(
            Transform book, TMP_FontAsset font, string name, Vector2 pageSize, bool left,
            out HowToPlayPageView front, out HowToPlayPageView back)
        {
            var go = Child(book, name);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            rect.sizeDelta = pageSize;
            rect.anchoredPosition = Vector2.zero;

            // 앞면은 책등이 도는 축과 같은 쪽에 있다. 뒷면은 좌우로 뒤집혀 있어
            // 제 자리에서 보면 책등이 반대쪽에 있는 것처럼 보인다 — 그늘도 그래야
            // 접히는 자리가 짙어진다.
            front = Paper(go.transform, font, "Front", pageSize, left, hingeOnRight: left);
            back = Paper(go.transform, font, "Back", pageSize, left, hingeOnRight: !left);

            // 뒷면은 반대쪽 면이므로 쪽수도 반대편 구석에 와야 한다. 좌우를 뒤집으면
            // 그 자리까지 함께 옮겨진다.
            back.transform.localScale = new Vector3(-1f, 1f, 1f);

            return rect;
        }

        /// <summary>펼쳐 놓은 장 하나. 책등에 붙여 세운다.</summary>
        private static HowToPlayPageView Page(
            Transform book, TMP_FontAsset font, string name, Vector2 pageSize, bool left)
        {
            var go = Child(book, name);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(left ? 1f : 0f, 0.5f);
            rect.sizeDelta = pageSize;
            rect.anchoredPosition = Vector2.zero;

            // 펼쳐 놓은 장에는 그늘이 지지 않지만, 책등 쪽은 정해 둔다.
            return Fill(go, font, pageSize, left, hingeOnRight: left);
        }

        /// <summary>도는 장 안쪽의 종이 한 면. 부모를 꽉 채운다.</summary>
        private static HowToPlayPageView Paper(
            Transform parent, TMP_FontAsset font, string name, Vector2 pageSize,
            bool left, bool hingeOnRight)
        {
            var go = Child(parent, name);
            Stretch((RectTransform)go.transform);
            return Fill(go, font, pageSize, left, hingeOnRight);
        }

        /// <summary>
        /// 종이에 부품을 붙인다 — 바탕 · 제목 · 본문 · 그림 · 쪽수 · 그늘.
        /// 쪽수는 책등 반대쪽(바깥쪽) 아래 구석이다.
        /// </summary>
        private static HowToPlayPageView Fill(
            GameObject go, TMP_FontAsset font, Vector2 pageSize, bool left, bool hingeOnRight)
        {
            var paper = go.AddComponent<RoundedPanelGraphic>();
            paper.Configure(PaperFill, PaperBorder, 6f, 1f,
                new Color(0f, 0f, 0f, 0f), 0f, 0f,
                new Color(0f, 0f, 0f, 0f), Vector2.zero, 0f);
            paper.raycastTarget = true;

            var view = go.AddComponent<HowToPlayPageView>();

            float innerWidth = pageSize.x - PagePadding * 2f;

            // 덩어리가 쌓이는 자리. 아래로는 쪽수 자리를 비워 둔다.
            float contentHeight = pageSize.y - PagePadding * 2f - PageNumberBand;
            float contentTop = pageSize.y * 0.5f - PagePadding;
            var content = Child(go.transform, "Content");
            Place((RectTransform)content.transform,
                new Vector2(0f, contentTop - contentHeight * 0.5f),
                new Vector2(innerWidth, contentHeight));

            var stack = content.AddComponent<VerticalLayoutGroup>();
            stack.spacing = BlockSpacing;

            // 위로 붙이지 않고 가운데에 모은다. 쪽마다 덩어리 수가 다른데 위로 붙이면
            // 덩어리가 적은 쪽은 아래 절반이 통째로 비어 쪽이 잘린 것처럼 보인다.
            stack.childAlignment = TextAnchor.MiddleCenter;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            var heading = BuildHeadingTemplate(content.transform, font);
            var row = BuildRowTemplate(content.transform, font);
            var caption = BuildCaptionTemplate(content.transform, font);

            // 쪽수는 바깥쪽 구석. 책등 쪽에 두면 두 쪽의 숫자가 가운데서 붙어 버린다.
            float numberX = (innerWidth * 0.5f - 10f) * (left ? -1f : 1f);
            var number = Label(go.transform, "PageNumber", PageNumberSize, PageNumberColor,
                left ? TextAlignmentOptions.Left : TextAlignmentOptions.Right, font);
            number.text = "1";
            Place((RectTransform)number.transform,
                new Vector2(numberX, -pageSize.y * 0.5f + PagePadding * 0.6f),
                new Vector2(60f, 24f));

            // 그늘은 맨 위에 덮인다. 짙기는 실행 중에 정해지므로 여기서는 0이다.
            var shadeGo = Child(go.transform, "Shade");
            Stretch((RectTransform)shadeGo.transform);
            shadeGo.AddComponent<CanvasRenderer>();
            var shade = shadeGo.AddComponent<PageShadeGraphic>();
            shade.color = new Color(0.10f, 0.07f, 0.04f, 1f);
            shade.SetHinge(hingeOnRight);
            shade.SetStrength(0f);
            shade.raycastTarget = false;

            var so = new SerializedObject(view);
            so.FindProperty("content").objectReferenceValue = content.transform;
            so.FindProperty("headingTemplate").objectReferenceValue = heading;
            so.FindProperty("rowTemplate").objectReferenceValue = row;
            so.FindProperty("captionTemplate").objectReferenceValue = caption;
            so.FindProperty("pageNumber").objectReferenceValue = number;
            so.FindProperty("shade").objectReferenceValue = shade;
            so.FindProperty("defaultRowHeight").floatValue = DefaultRowHeight;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        /// <summary>쪽 제목 본보기. 쪽마다 하나면 충분하다.</summary>
        private static TextMeshProUGUI BuildHeadingTemplate(Transform content, TMP_FontAsset font)
        {
            var heading = Label(content, "HeadingTemplate", PageTitleSize, InkColor,
                TextAlignmentOptions.Center, font);
            heading.fontStyle = FontStyles.Bold;
            heading.text = "제목";

            var layout = heading.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = PageTitleSize * 1.5f;

            heading.gameObject.SetActive(false);
            return heading;
        }

        /// <summary>짧은 글 본보기. 높이는 글 길이에 따라 TMP가 스스로 정한다.</summary>
        private static TextMeshProUGUI BuildCaptionTemplate(Transform content, TMP_FontAsset font)
        {
            var caption = Label(content, "CaptionTemplate", PageBodySize, InkColor,
                TextAlignmentOptions.Top, font);
            caption.text = "설명";
            caption.lineSpacing = 10f;

            caption.gameObject.SetActive(false);
            return caption;
        }

        /// <summary>
        /// 그림 줄 본보기 — 가로로 늘어서는 자리와, 그 아래 한 줄 설명.
        /// 늘어서는 것의 개수는 쪽마다 다르므로 안에 <b>본보기 하나</b>만 넣어 둔다.
        /// </summary>
        private static HowToPlayRowView BuildRowTemplate(Transform content, TMP_FontAsset font)
        {
            var go = Child(content, "RowTemplate");
            var row = go.AddComponent<HowToPlayRowView>();

            var stack = go.AddComponent<VerticalLayoutGroup>();
            stack.spacing = 8f;
            stack.childAlignment = TextAnchor.UpperCenter;
            stack.childControlWidth = true;
            stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;

            var itemsGo = Child(go.transform, "Items");
            var line = itemsGo.AddComponent<HorizontalLayoutGroup>();
            line.spacing = ItemSpacing;
            line.childAlignment = TextAnchor.MiddleCenter;
            line.childControlWidth = true;
            line.childControlHeight = true;
            line.childForceExpandWidth = false;
            line.childForceExpandHeight = false;

            // 줄 높이는 실행 중에 덩어리가 정한다. 여기 값은 씬에서 볼 때의 자리만 잡는다.
            var itemsLayout = itemsGo.AddComponent<LayoutElement>();
            itemsLayout.preferredHeight = DefaultRowHeight;

            var item = BuildItemTemplate(itemsGo.transform, font);

            var caption = Label(go.transform, "Caption", RowCaptionSize, RowCaptionColor,
                TextAlignmentOptions.Top, font);
            caption.text = "한 줄 설명";

            var so = new SerializedObject(row);
            so.FindProperty("items").objectReferenceValue = itemsGo.transform;
            so.FindProperty("itemsLayout").objectReferenceValue = itemsLayout;
            so.FindProperty("itemTemplate").objectReferenceValue = item;
            so.FindProperty("caption").objectReferenceValue = caption;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            return row;
        }

        /// <summary>줄에 놓이는 것 하나의 본보기 — 그림이거나 기호다.</summary>
        private static HowToPlayItemView BuildItemTemplate(Transform items, TMP_FontAsset font)
        {
            var go = Child(items, "ItemTemplate");
            var view = go.AddComponent<HowToPlayItemView>();

            var icon = go.AddComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = DefaultRowHeight;
            layout.preferredWidth = DefaultRowHeight * 0.72f;

            var symbol = Label(go.transform, "Symbol", RowSymbolSize, RowCaptionColor,
                TextAlignmentOptions.Center, font);
            Stretch((RectTransform)symbol.transform);
            symbol.text = "→";

            var so = new SerializedObject(view);
            so.FindProperty("icon").objectReferenceValue = icon;
            so.FindProperty("symbol").objectReferenceValue = symbol;
            so.FindProperty("layout").objectReferenceValue = layout;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(false);
            return view;
        }

        /// <summary>
        /// 책배 — 아직 넘기지 않은 장을 옆에서 본 선 묶음.
        /// <paramref name="direction"/>은 바깥쪽(+1 오른쪽, -1 왼쪽)이다.
        /// </summary>
        private static RectTransform EdgeStack(
            Transform book, string name, float x, float height, float direction)
        {
            var go = Child(book, name);
            Place((RectTransform)go.transform, new Vector2(x, 0f),
                new Vector2(EdgeLineCount * EdgeLineGap, height));

            for (int i = 0; i < EdgeLineCount; i++)
            {
                var line = Child(go.transform, "Edge" + i);

                // 0번이 펼친 쪽에 가장 가깝고 마지막이 책 바깥 끝이다. 켜는 순서는
                // HowToPlayPanel이 정한다 — 넘어가는 것은 맨 앞의 장이므로 안쪽부터 꺼진다.
                //
                // 안쪽일수록 길다. 맨 앞의 장이 가장 길게 보이고, 넘기면 그 긴 선이
                // 사라지면서 바로 다음 선이 맨 앞이 된다 — 길이가 곧 차례다.
                float offset = (i - (EdgeLineCount - 1) * 0.5f) * EdgeLineGap;
                Place((RectTransform)line.transform,
                    new Vector2(direction * offset, 0f),
                    new Vector2(EdgeLineWidth, height - i * EdgeLineTaper));

                var image = line.AddComponent<Image>();
                image.color = EdgeColor;
                image.raycastTarget = false;
            }

            return (RectTransform)go.transform;
        }

        // ── 버튼 ─────────────────────────────────────────────────────

        private static Button ArrowButton(
            Transform parent, TMP_FontAsset font, string name, string glyph, Vector2 position)
        {
            var go = Child(parent, name);
            Place((RectTransform)go.transform, position, ArrowSize);

            var frame = Frame(go, ArrowFill, CoverBorder, 14f, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;

            var label = Label(go.transform, "Label", ArrowTextSize, TitleColor,
                TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            label.text = glyph;

            return button;
        }

        private static Button TextButton(
            Transform parent, TMP_FontAsset font, string name, string text, Color fill,
            Vector2 position, Vector2 size)
        {
            var go = Child(parent, name);
            Place((RectTransform)go.transform, position, size);

            var frame = Frame(go, fill, CoverBorder, 8f, 2f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = frame;

            var label = Label(go.transform, "Label", ButtonTextSize, TitleColor,
                TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            label.text = text;

            return button;
        }

        // ── 배선 ─────────────────────────────────────────────────────

        private static void Wire(HowToPlayPanel panel, GameObject window, Wired wired)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("window").objectReferenceValue = window;
            so.FindProperty("scrim").objectReferenceValue = wired.Scrim;
            so.FindProperty("btnOpen").objectReferenceValue = wired.Open;
            so.FindProperty("btnClose").objectReferenceValue = wired.Close;
            so.FindProperty("btnPrev").objectReferenceValue = wired.Prev;
            so.FindProperty("btnNext").objectReferenceValue = wired.Next;

            so.FindProperty("leftPage").objectReferenceValue = wired.LeftPage;
            so.FindProperty("rightPage").objectReferenceValue = wired.RightPage;

            so.FindProperty("flipForward").objectReferenceValue = wired.FlipForward;
            so.FindProperty("flipForwardFront").objectReferenceValue = wired.ForwardFront;
            so.FindProperty("flipForwardBack").objectReferenceValue = wired.ForwardBack;
            so.FindProperty("flipBackward").objectReferenceValue = wired.FlipBackward;
            so.FindProperty("flipBackwardFront").objectReferenceValue = wired.BackwardFront;
            so.FindProperty("flipBackwardBack").objectReferenceValue = wired.BackwardBack;

            so.FindProperty("leftEdges").objectReferenceValue = wired.LeftEdges;
            so.FindProperty("rightEdges").objectReferenceValue = wired.RightEdges;
            so.FindProperty("spreadCounter").objectReferenceValue = wired.Counter;
            // 카드 그림도 쪽 내용의 초상화도 게임 씬에서 빌려 온다. 한 번의 대여 안에서
            // 끝내야 로비에서 만들 때 게임 씬을 두 번 열지 않는다 — 내용 채우기도 제
            // 대여를 걸지만, 대여가 겹치면 안쪽은 아무 일도 하지 않는다.
            GameSceneLoan.While(() =>
            {
                so.FindProperty("cardSprites").objectReferenceValue = BorrowCardSprites();

                // 빈 쪽을 깔아 둔다. 배열이 비어 있으면 펼침면이 하나뿐이라 화살표가
                // 둘 다 사라져, 만들어 놓고도 넘어가는지 확인할 수 없다.
                so.FindProperty("pages").arraySize = StartingPageCount;

                so.ApplyModifiedPropertiesWithoutUndo();

                // 그 위에 내용을 덮는다. 빈 책이 남으면 이 화면이 고장 난 것인지
                // 아직 안 쓴 것인지 구별할 수 없다.
                HowToPlayContentTool.Apply(panel);
            });
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
            return label;
        }

        /// <summary>
        /// 게임이 쓰는 카드 그림 묶음을 <b>씬의 카드에서</b> 빌린다. 경로로 지정해 두면
        /// 묶음을 옮겼을 때 설명서만 조용히 빈 카드가 된다.
        /// </summary>
        private static CardSpriteRegistry BorrowCardSprites()
        {
            foreach (var card in Object.FindObjectsByType<CardView>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var registry = new SerializedObject(card)
                    .FindProperty("registry").objectReferenceValue as CardSpriteRegistry;
                if (registry != null) return registry;
            }

            Debug.LogWarning("[HowToPlaySetupTool] 씬에서 카드 그림 묶음을 찾지 못했습니다. " +
                             "설명서의 '카드 그림'을 직접 넣어 주세요.");
            return null;
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
    }
}
