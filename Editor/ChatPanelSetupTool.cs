using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace ZooJack.Editor
{
    /// <summary>
    /// 화면 오른쪽 위의 채팅(<see cref="ChatPanel"/>)을 만든다.
    ///
    /// <b>창이 아니라 겹쳐 놓은 글이다.</b> 평소에는 오간 말만 테이블 위에 얹혀 있다가
    /// 옅어지고, Enter를 눌러야 반투명 판과 입력칸이 선다. 그래서 제목 줄도 전송 버튼도
    /// 숨기기 막대도 없다 — 늘 떠 있는 것이 없으니 늘 눌러야 할 것도 없다.
    ///
    /// <b>씬을 가리지 않는다.</b> 붙을 자리는 <see cref="PanelHost"/>가 정한다 —
    /// 게임 씬에서는 Canvas 바로 아래, 로비에서는 방 화면 안이다. 그래서 같은 메뉴로
    /// 두 곳에 같은 채팅이 선다.
    ///
    /// 이미 있으면 지우고 새로 만든다 — 값이 반쯤 바뀐 옛 화면이 남는 것보다 낫다.
    /// </summary>
    public static class ChatPanelSetupTool
    {
        private const string RootName = "ZJ_Chat";

        // ── 자리와 크기 ──────────────────────────────────────────────

        /// <summary>두 씬에서 같은 화면 위치를 쓰는 오른쪽 위 기준 좌표.</summary>
        private static readonly Vector2 PanelPosition = new Vector2(-14f, -114f);

        private static readonly Vector2 PanelSize = new Vector2(560f, 300f);

        /// <summary>입력칸 한 줄의 높이. 그 위가 전부 오간 말이다.</summary>
        private const float InputHeight = 38f;

        /// <summary>입력칸과 말 사이의 틈.</summary>
        private const float Gap = 6f;

        /// <summary>반투명 판 안쪽에서 글이 물러나는 거리.</summary>
        private const float LogPadding = 12f;

        // ── 색 ───────────────────────────────────────────────────────
        // '라운드 준비' 카드(ZJ_WaitingCard)의 색을 쓰되 투명하게 눕힌다. 같은 초록이라야
        // 화면마다 조금씩 다른 어둠이 생기지 않는다.

        /// <summary>
        /// 글 뒤에 까는 판. <b>불투명하면 안 된다</b> — 이 판이 서는 동안에도 판돈과
        /// 캐릭터가 그 뒤에서 움직이고 있고, 치는 사람도 그것을 보면서 답한다.
        /// </summary>
        private static readonly Color BackdropFill = ZooJackPalette.Hex(0x06150F, 0.55f);

        private static readonly Color CardInner = ZooJackPalette.Hex(0x9E6E2B, 0.878f);

        /// <summary>입력칸은 판보다 한 톤 진해야 '여기 쓰는 곳'으로 읽힌다.</summary>
        private static readonly Color InputFill = ZooJackPalette.Hex(0x0B2318, 0.88f);

        private static readonly Color TitleColor = ZooJackPalette.Hex(0xF4E8CB, 1f);
        private static readonly Color BodyColor = ZooJackPalette.Hex(0xDCD2BA, 1f);
        private static readonly Color HintColor = ZooJackPalette.Hex(0x7E7361, 1f);

        private const float BodySize = 17f;

        private const float BackdropRadius = 14f;
        private const float InputRadius = 10f;

        [MenuItem("ZooJack/게임/채팅 화면 만들기")]
        public static void Setup()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("채팅 화면",
                    "열려 있는 씬에 Canvas가 없습니다. 게임 씬이나 로비 씬을 먼저 여세요.", "확인");
                return;
            }

            var font = BorrowFont(canvas.transform);
            var host = PanelHost.For(canvas);

            var existing = host.Find(RootName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var root = Child(host, RootName);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = rootRect.anchorMax = Vector2.one;
            rootRect.pivot = Vector2.one;
            rootRect.sizeDelta = PanelSize;
            rootRect.anchoredPosition = PanelPosition;

            var group = root.AddComponent<CanvasGroup>();
            group.interactable = true;
            group.blocksRaycasts = false;   // 치지 않는 동안에는 클릭을 흘려보낸다

            var panel = root.AddComponent<ChatPanel>();

            // 판이 먼저 서야 글보다 뒤에 그려진다. 형제 순서가 곧 앞뒤다.
            var chrome = BuildTypingChrome(root.transform, font,
                out TMP_InputField input, out TextMeshProUGUI hint);

            var scroll = BuildLog(root.transform, font, out TextMeshProUGUI body);

            Wire(panel, group, chrome, scroll, body, input, hint);

            // 입력칸은 꺼진 채로 저장한다. 켜진 채 두면 씬을 열자마자 아무도 치지 않는데
            // 판이 깔려 있다.
            chrome.SetActive(false);

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log($"[ChatPanelSetupTool] {GetPath(root.transform)} 에 채팅을 만들었습니다.");
        }

        // ── 글을 치는 동안에만 서는 것들 ─────────────────────────────

        /// <summary>
        /// 반투명 판과 입력칸. 둘을 한 덩어리로 묶어 두면 <see cref="ChatPanel"/>이
        /// 껐다 켜는 것 하나만 알면 된다.
        /// </summary>
        private static GameObject BuildTypingChrome(
            Transform parent, TMP_FontAsset font,
            out TMP_InputField input, out TextMeshProUGUI hint)
        {
            var chrome = Child(parent, "Typing");
            Stretch((RectTransform)chrome.transform);

            // 판은 입력칸 위쪽만 덮는다. 입력칸에는 저만의 테두리가 있다.
            var backdrop = Child(chrome.transform, "Backdrop");
            Fill((RectTransform)backdrop.transform, 0f, 0f, 0f, InputHeight + Gap);

            var frame = backdrop.AddComponent<RoundedPanelGraphic>();
            frame.Configure(BackdropFill, new Color(0f, 0f, 0f, 0f), BackdropRadius, 0f,
                new Color(0f, 0f, 0f, 0f), 0f, 0f,
                new Color(0f, 0f, 0f, 0f), Vector2.zero, 0f);
            // 판이 raycast를 먹어야 글을 치는 동안 판 위를 눌렀을 때 뒤의 테이블이
            // 반응하지 않는다. 치지 않는 동안에는 CanvasGroup이 통째로 막아 준다.
            frame.raycastTarget = true;

            input = BuildInput(chrome.transform, font, out hint);
            var inputRect = (RectTransform)input.transform;
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = new Vector2(1f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.offsetMin = Vector2.zero;
            inputRect.offsetMax = new Vector2(0f, InputHeight);

            return chrome;
        }

        private static TMP_InputField BuildInput(
            Transform parent, TMP_FontAsset font, out TextMeshProUGUI hint)
        {
            var go = Child(parent, "Input");

            var frame = go.AddComponent<RoundedPanelGraphic>();
            frame.Configure(InputFill, CardInner, InputRadius, 1.4f,
                new Color(0f, 0f, 0f, 0f), 0f, 0f,
                new Color(0f, 0f, 0f, 0f), Vector2.zero, 0f);
            frame.raycastTarget = true;

            // 글자가 칸 밖으로 새지 않도록 잘라 주는 안쪽 칸. TMP_InputField가 요구한다.
            var area = Child(go.transform, "TextArea");
            var areaRect = (RectTransform)area.transform;
            Stretch(areaRect);
            areaRect.offsetMin = new Vector2(12f, 4f);
            areaRect.offsetMax = new Vector2(-12f, -4f);
            area.AddComponent<RectMask2D>();

            var text = Label(area.transform, "Text", BodySize, TitleColor,
                TextAlignmentOptions.Left, font);
            Stretch((RectTransform)text.transform);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;

            // <b>켜 두어야 한다.</b> TMP_InputField는 한글을 조합하는 동안 조합 중인 글자를
            // <u>…</u>로 감싸 밑줄을 긋는다. 서식을 꺼 두면 그 꺾쇠가 <b>글자로 보이고</b>,
            // 더 나쁘게는 커서 위치 계산이 그 일곱 글자만큼 밀려 한글이 뭉개진다
            // ("안녕"을 치면 "안ㄴ"이 남았다).
            //
            // 사람이 친 서식이 기록에 남는 문제는 여기서 막지 않는다. 보낼 때
            // ChatLog.Sanitize가 '<'를 바꿔 놓으므로 채팅에 남는 것은 언제나 글자다.
            text.richText = true;

            hint = Label(area.transform, "Placeholder", BodySize, HintColor,
                TextAlignmentOptions.Left, font);
            Stretch((RectTransform)hint.transform);
            hint.textWrappingMode = TextWrappingModes.NoWrap;
            hint.text = "Enter로 보내기 · Esc로 취소";

            var input = go.AddComponent<TMP_InputField>();
            input.targetGraphic = frame;
            input.textViewport = areaRect;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = ChatLog.MaxTextLength;
            input.caretColor = TitleColor;
            input.customCaretColor = true;
            input.selectionColor = new Color(0.82f, 0.58f, 0.22f, 0.45f);

            // Esc는 채팅창을 닫는 데 쓴다. TMP가 글까지 되돌려 버리면 다시 열었을 때
            // 쓰던 문장이 사라져 있다.
            input.restoreOriginalTextOnEscape = false;

            return input;
        }

        // ── 오간 말 ──────────────────────────────────────────────────

        /// <summary>
        /// 말이 흐르는 칸. 줄마다 오브젝트를 만들지 않고 글 한 덩어리를 흘린다 —
        /// 줄바꿈과 높이를 TMP가 맡으므로 이쪽이 셀 것이 없다.
        ///
        /// 반투명 판과 같은 자리에 서되 안쪽으로 물러난다. 판이 없는 동안에도 글은
        /// 그대로 그 자리에 남아 있어야 하므로, 글은 판의 자식이 아니다.
        /// </summary>
        private static ScrollRect BuildLog(
            Transform parent, TMP_FontAsset font, out TextMeshProUGUI body)
        {
            var go = Child(parent, "Log");
            Fill((RectTransform)go.transform,
                LogPadding, LogPadding, LogPadding, InputHeight + Gap + LogPadding);

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.inertia = false;

            var viewport = Child(go.transform, "Viewport");
            var viewRect = (RectTransform)viewport.transform;
            Stretch(viewRect);

            // 휠을 굴리려면 이 칸이 raycast를 받아야 한다. 보이지는 않아야 하므로 알파 0.
            // 치지 않는 동안에는 CanvasGroup이 막으므로 테이블을 가로채지 않는다.
            var catcher = viewport.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
            viewport.AddComponent<RectMask2D>();

            body = Label(viewport.transform, "Content", BodySize, BodyColor,
                TextAlignmentOptions.TopLeft, font);
            body.richText = true;
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode = TextOverflowModes.Overflow;
            body.lineSpacing = 6f;
            body.text = string.Empty;

            // 아래부터 쌓는다. 위에 붙여 두면 몇 줄 안 되는 대화가 판 꿄대기에 떠 있고
            // 그 아래로 빈 자리가 넘자리로 남는다. 판을 감춘 동안에는 더 나빤데 — 글만
            // 남은 화면에서 그 글이 허공에 압자인 것처럼 보인다.
            var contentRect = (RectTransform)body.transform;
            contentRect.anchorMin = Vector2.zero;
            contentRect.anchorMax = new Vector2(1f, 0f);
            contentRect.pivot = new Vector2(0.5f, 0f);
            contentRect.offsetMin = new Vector2(4f, 0f);
            contentRect.offsetMax = new Vector2(-4f, 0f);

            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewRect;
            scroll.content = contentRect;
            return scroll;
        }

        // ── 배선 ─────────────────────────────────────────────────────

        private static void Wire(
            ChatPanel panel, CanvasGroup group, GameObject chrome,
            ScrollRect scroll, TextMeshProUGUI body,
            TMP_InputField input, TextMeshProUGUI hint)
        {
            var so = new SerializedObject(panel);
            so.FindProperty("group").objectReferenceValue = group;
            so.FindProperty("typingChrome").objectReferenceValue = chrome;
            so.FindProperty("scroll").objectReferenceValue = scroll;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("input").objectReferenceValue = input;
            so.FindProperty("placeholder").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── 조각들 ───────────────────────────────────────────────────

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

        /// <summary>네 변에서 각각 이만큼 떨어진 채로 나머지를 다 채운다.</summary>
        private static void Fill(
            RectTransform rect, float left, float right, float top, float bottom)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
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
        /// 씬이 이미 쓰고 있는 글꼴을 그대로 빌린다. 경로로 지정해 두면 글꼴을 바꿀 때
        /// 이 화면만 옛 글꼴로 남는다.
        /// </summary>
        private static TMP_FontAsset BorrowFont(Transform canvas)
        {
            foreach (var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (text.font != null) return text.font;
            return null;
        }

        private static string GetPath(Transform t)
        {
            string path = t.name;
            for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
